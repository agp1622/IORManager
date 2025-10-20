using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly IFinancialDocumentRepository<Invoice> _repository;

    public InvoicesController(IFinancialDocumentRepository<Invoice> repository)
    {
        _repository = repository;
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
}
