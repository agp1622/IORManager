using System.Linq;
using IORManager.Data;
using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using IORManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuotesController : ControllerBase
{
    private readonly IFinancialDocumentRepository<Quote> _repository;
    private readonly IFinancialDocumentPdfService _pdfService;
    private readonly IInvoiceNumberGenerator _numberGenerator;
    private readonly IORManagerContext _context;

    public QuotesController(
        IFinancialDocumentRepository<Quote> repository,
        IFinancialDocumentPdfService pdfService,
        IInvoiceNumberGenerator numberGenerator,
        IORManagerContext context)
    {
        _repository = repository;
        _pdfService = pdfService;
        _numberGenerator = numberGenerator;
        _context = context;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<Quote>> GetQuotes()
    {
        var quotes = _repository.GetAll().ToList();

        if (quotes.Count > 0)
        {
            var quoteIds = quotes.Select(q => q.Id).ToList();

            var quoteIdsWithOrders = _context.PurchaseOrders
                .Where(order => order.QuoteId.HasValue && quoteIds.Contains(order.QuoteId.Value))
                .Select(order => order.QuoteId!.Value)
                .Distinct()
                .ToHashSet();

            foreach (var quote in quotes)
            {
                quote.HasOrder = quoteIdsWithOrders.Contains(quote.Id);
            }

            var expenseRows = _context.OrderExpenses
                .Join(
                    _context.PurchaseOrders,
                    expense => expense.OrderId,
                    order => order.Id,
                    (expense, order) => new { order.QuoteId, expense.Amount, expense.CurrencyCode })
                .Where(row => row.QuoteId.HasValue && quoteIds.Contains(row.QuoteId.Value))
                .ToList();

            var expensesByQuote = expenseRows
                .GroupBy(row => row.QuoteId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var quote in quotes)
            {
                if (!expensesByQuote.TryGetValue(quote.Id, out var rows))
                {
                    continue;
                }

                quote.ExpenseCount = rows.Count;
                quote.TotalExpenses = rows
                    .Where(row => string.Equals(row.CurrencyCode, quote.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                    .Sum(row => row.Amount);

                var otherRows = rows
                    .Where(row => !string.Equals(row.CurrencyCode, quote.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (otherRows.Count > 0)
                {
                    quote.OtherCurrencyExpenses = otherRows.Sum(row => row.Amount);
                    quote.OtherCurrencyCode = otherRows[0].CurrencyCode;
                }
            }
        }

        return Ok(quotes);
    }

    [HttpGet("{id:guid}")]
    public ActionResult<Quote> GetQuote(Guid id)
    {
        var quote = _repository.GetById(id);
        return quote is not null ? Ok(quote) : NotFound();
    }

    /// <summary>Lists soft-deleted quotes still inside their 1-year recovery window (the "trash").</summary>
    [HttpGet("trash")]
    [Authorize(Roles = "Admin")]
    public ActionResult<IReadOnlyCollection<Quote>> GetTrashedQuotes()
    {
        var cutoff = DateTime.UtcNow - FinancialDocument.SoftDeleteRecoveryWindow;
        var quotes = _context.Quotes
            .IgnoreQueryFilters()
            .Include(quote => quote.Lines)
            .Where(quote => quote.DeletedAt != null && quote.DeletedAt >= cutoff)
            .OrderByDescending(quote => quote.DeletedAt)
            .ToList();

        return Ok(quotes);
    }

    /// <summary>Soft-deletes a quote. It remains recoverable for 1 year via <see cref="RestoreQuote"/>.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public ActionResult DeleteQuote(Guid id)
    {
        var quote = _context.Quotes.FirstOrDefault(existing => existing.Id == id);
        if (quote is null)
        {
            return NotFound();
        }

        quote.DeletedAt = DateTime.UtcNow;
        _context.SaveChanges();
        return NoContent();
    }

    /// <summary>Restores a soft-deleted quote, provided its recovery window hasn't expired.</summary>
    [HttpPost("{id:guid}/restore")]
    [Authorize(Roles = "Admin")]
    public ActionResult<Quote> RestoreQuote(Guid id)
    {
        var quote = _context.Quotes
            .IgnoreQueryFilters()
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == id);

        if (quote is null || quote.DeletedAt is null)
        {
            return NotFound();
        }

        var cutoff = DateTime.UtcNow - FinancialDocument.SoftDeleteRecoveryWindow;
        if (quote.DeletedAt < cutoff)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Recovery window expired",
                Detail = "This quote was deleted more than a year ago and can no longer be restored.",
            });
        }

        quote.DeletedAt = null;
        _context.SaveChanges();
        return Ok(quote);
    }

    [HttpPost]
    public ActionResult<QuoteCreateResponse> CreateQuote([FromBody] InvoiceCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!TryResolveCustomerRecord(
                request.CustomerName,
                request.CustomerAddress,
                request.CustomerContact,
                out var customer,
                out var customerError))
        {
            return customerError!;
        }

        var quote = request.ToQuote();
        quote.Date = DateOnly.FromDateTime(DateTime.UtcNow);
        quote.Number = _numberGenerator.GenerateNextNumber();
        quote.CustomerId = customer.Id;
        quote.CustomerName = customer.Name;
        quote.CustomerAddress = customer.Address;
        quote.CustomerContact = customer.Contact;

        var savedQuote = _repository.Add(quote);
        var pdfBytes = _pdfService.GenerateQuotePdf(ToQuotePdfModel(savedQuote), savedQuote.Comments);
        var response = new QuoteCreateResponse(
            savedQuote,
            $"Quote-{savedQuote.Number}.pdf",
            Convert.ToBase64String(pdfBytes));

        return CreatedAtAction(nameof(GetQuote), new { id = savedQuote.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public ActionResult<Quote> UpdateQuote(Guid id, [FromBody] InvoiceUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var quote = _context.Quotes
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == id);

        if (quote is null)
        {
            return NotFound();
        }

        quote.CustomerName = request.CustomerName;
        quote.CustomerAddress = request.CustomerAddress.Trim();
        quote.CustomerContact = request.CustomerContact.Trim();
        quote.CurrencyCode = request.CurrencyCode;
        quote.CultureName = request.Locale;
        quote.ItbisRate = request.ItbisRate;
        quote.CustomerPONumber = string.IsNullOrWhiteSpace(request.CustomerPONumber)
            ? null
            : request.CustomerPONumber.Trim();
        quote.Comments = string.IsNullOrWhiteSpace(request.Comments)
            ? null
            : request.Comments.Trim();

        if (!TryResolveCustomerRecord(
                quote.CustomerName,
                quote.CustomerAddress,
                quote.CustomerContact,
                out var customer,
                out var customerError))
        {
            return customerError!;
        }

        quote.CustomerId = customer.Id;
        quote.CustomerName = customer.Name;
        quote.CustomerAddress = customer.Address;
        quote.CustomerContact = customer.Contact;

        var existingLines = quote.Lines.ToList();
        if (existingLines.Count > 0)
        {
            _context.DocumentLines.RemoveRange(existingLines);
        }
        quote.Lines.Clear();

        foreach (var lineRequest in request.Lines)
        {
            quote.Lines.Add(lineRequest.ToDocumentLine());
        }

        quote.RecalculateTotal();
        _context.SaveChanges();

        return Ok(quote);
    }

    [HttpPost("{id:guid}/duplicate")]
    public ActionResult<Quote> DuplicateQuote(Guid id)
    {
        var source = GetQuoteWithLines(id);
        if (source is null)
        {
            return NotFound();
        }

        var duplicated = new Quote
        {
            Id = Guid.NewGuid(),
            Number = _numberGenerator.GenerateNextNumber(),
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            CustomerId = source.CustomerId,
            CustomerName = source.CustomerName,
            CustomerAddress = source.CustomerAddress,
            CustomerContact = source.CustomerContact,
            CurrencyCode = source.CurrencyCode,
            CultureName = source.CultureName,
            ItbisRate = source.ItbisRate,
            Comments = source.Comments,
            ConvertedInvoiceId = null,
            ConvertedAt = null,
            Lines = source.Lines.Select(line => new DocumentLine
            {
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                UnitOfMeasure = line.UnitOfMeasure
            }).ToList()
        };

        duplicated.RecalculateTotal();
        _context.Quotes.Add(duplicated);
        _context.SaveChanges();

        return CreatedAtAction(nameof(GetQuote), new { id = duplicated.Id }, duplicated);
    }

    [HttpGet("{id:guid}/pdf")]
    public ActionResult GetQuotePdf(Guid id)
    {
        var quote = _repository.GetById(id);
        if (quote is null)
        {
            return NotFound();
        }

        var pdfBytes = _pdfService.GenerateQuotePdf(ToQuotePdfModel(quote), quote.Comments);
        return File(pdfBytes, "application/pdf", $"Quote-{quote.Number}.pdf");
    }

    // Direct Quote → Invoice conversion has been removed: invoices are now only generated
    // from a completed Order (see PurchaseOrdersController.ConvertPurchaseOrderToInvoice).

    [HttpPost("{id:guid}/undo-conversion")]
    public ActionResult<Quote> UndoQuoteConversion(Guid id)
    {
        var quote = GetQuoteWithLines(id);
        if (quote is null)
        {
            return NotFound();
        }

        if (!quote.ConvertedInvoiceId.HasValue)
        {
            return Ok(quote);
        }

        var invoice = _context.Invoices
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == quote.ConvertedInvoiceId.Value);

        if (invoice is not null)
        {
            if (invoice.OrderId.HasValue)
            {
                var order = _context.PurchaseOrders.FirstOrDefault(po => po.Id == invoice.OrderId.Value);
                if (order is not null)
                {
                    order.ConvertedInvoiceId = null;
                    order.ConvertedAt = null;
                }
            }

            var invoiceLines = invoice.Lines.ToList();
            if (invoiceLines.Count > 0)
            {
                _context.DocumentLines.RemoveRange(invoiceLines);
            }

            _context.Invoices.Remove(invoice);
        }

        quote.ConvertedInvoiceId = null;
        quote.ConvertedAt = null;
        _context.SaveChanges();

        return Ok(quote);
    }

    // ── Attachments (customer PO documents) ──────────────────────────────────

    private const long MaxAttachmentBytes = 20 * 1024 * 1024; // 20 MB

    [HttpGet("{id:guid}/attachments")]
    public ActionResult<IReadOnlyCollection<object>> GetAttachments(Guid id)
    {
        if (!_context.Quotes.Any(q => q.Id == id))
        {
            return NotFound();
        }

        var attachments = _context.QuoteAttachments
            .Where(a => a.QuoteId == id)
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
        if (!_context.Quotes.Any(q => q.Id == id))
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

        var attachment = new QuoteAttachment
        {
            QuoteId = id,
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType ?? "application/octet-stream",
            FileSize = file.Length,
            FileData = ms.ToArray(),
            UploadedAt = DateTime.UtcNow,
        };

        _context.QuoteAttachments.Add(attachment);
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
        var attachment = _context.QuoteAttachments
            .FirstOrDefault(a => a.QuoteId == id && a.Id == attachmentId);

        if (attachment is null)
        {
            return NotFound();
        }

        return File(attachment.FileData, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:int}")]
    public ActionResult DeleteAttachment(Guid id, int attachmentId)
    {
        var attachment = _context.QuoteAttachments
            .FirstOrDefault(a => a.QuoteId == id && a.Id == attachmentId);

        if (attachment is null)
        {
            return NotFound();
        }

        _context.QuoteAttachments.Remove(attachment);
        _context.SaveChanges();

        return NoContent();
    }

    private Quote? GetQuoteWithLines(Guid id) =>
        _context.Quotes
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == id);

    private bool TryResolveCustomerRecord(
        string? customerName,
        string? customerAddress,
        string? customerContact,
        out Customer customer,
        out ActionResult? errorResult)
    {
        customer = default!;
        var normalizedName = customerName?.Trim() ?? string.Empty;
        var normalizedAddress = customerAddress?.Trim() ?? string.Empty;
        var normalizedContact = customerContact?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedName) ||
            string.IsNullOrWhiteSpace(normalizedAddress) ||
            string.IsNullOrWhiteSpace(normalizedContact))
        {
            errorResult = BadRequest(new ProblemDetails
            {
                Title = "Customer details required",
                Detail = "Customer name, address, and contact are required.",
            });
            return false;
        }

        customer = _context.Customers
            .FirstOrDefault(existing => existing.Name == normalizedName)
            ?? new Customer
            {
                Id = Guid.NewGuid(),
                Name = normalizedName,
                CreatedAt = DateTime.UtcNow,
            };

        customer.Address = normalizedAddress;
        customer.Contact = normalizedContact;
        customer.UpdatedAt = DateTime.UtcNow;

        if (_context.Entry(customer).State == EntityState.Detached)
        {
            _context.Customers.Add(customer);
        }

        errorResult = null;
        return true;
    }

    private static Invoice ToQuotePdfModel(Quote quote)
    {
        var pdfModel = new Invoice
        {
            Id = quote.Id,
            Number = quote.Number,
            Date = quote.Date,
            CurrencyCode = quote.CurrencyCode,
            CultureName = quote.CultureName,
            CustomerName = quote.CustomerName,
            CustomerAddress = quote.CustomerAddress,
            CustomerContact = quote.CustomerContact,
            ItbisRate = quote.ItbisRate,
            CustomerPONumber = quote.CustomerPONumber,
            Lines = quote.Lines.Select(line => new DocumentLine
            {
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                UnitOfMeasure = line.UnitOfMeasure
            }).ToList()
        };

        pdfModel.RecalculateTotal();
        return pdfModel;
    }
}

public record QuoteCreateResponse(Quote Quote, string PdfFileName, string PdfBase64);
