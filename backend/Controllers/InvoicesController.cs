using System.Linq;
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
public class InvoicesController : ControllerBase
{
    private readonly IFinancialDocumentRepository<Invoice> _repository;
    private readonly IFinancialDocumentPdfService _pdfService;
    private readonly IInvoiceNumberGenerator _numberGenerator;
    private readonly INcfNumberGenerator _ncfNumberGenerator;
    private readonly IORManagerContext _context;

    public InvoicesController(
        IFinancialDocumentRepository<Invoice> repository,
        IFinancialDocumentPdfService pdfService,
        IInvoiceNumberGenerator numberGenerator,
        INcfNumberGenerator ncfNumberGenerator,
        IORManagerContext context)
    {
        _repository = repository;
        _pdfService = pdfService;
        _numberGenerator = numberGenerator;
        _ncfNumberGenerator = ncfNumberGenerator;
        _context = context;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<Invoice>> GetInvoices()
        => Ok(_repository.GetAll());

    [HttpGet("{id:guid}")]
    public ActionResult<Invoice> GetInvoice(Guid id)
    {
        var invoice = _repository.GetById(id);
        return invoice is not null ? Ok(invoice) : NotFound();
    }

    [HttpGet("next-ncf")]
    public ActionResult<NcfAssignmentResponse> GetNextNcf()
        => Ok(new NcfAssignmentResponse(_ncfNumberGenerator.PeekNextNumber()));

    [HttpPost]
    public ActionResult<InvoiceCreateResponse> CreateInvoice([FromBody] InvoiceCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var invoice = request.ToInvoice();
        invoice.NcfNumber = NormalizeNcfNumber(invoice.NcfNumber);
        if (!string.IsNullOrWhiteSpace(invoice.NcfNumber) &&
            IsDuplicateNcf(invoice.NcfNumber, invoice.Id))
        {
            return DuplicateNcfConflict(invoice.NcfNumber);
        }

        invoice.InvoiceGeneratedAt = string.IsNullOrWhiteSpace(invoice.NcfNumber)
            ? null
            : DateTime.UtcNow;
        invoice.Number = _numberGenerator.GenerateNextNumber();

        var savedInvoice = _repository.Add(invoice);
        var pdfBytes = _pdfService.GenerateQuotePdf(savedInvoice);
        var response = new InvoiceCreateResponse(
            savedInvoice,
            $"Quote-{savedInvoice.Number}.pdf",
            Convert.ToBase64String(pdfBytes));

        return CreatedAtAction(nameof(GetInvoice), new { id = savedInvoice.Id }, response);
    }

    [HttpGet("{id:guid}/pdf")]
    public ActionResult GetInvoicePdf(Guid id)
    {
        var invoice = _repository.GetById(id);
        if (invoice is null)
        {
            return NotFound();
        }

        var pdfBytes = _pdfService.GenerateQuotePdf(invoice);
        return File(pdfBytes, "application/pdf", $"Quote-{invoice.Number}.pdf");
    }

    [HttpGet("{id:guid}/invoice-pdf")]
    public ActionResult GetInvoiceDocumentPdf(Guid id, [FromQuery] string? ncfNumber)
    {
        var invoice = GetInvoiceWithLines(id);
        if (invoice is null)
        {
            return NotFound();
        }

        if (!TryAssignNcfNumber(invoice, ncfNumber, out var normalizedNcf, out var errorResult))
        {
            return errorResult!;
        }

        var pdfBytes = _pdfService.GenerateInvoicePdf(invoice, normalizedNcf);
        var invoiceNumber = DocumentNumberFormatter.ToInvoiceNumber(invoice.Number);
        return File(pdfBytes, "application/pdf", $"Invoice-{invoiceNumber}.pdf");
    }

    [HttpGet("{id:guid}/invoice-word")]
    public ActionResult GetInvoiceDocumentWord(Guid id, [FromQuery] string? ncfNumber)
    {
        var invoice = GetInvoiceWithLines(id);
        if (invoice is null)
        {
            return NotFound();
        }

        if (!TryAssignNcfNumber(invoice, ncfNumber, out var normalizedNcf, out var errorResult))
        {
            return errorResult!;
        }

        var docBytes = _pdfService.GenerateInvoiceWord(invoice, normalizedNcf);
        var invoiceNumber = DocumentNumberFormatter.ToInvoiceNumber(invoice.Number);
        return File(docBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"Invoice-{invoiceNumber}.docx");
    }

    [HttpPut("{id:guid}")]
    public ActionResult<Invoice> UpdateInvoice(Guid id, [FromBody] InvoiceUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var invoice = _context.Invoices
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == id);

        if (invoice is null)
        {
            return NotFound();
        }

        invoice.Date = request.InvoiceDate;
        invoice.CustomerName = request.CustomerName;
        invoice.CustomerAddress = request.CustomerAddress;
        invoice.CustomerContact = request.CustomerContact;
        invoice.CurrencyCode = request.CurrencyCode;
        invoice.CultureName = request.Locale;
        invoice.ItbisRate = request.ItbisRate;
        var normalizedNcf = NormalizeNcfNumber(request.NcfNumber);
        if (!string.IsNullOrWhiteSpace(normalizedNcf) &&
            IsDuplicateNcf(normalizedNcf, invoice.Id))
        {
            return DuplicateNcfConflict(normalizedNcf);
        }

        invoice.NcfNumber = normalizedNcf;
        invoice.InvoiceGeneratedAt = string.IsNullOrWhiteSpace(normalizedNcf)
            ? null
            : invoice.InvoiceGeneratedAt ?? DateTime.UtcNow;

        var existingLines = invoice.Lines.ToList();
        if (existingLines.Count > 0)
        {
            _context.DocumentLines.RemoveRange(existingLines);
        }
        invoice.Lines.Clear();

        foreach (var lineRequest in request.Lines)
        {
            invoice.Lines.Add(lineRequest.ToDocumentLine());
        }

        invoice.RecalculateTotal();
        _context.SaveChanges();

        return Ok(invoice);
    }

