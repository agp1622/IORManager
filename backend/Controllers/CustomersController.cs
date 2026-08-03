using IORManager.Data;
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
                customer.Rnc,
                customer.DefaultPaymentTermsDays,
                customer.UpdatedAt))
            .ToArray();

        return Ok(customers);
    }

    /// <summary>Sets or clears a customer's RNC/Cédula, used to identify the buyer on their e-CFs.</summary>
    [HttpPut("{id:guid}/rnc")]
    public ActionResult UpdateRnc(Guid id, [FromBody] CustomerRncRequest request)
    {
        var customer = _context.Customers.FirstOrDefault(c => c.Id == id);
        if (customer is null)
        {
            return NotFound();
        }

        var normalized = request.Rnc?.Trim();
        customer.Rnc = string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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
    string? Rnc,
    int DefaultPaymentTermsDays,
    DateTime UpdatedAt);

public record CustomerPaymentTermsRequest(int DefaultPaymentTermsDays);

public record CustomerRncRequest(string? Rnc);
