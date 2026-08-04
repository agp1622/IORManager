using IORManager.Data;
using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using IORManager.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IFinancialDocumentRepository<PurchaseOrder> _repository;
    private readonly IFinancialDocumentPdfService _pdfService;
    private readonly IInvoiceNumberGenerator _numberGenerator;
    private readonly INcfNumberGenerator _ncfNumberGenerator;
    private readonly NcfAssignmentService _ncfAssignmentService;
    private readonly IORManagerContext _context;

    private const long MaxAttachmentBytes = 20 * 1024 * 1024; // 20 MB

    /// <summary>Valid internal-order workflow statuses, in progression order.</summary>
    private static readonly string[] ValidStatuses = { "Pendiente", "EnProceso", "Completada" };

    public PurchaseOrdersController(
        IFinancialDocumentRepository<PurchaseOrder> repository,
        IFinancialDocumentPdfService pdfService,
        IInvoiceNumberGenerator numberGenerator,
        INcfNumberGenerator ncfNumberGenerator,
        NcfAssignmentService ncfAssignmentService,
        IORManagerContext context)
    {
        _repository = repository;
        _pdfService = pdfService;
        _numberGenerator = numberGenerator;
        _ncfNumberGenerator = ncfNumberGenerator;
        _ncfAssignmentService = ncfAssignmentService;
        _context = context;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<PurchaseOrder>> GetPurchaseOrders()
        => Ok(_repository.GetAll());

    [HttpGet("{id:guid}")]
    public ActionResult<PurchaseOrder> GetPurchaseOrder(Guid id)
    {
        var purchaseOrder = _repository.GetById(id);
        return purchaseOrder is not null ? Ok(purchaseOrder) : NotFound();
    }

    [HttpPost]
    public ActionResult<PurchaseOrder> CreatePurchaseOrder([FromBody] PurchaseOrderCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var purchaseOrder = request.ToPurchaseOrder();

        if (string.IsNullOrWhiteSpace(purchaseOrder.Number))
        {
            var candidate = DocumentNumberFormatter.ToOrderNumber(_numberGenerator.GenerateNextNumber());
            while (_context.PurchaseOrders.Any(existing => existing.Number == candidate))
            {
                candidate = DocumentNumberFormatter.ToOrderNumber(_numberGenerator.GenerateNextNumber());
            }

            purchaseOrder.Number = candidate;
        }

        if (purchaseOrder.QuoteId.HasValue)
        {
            var linkedQuote = _context.Quotes
                .Include(quote => quote.Lines)
                .FirstOrDefault(quote => quote.Id == purchaseOrder.QuoteId.Value);

            if (string.IsNullOrWhiteSpace(purchaseOrder.SupplierName))
            {
                purchaseOrder.SupplierName = linkedQuote?.PartyName ?? "N/A";
            }

            // The order tracks the same items being purchased for the quote it was raised
            // from, so seed its lines from the quote's when the caller didn't supply any.
            if (purchaseOrder.Lines.Count == 0 && linkedQuote is not null)
            {
                purchaseOrder.Lines = linkedQuote.Lines.Select(line => new DocumentLine
                {
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    UnitOfMeasure = line.UnitOfMeasure
                }).ToList();
                purchaseOrder.RecalculateTotal();
            }
        }
        else if (string.IsNullOrWhiteSpace(purchaseOrder.SupplierName))
        {
            purchaseOrder.SupplierName = "N/A";
        }

        var created = _repository.Add(purchaseOrder);
        return CreatedAtAction(nameof(GetPurchaseOrder), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public ActionResult<PurchaseOrder> UpdatePurchaseOrder(Guid id, [FromBody] PurchaseOrderUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var po = _context.PurchaseOrders
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == id);

        if (po is null)
        {
            return NotFound();
        }

        po.SupplierName = request.SupplierName.Trim();
        po.Date = request.PurchaseOrderDate;
        po.QuoteId = request.QuoteId;
        po.InvestmentNotes = string.IsNullOrWhiteSpace(request.InvestmentNotes)
            ? null
            : request.InvestmentNotes.Trim();
        po.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? "USD"
            : request.CurrencyCode.Trim().ToUpperInvariant();

        var existingLines = po.Lines.ToList();
        if (existingLines.Count > 0)
        {
            _context.DocumentLines.RemoveRange(existingLines);
        }
        po.Lines.Clear();

        foreach (var lineRequest in request.Lines)
        {
            po.Lines.Add(lineRequest.ToDocumentLine());
        }

        po.RecalculateTotal();
        _context.SaveChanges();

        return Ok(po);
    }

    /// <summary>
    /// Transitions the internal order's workflow status (Pendiente → EnProceso → Completada). Marking a PO
    /// "Completada" is what surfaces the "send invoice" alert for its linked quote, if that quote hasn't been
    /// converted to an invoice yet.
    /// </summary>
    [HttpPost("{id:guid}/status")]
    public ActionResult<PurchaseOrder> UpdatePurchaseOrderStatus(Guid id, [FromBody] PurchaseOrderStatusUpdateRequest? request)
    {
        var status = request?.Status?.Trim();
        if (string.IsNullOrWhiteSpace(status) || !ValidStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid status",
                Detail = $"Status must be one of: {string.Join(", ", ValidStatuses)}.",
            });
        }

        var po = _context.PurchaseOrders.FirstOrDefault(existing => existing.Id == id);
        if (po is null)
        {
            return NotFound();
        }

        if (po.ConvertedInvoiceId.HasValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Order locked",
                Detail = "This order has already been converted into an invoice and can no longer change status.",
            });
        }

        po.Status = ValidStatuses.First(valid => string.Equals(valid, status, StringComparison.OrdinalIgnoreCase));
        _context.SaveChanges();

        return Ok(po);
    }

    /// <summary>
    /// Generates the Invoice for this order's linked quote. Only allowed once the order's workflow status
    /// is "Completada" — this is the sole entry point for creating an invoice (there is no direct
    /// Quote → Invoice path anymore).
    /// </summary>
    [HttpPost("{id:guid}/convert")]
    public ActionResult<QuoteConversionResponse> ConvertPurchaseOrderToInvoice(Guid id, [FromBody] NcfAssignmentRequest? request)
    {
        var order = _context.PurchaseOrders
            .Include(po => po.Quote)
            .FirstOrDefault(po => po.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        if (order.ConvertedInvoiceId.HasValue)
        {
            var existingInvoice = _context.Invoices
                .AsNoTracking()
                .FirstOrDefault(invoice => invoice.Id == order.ConvertedInvoiceId.Value);
            if (existingInvoice is not null)
            {
                return Ok(new QuoteConversionResponse(
                    existingInvoice.Id,
                    existingInvoice.Number,
                    existingInvoice.NcfNumber ?? string.Empty,
                    existingInvoice.NcfCategory ?? NcfCategoryCatalog.DefaultCategoryCode));
            }
        }

        if (!string.Equals(order.Status, "Completada", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Order not completed",
                Detail = "The order must be marked Completada before an invoice can be generated.",
            });
        }

        if (order.QuoteId is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Order has no linked quote",
                Detail = "This order isn't linked to a quote, so an invoice can't be generated from it.",
            });
        }

        var quote = _context.Quotes
            .Include(existing => existing.Lines)
            .Include(existing => existing.Customer)
            .FirstOrDefault(existing => existing.Id == order.QuoteId.Value);
        if (quote is null)
        {
            return NotFound();
        }

        if (quote.ConvertedInvoiceId.HasValue)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Quote already invoiced",
                Detail = "This quote already has an invoice generated from another order.",
            });
        }

        if (!_ncfAssignmentService.TryResolveNcfData(
                request?.NcfNumber,
                request?.NcfCategory,
                out var normalizedNcf,
                out var normalizedCategory,
                out var ncfError))
        {
            return StatusCode(ncfError!.Status ?? StatusCodes.Status400BadRequest, ncfError);
        }

        if (!string.IsNullOrWhiteSpace(normalizedNcf) && _ncfAssignmentService.IsDuplicateNcf(normalizedNcf))
        {
            var conflict = _ncfAssignmentService.DuplicateNcfProblem(normalizedNcf);
            return StatusCode(conflict.Status ?? StatusCodes.Status409Conflict, conflict);
        }

        string? categoryForGeneration = null;
        if (request?.SkipNcf != true)
        {
            categoryForGeneration = normalizedCategory ?? quote.Customer?.DefaultNcfCategory ?? NcfCategoryCatalog.DefaultCategoryCode;
            if (string.IsNullOrWhiteSpace(normalizedNcf))
            {
                normalizedNcf = _ncfNumberGenerator.GenerateNextNumber(categoryForGeneration);
            }

            if (_ncfAssignmentService.IsDuplicateNcf(normalizedNcf))
            {
                var conflict = _ncfAssignmentService.DuplicateNcfProblem(normalizedNcf);
                return StatusCode(conflict.Status ?? StatusCodes.Status409Conflict, conflict);
            }
        }

        var invoiceNumber = DocumentNumberFormatter.ToInvoiceNumber(_numberGenerator.GenerateNextNumber());
        while (_context.Invoices.Any(invoice => invoice.Number == invoiceNumber))
        {
            invoiceNumber = DocumentNumberFormatter.ToInvoiceNumber(_numberGenerator.GenerateNextNumber());
        }

        var generatedAt = DateTime.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = invoiceNumber,
            Date = DateOnly.FromDateTime(generatedAt),
            CurrencyCode = quote.CurrencyCode,
            CultureName = quote.CultureName,
            CustomerId = quote.CustomerId,
            CustomerName = quote.CustomerName,
            CustomerAddress = quote.CustomerAddress,
            CustomerContact = quote.CustomerContact,
            QuoteId = quote.Id,
            OrderId = order.Id,
            ItbisRate = quote.ItbisRate,
            NcfNumber = normalizedNcf,
            NcfCategory = normalizedCategory ?? categoryForGeneration ?? null,
            InvoiceGeneratedAt = generatedAt,
            Lines = quote.Lines.Select(line => new DocumentLine
            {
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                UnitOfMeasure = line.UnitOfMeasure
            }).ToList()
        };
        invoice.RecalculateTotal();

        order.ConvertedInvoiceId = invoice.Id;
        order.ConvertedAt = generatedAt;
        quote.ConvertedInvoiceId = invoice.Id;
        quote.ConvertedAt = generatedAt;

        _context.Invoices.Add(invoice);
        _context.SaveChanges();

        return Ok(new QuoteConversionResponse(
            invoice.Id,
            invoice.Number,
            invoice.NcfNumber ?? string.Empty,
            invoice.NcfCategory ?? NcfCategoryCatalog.DefaultCategoryCode));
    }

    [HttpGet("{id:guid}/pdf")]
    public ActionResult GetPurchaseOrderPdf(Guid id)
    {
        var purchaseOrder = _repository.GetById(id);
        if (purchaseOrder is null)
        {
            return NotFound();
        }

        var pdfBytes = _pdfService.GeneratePurchaseOrderPdf(purchaseOrder);
        return File(pdfBytes, "application/pdf", $"PurchaseOrder-{purchaseOrder.Number}.pdf");
    }

    // ── Attachments ──────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/attachments")]
    public ActionResult<IReadOnlyCollection<object>> GetAttachments(Guid id)
    {
        if (!_context.PurchaseOrders.Any(po => po.Id == id))
        {
            return NotFound();
        }

        var attachments = _context.PurchaseOrderAttachments
            .Where(a => a.PurchaseOrderId == id)
            .OrderBy(a => a.UploadedAt)
            .Select(a => new
            {
                a.Id,
                a.FileName,
                a.ContentType,
                a.FileSize,
                a.UploadedAt,
            })
            .ToList<object>();

        return Ok(attachments);
    }

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(MaxAttachmentBytes + 1024)]
    public async Task<ActionResult> UploadAttachment(Guid id, IFormFile file)
    {
        if (!_context.PurchaseOrders.Any(po => po.Id == id))
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "No file provided",
                Detail = "Please attach a file to upload.",
            });
        }

        if (file.Length > MaxAttachmentBytes)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "File too large",
                Detail = $"Attachments must be smaller than {MaxAttachmentBytes / (1024 * 1024)} MB.",
            });
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var attachment = new PurchaseOrderAttachment
        {
            PurchaseOrderId = id,
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType ?? "application/octet-stream",
            FileSize = file.Length,
            FileData = ms.ToArray(),
            UploadedAt = DateTime.UtcNow,
        };

        _context.PurchaseOrderAttachments.Add(attachment);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.FileSize,
            attachment.UploadedAt,
        });
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:int}/download")]
    public ActionResult DownloadAttachment(Guid id, int attachmentId)
    {
        var attachment = _context.PurchaseOrderAttachments
            .FirstOrDefault(a => a.PurchaseOrderId == id && a.Id == attachmentId);

        if (attachment is null)
        {
            return NotFound();
        }

        return File(attachment.FileData, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:int}")]
    public ActionResult DeleteAttachment(Guid id, int attachmentId)
    {
        var attachment = _context.PurchaseOrderAttachments
            .FirstOrDefault(a => a.PurchaseOrderId == id && a.Id == attachmentId);

        if (attachment is null)
        {
            return NotFound();
        }

        _context.PurchaseOrderAttachments.Remove(attachment);
        _context.SaveChanges();

        return NoContent();
    }

    // ── Expenses ─────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/expenses")]
    public ActionResult<IReadOnlyCollection<object>> GetExpenses(Guid id)
    {
        if (!_context.PurchaseOrders.Any(po => po.Id == id))
        {
            return NotFound();
        }

        var expenses = _context.OrderExpenses
            .Where(e => e.OrderId == id)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id,
                e.Description,
                e.Date,
                e.Amount,
                e.CurrencyCode,
                e.HasInvoice,
                e.Rnc,
                e.ReceiptFileName,
                e.ReceiptContentType,
                e.ReceiptFileSize,
                e.ReceiptUploadedAt,
                e.CreatedAt,
            })
            .ToList<object>();

        return Ok(expenses);
    }

    [HttpPost("{id:guid}/expenses")]
    public ActionResult CreateExpense(Guid id, [FromBody] OrderExpenseCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var order = _context.PurchaseOrders
            .Include(po => po.Expenses)
            .FirstOrDefault(po => po.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        var expense = new OrderExpense
        {
            OrderId = id,
            Description = request.Description.Trim(),
            Date = request.Date,
            Amount = request.Amount,
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "USD" : request.CurrencyCode.Trim().ToUpperInvariant(),
            HasInvoice = request.HasInvoice,
            Rnc = request.HasInvoice && !string.IsNullOrWhiteSpace(request.Rnc) ? request.Rnc.Trim() : null,
            CreatedAt = DateTime.UtcNow,
        };

        _context.OrderExpenses.Add(expense);
        order.Expenses.Add(expense);
        order.RecalculateTotal();
        _context.SaveChanges();

        return CreatedAtAction(nameof(GetExpenses), new { id }, new
        {
            expense.Id,
            expense.Description,
            expense.Date,
            expense.Amount,
            expense.CurrencyCode,
            expense.HasInvoice,
            expense.Rnc,
            expense.ReceiptFileName,
            expense.ReceiptContentType,
            expense.ReceiptFileSize,
            expense.ReceiptUploadedAt,
            expense.CreatedAt,
        });
    }

    [HttpPut("{id:guid}/expenses/{expenseId:int}")]
    public ActionResult UpdateExpense(Guid id, int expenseId, [FromBody] OrderExpenseUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var order = _context.PurchaseOrders
            .Include(po => po.Expenses)
            .FirstOrDefault(po => po.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        var expense = order.Expenses.FirstOrDefault(e => e.Id == expenseId);
        if (expense is null)
        {
            return NotFound();
        }

        expense.Description = request.Description.Trim();
        expense.Date = request.Date;
        expense.Amount = request.Amount;
        expense.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "USD" : request.CurrencyCode.Trim().ToUpperInvariant();
        expense.HasInvoice = request.HasInvoice;
        expense.Rnc = request.HasInvoice && !string.IsNullOrWhiteSpace(request.Rnc) ? request.Rnc.Trim() : null;

        order.RecalculateTotal();
        _context.SaveChanges();

        return Ok(new
        {
            expense.Id,
            expense.Description,
            expense.Date,
            expense.Amount,
            expense.CurrencyCode,
            expense.HasInvoice,
            expense.Rnc,
            expense.ReceiptFileName,
            expense.ReceiptContentType,
            expense.ReceiptFileSize,
            expense.ReceiptUploadedAt,
            expense.CreatedAt,
        });
    }

    [HttpDelete("{id:guid}/expenses/{expenseId:int}")]
    public ActionResult DeleteExpense(Guid id, int expenseId)
    {
        var order = _context.PurchaseOrders
            .Include(po => po.Expenses)
            .FirstOrDefault(po => po.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        var expense = order.Expenses.FirstOrDefault(e => e.Id == expenseId);
        if (expense is null)
        {
            return NotFound();
        }

        order.Expenses.Remove(expense);
        _context.OrderExpenses.Remove(expense);
        order.RecalculateTotal();
        _context.SaveChanges();

        return NoContent();
    }

    [HttpPost("{id:guid}/expenses/{expenseId:int}/receipt")]
    [RequestSizeLimit(MaxAttachmentBytes + 1024)]
    public async Task<ActionResult> UploadExpenseReceipt(Guid id, int expenseId, IFormFile file)
    {
        var expense = _context.OrderExpenses.FirstOrDefault(e => e.OrderId == id && e.Id == expenseId);
        if (expense is null)
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "No file provided",
                Detail = "Please attach a file to upload.",
            });
        }

        if (file.Length > MaxAttachmentBytes)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "File too large",
                Detail = $"Attachments must be smaller than {MaxAttachmentBytes / (1024 * 1024)} MB.",
            });
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        expense.ReceiptFileName = Path.GetFileName(file.FileName);
        expense.ReceiptContentType = file.ContentType ?? "application/octet-stream";
        expense.ReceiptFileSize = file.Length;
        expense.ReceiptFileData = ms.ToArray();
        expense.ReceiptUploadedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            expense.Id,
            expense.ReceiptFileName,
            expense.ReceiptContentType,
            expense.ReceiptFileSize,
            expense.ReceiptUploadedAt,
        });
    }

    [HttpGet("{id:guid}/expenses/{expenseId:int}/receipt")]
    public ActionResult DownloadExpenseReceipt(Guid id, int expenseId)
    {
        var expense = _context.OrderExpenses.FirstOrDefault(e => e.OrderId == id && e.Id == expenseId);
        if (expense?.ReceiptFileData is null)
        {
            return NotFound();
        }

        return File(expense.ReceiptFileData, expense.ReceiptContentType ?? "application/octet-stream", expense.ReceiptFileName ?? "receipt");
    }

    [HttpDelete("{id:guid}/expenses/{expenseId:int}/receipt")]
    public ActionResult DeleteExpenseReceipt(Guid id, int expenseId)
    {
        var expense = _context.OrderExpenses.FirstOrDefault(e => e.OrderId == id && e.Id == expenseId);
        if (expense is null)
        {
            return NotFound();
        }

        expense.ReceiptFileName = null;
        expense.ReceiptContentType = null;
        expense.ReceiptFileSize = null;
        expense.ReceiptFileData = null;
        expense.ReceiptUploadedAt = null;
        _context.SaveChanges();

        return NoContent();
    }
}

public record PurchaseOrderStatusUpdateRequest(string Status);