    private Invoice? GetInvoiceWithLines(Guid id) =>
        _context.Invoices
            .Include(existing => existing.Lines)
            .FirstOrDefault(existing => existing.Id == id);

    [HttpPost("{id:guid}/ncf")]
    public ActionResult<NcfAssignmentResponse> AssignNcf(Guid id, [FromBody] NcfAssignmentRequest? request)
    {
        var invoice = GetInvoiceWithLines(id);
        if (invoice is null)
        {
            return NotFound();
        }

        if (!TryAssignNcfNumber(invoice, request?.NcfNumber, out var normalized, out var errorResult))
        {
            return errorResult!;
        }

        return Ok(new NcfAssignmentResponse(normalized!));
    }

    private bool TryAssignNcfNumber(
        Invoice invoice,
        string? requestedNcf,
        out string? normalizedNcf,
        out ActionResult? errorResult)
    {
        normalizedNcf = NormalizeNcfNumber(requestedNcf);

        if (!string.IsNullOrWhiteSpace(normalizedNcf) &&
            IsDuplicateNcf(normalizedNcf, invoice.Id))
        {
            errorResult = DuplicateNcfConflict(normalizedNcf);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(normalizedNcf))
        {
            invoice.NcfNumber = normalizedNcf;
        }
        else if (string.IsNullOrWhiteSpace(invoice.NcfNumber))
        {
            invoice.NcfNumber = _ncfNumberGenerator.GenerateNextNumber();
            normalizedNcf = invoice.NcfNumber;
        }
        else
        {
            normalizedNcf = invoice.NcfNumber;
        }

        if (string.IsNullOrWhiteSpace(invoice.NcfNumber))
        {
            errorResult = BadRequest(new ProblemDetails
            {
                Title = "NCF is required",
                Detail = "An NCF value could not be determined.",
            });
            return false;
        }

        if (IsDuplicateNcf(invoice.NcfNumber!, invoice.Id))
        {
            errorResult = DuplicateNcfConflict(invoice.NcfNumber!);
            return false;
        }

        invoice.InvoiceGeneratedAt ??= DateTime.UtcNow;
        _context.SaveChanges();
        normalizedNcf = invoice.NcfNumber;
        errorResult = null;
        return true;
    }

    private bool IsDuplicateNcf(string normalizedNcf, Guid? excludingId) =>
        _context.Invoices.Any(invoice =>
            invoice.NcfNumber == normalizedNcf &&
            (!excludingId.HasValue || invoice.Id != excludingId.Value));

    private ActionResult DuplicateNcfConflict(string normalizedNcf) =>
        Conflict(new ProblemDetails
        {
            Title = "Duplicate NCF",
            Detail = $"The NCF \"{normalizedNcf}\" is already assigned to another invoice.",
        });

    private static string? NormalizeNcfNumber(string? ncfNumber)
    {
        if (string.IsNullOrWhiteSpace(ncfNumber))
        {
            return null;
        }

        var trimmed = ncfNumber.Trim();
        const string prefix = "NCF";

        if (trimmed.Length >= prefix.Length &&
            trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var suffix = trimmed[prefix.Length..];
            if (string.IsNullOrWhiteSpace(suffix))
            {
                return null;
            }

            return $"{prefix}{suffix}";
        }

        var joiner = char.IsLetterOrDigit(trimmed[0]) ? " " : string.Empty;
        return $"{prefix}{joiner}{trimmed}";
    }
}
