using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using IORManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IFinancialDocumentRepository<PurchaseOrder> _repository;
    private readonly IFinancialDocumentPdfService _pdfService;

    public PurchaseOrdersController(
        IFinancialDocumentRepository<PurchaseOrder> repository,
        IFinancialDocumentPdfService pdfService)
    {
        _repository = repository;
        _pdfService = pdfService;
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
}
