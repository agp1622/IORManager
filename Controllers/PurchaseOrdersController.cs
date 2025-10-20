using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IFinancialDocumentRepository<PurchaseOrder> _repository;

    public PurchaseOrdersController(IFinancialDocumentRepository<PurchaseOrder> repository)
    {
        _repository = repository;
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
}
