using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using IORManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly IFinancialDocumentRepository<Invoice> _repository;
    private readonly IFinancialDocumentPdfService _pdfService;
    private readonly IInvoiceNumberGenerator _numberGenerator;

    public InvoicesController(
        IFinancialDocumentRepository<Invoice> repository,
        IFinancialDocumentPdfService pdfService,
        IInvoiceNumberGenerator numberGenerator)
    {
        _repository = repository;
        _pdfService = pdfService;
        _numberGenerator = numberGenerator;
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

    [HttpPost]
    public ActionResult<InvoiceCreateResponse> CreateInvoice([FromBody] InvoiceCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var invoice = request.ToInvoice();
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
        var invoice = _repository.GetById(id);
        if (invoice is null)
        {
            return NotFound();
        }

        var normalizedNcf = NormalizeNcfNumber(ncfNumber);
        var pdfBytes = _pdfService.GenerateInvoicePdf(invoice, normalizedNcf);
        var invoiceNumber = DocumentNumberFormatter.ToInvoiceNumber(invoice.Number);
        return File(pdfBytes, "application/pdf", $"Invoice-{invoiceNumber}.pdf");
    }

    [HttpGet("{id:guid}/invoice-word")]
    public ActionResult GetInvoiceDocumentWord(Guid id, [FromQuery] string? ncfNumber)
    {
        var invoice = _repository.GetById(id);
        if (invoice is null)
        {
            return NotFound();
        }

        var normalizedNcf = NormalizeNcfNumber(ncfNumber);
        var docBytes = _pdfService.GenerateInvoiceWord(invoice, normalizedNcf);
        var invoiceNumber = DocumentNumberFormatter.ToInvoiceNumber(invoice.Number);
        return File(docBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"Invoice-{invoiceNumber}.docx");
    }

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
