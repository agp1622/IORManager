using IORManager.Data;
using IORManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly IORManagerContext _context;

    public CustomersController(IORManagerContext context)
    {
        _context = context;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<CustomerListItemResponse>> GetCustomers()
    {
        var customers = _context.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Name)
            .Select(customer => new CustomerListItemResponse(
                customer.Id,
                customer.Name,
                customer.Address,
                customer.Contact,
                customer.DefaultPaymentTermsDays,
                customer.DefaultNcfCategory,
                customer.UpdatedAt))
            .ToArray();

        return Ok(customers);
    }

    [HttpPut("{id:guid}/ncf-category")]
    public ActionResult UpdateDefaultNcfCategory(Guid id, [FromBody] CustomerNcfCategoryRequest request)
    {
        if (!NcfCategoryCatalog.TryGetByCode(request.NcfCategory, out var definition))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid NCF category",
                Detail = $"The NCF category \"{request.NcfCategory}\" is not supported.",
            });
        }

        var customer = _context.Customers.FirstOrDefault(c => c.Id == id);
        if (customer is null)
        {
            return NotFound();
        }

        customer.DefaultNcfCategory = definition.Code;
        customer.UpdatedAt = DateTime.UtcNow;
        _context.SaveChanges();

        return NoContent();
    }

    [HttpPut("{id:guid}/payment-terms")]
    public ActionResult UpdatePaymentTerms(Guid id, [FromBody] CustomerPaymentTermsRequest request)
    {
        if (request.DefaultPaymentTermsDays < 1 || request.DefaultPaymentTermsDays > 365)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid payment terms",
                Detail = "Payment terms must be between 1 and 365 days.",
            });
        }

        var customer = _context.Customers.FirstOrDefault(c => c.Id == id);
        if (customer is null)
        {
            return NotFound();
        }

        customer.DefaultPaymentTermsDays = request.DefaultPaymentTermsDays;
        customer.UpdatedAt = DateTime.UtcNow;
        _context.SaveChanges();

        return NoContent();
    }
}

public record CustomerListItemResponse(
    Guid Id,
    string Name,
    string Address,
    string Contact,
    int DefaultPaymentTermsDays,
    string? DefaultNcfCategory,
    DateTime UpdatedAt);

public record CustomerPaymentTermsRequest(int DefaultPaymentTermsDays);

public record CustomerNcfCategoryRequest(string NcfCategory);
