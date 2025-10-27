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
        var pdfBytes = _pdfService.GenerateInvoicePdf(savedInvoice);
        var response = new InvoiceCreateResponse(
            savedInvoice,
            $"Invoice-{savedInvoice.Number}.pdf",
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

        var pdfBytes = _pdfService.GenerateInvoicePdf(invoice);
        return File(pdfBytes, "application/pdf", $"Invoice-{invoice.Number}.pdf");
    }
}
