using IORManager.Data;
using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using IORManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IFinancialDocumentRepository<PurchaseOrder> _repository;
    private readonly IFinancialDocumentPdfService _pdfService;
    private readonly IORManagerContext _context;

    private const long MaxAttachmentBytes = 20 * 1024 * 1024; // 20 MB

    /// <summary>Valid internal-order workflow statuses, in progression order.</summary>
    private static readonly string[] ValidStatuses = { "Pendiente", "EnProceso", "Completada" };

    public PurchaseOrdersController(
        IFinancialDocumentRepository<PurchaseOrder> repository,
        IFinancialDocumentPdfService pdfService,
        IORManagerContext context)
    {
        _repository = repository;
        _pdfService = pdfService;
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

        var purchaseOrder = _repository.Add(request.ToPurchaseOrder());
        return CreatedAtAction(nameof(GetPurchaseOrder), new { id = purchaseOrder.Id }, purchaseOrder);
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

        po.Status = ValidStatuses.First(valid => string.Equals(valid, status, StringComparison.OrdinalIgnoreCase));
        _context.SaveChanges();

        return Ok(po);
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
}

public record PurchaseOrderStatusUpdateRequest(string Status);
