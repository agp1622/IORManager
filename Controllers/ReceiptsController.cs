using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReceiptsController : ControllerBase
{
    private readonly IFinancialDocumentRepository<Receipt> _repository;

    public ReceiptsController(IFinancialDocumentRepository<Receipt> repository)
    {
        _repository = repository;
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
}
