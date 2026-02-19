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
                customer.UpdatedAt))
            .ToArray();

        return Ok(customers);
    }
}

public record CustomerListItemResponse(
    Guid Id,
    string Name,
    string Address,
    string Contact,
    DateTime UpdatedAt);
