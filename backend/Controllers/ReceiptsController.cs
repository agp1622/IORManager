using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using IORManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReceiptsController : ControllerBase
{
    private readonly IFinancialDocumentRepository<Receipt> _repository;
    private readonly IFinancialDocumentPdfService _pdfService;

    public ReceiptsController(
        IFinancialDocumentRepository<Receipt> repository,
        IFinancialDocumentPdfService pdfService)
    {
        _repository = repository;
        _pdfService = pdfService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<Receipt>> GetReceipts()
        => Ok(_repository.GetAll());

    [HttpGet("{id:guid}")]
    public ActionResult<Receipt> GetReceipt(Guid id)
    {
        var receipt = _repository.GetById(id);
        return receipt is not null ? Ok(receipt) : NotFound();
    }

    [HttpPost]
    public ActionResult<Receipt> CreateReceipt([FromBody] ReceiptCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var receipt = _repository.Add(request.ToReceipt());
        return CreatedAtAction(nameof(GetReceipt), new { id = receipt.Id }, receipt);
    }

    [HttpGet("{id:guid}/pdf")]
    public ActionResult GetReceiptPdf(Guid id)
    {
        var receipt = _repository.GetById(id);
        if (receipt is null)
        {
            return NotFound();
        }

        var pdfBytes = _pdfService.GenerateReceiptPdf(receipt);
        return File(pdfBytes, "application/pdf", $"Receipt-{receipt.Number}.pdf");
    }
}
