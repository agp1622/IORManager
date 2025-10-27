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

    public InvoicesController(
        IFinancialDocumentRepository<Invoice> repository,
        IFinancialDocumentPdfService pdfService)
    {
        _repository = repository;
        _pdfService = pdfService;
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
    public ActionResult<Invoice> CreateInvoice([FromBody] InvoiceCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var invoice = _repository.Add(request.ToInvoice());
        return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, invoice);
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
