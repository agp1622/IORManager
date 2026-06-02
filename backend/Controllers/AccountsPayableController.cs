using IORManager.Data;
using IORManager.Dtos;
using IORManager.Models;
using IORManager.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IORManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsPayableController : ControllerBase
{
    private readonly IFinancialDocumentRepository<AccountPayable> _repository;
    private readonly IORManagerContext _context;

    public AccountsPayableController(
        IFinancialDocumentRepository<AccountPayable> repository,
        IORManagerContext context)
    {
        _repository = repository;
        _context = context;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<AccountPayable>> GetAll()
    {
        var records = _repository.GetAll();

        // Auto-transition Pendiente → Vencido for overdue records
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var overdueRecords = records
            .Where(ap => ap.Status == "Pendiente" && ap.DueDate < today)
            .ToList();

        if (overdueRecords.Count > 0)
        {
            foreach (var ap in overdueRecords)
                ap.Status = "Vencido";

            _context.SaveChanges();
        }

        return Ok(_repository.GetAll());
    }

    [HttpGet("{id:guid}")]
    public ActionResult<AccountPayable> GetById(Guid id)
    {
        var ap = _repository.GetById(id);
        return ap is not null ? Ok(ap) : NotFound();
    }

    [HttpPost]
    public ActionResult<AccountPayable> Create([FromBody] AccountPayableCreateRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var created = _repository.Add(request.ToAccountPayable());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public ActionResult<AccountPayable> Update(Guid id, [FromBody] AccountPayableUpdateRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var ap = _context.AccountsPayable.FirstOrDefault(a => a.Id == id);
        if (ap is null)
            return NotFound();

        ap.SupplierName = request.SupplierName.Trim();
        ap.CustomerPO = string.IsNullOrWhiteSpace(request.CustomerPO) ? null : request.CustomerPO.Trim();
        ap.TotalAmount = request.Amount;
        ap.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? "USD"
            : request.CurrencyCode.Trim().ToUpperInvariant();
        ap.Date = request.Date;
        ap.DueDate = request.DueDate;
        ap.Status = string.IsNullOrWhiteSpace(request.Status) ? "Pendiente" : request.Status.Trim();
        ap.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        _context.SaveChanges();
        return Ok(ap);
    }

    [HttpDelete("{id:guid}")]
    public ActionResult Delete(Guid id)
    {
        var ap = _context.AccountsPayable.FirstOrDefault(a => a.Id == id);
        if (ap is null)
            return NotFound();

        _context.AccountsPayable.Remove(ap);
        _context.SaveChanges();
        return NoContent();
    }

    [HttpPost("recalculate-due-dates")]
    public ActionResult<RecalculateDueDatesResponse> RecalculateDueDates()
    {
        // Load all AP records that are linked to an invoice and not yet paid.
        var payables = _context.AccountsPayable
            .Where(ap => ap.InvoiceId != null && ap.Status != "Pagado")
            .ToList();

        if (payables.Count == 0)
            return Ok(new RecalculateDueDatesResponse(0));

        // Build a lookup of InvoiceId → (InvoiceDate, CustomerId)
        var invoiceIds = payables.Select(ap => ap.InvoiceId!.Value).Distinct().ToList();
        var invoices = _context.Invoices
            .Where(i => invoiceIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Date, i.CustomerId })
            .ToDictionary(i => i.Id);

        // Build a lookup of CustomerId → DefaultPaymentTermsDays
        var customerIds = invoices.Values
            .Where(i => i.CustomerId.HasValue)
            .Select(i => i.CustomerId!.Value)
            .Distinct()
            .ToList();
        var paymentTerms = _context.Customers
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.DefaultPaymentTermsDays })
            .ToDictionary(c => c.Id, c => c.DefaultPaymentTermsDays);

        int updatedCount = 0;
        foreach (var ap in payables)
        {
            if (!invoices.TryGetValue(ap.InvoiceId!.Value, out var invoice))
                continue;

            var days = invoice.CustomerId.HasValue && paymentTerms.TryGetValue(invoice.CustomerId.Value, out var terms)
                ? terms
                : 30;

            var newDueDate = invoice.Date.AddDays(days);
            if (ap.DueDate == newDueDate)
                continue;

            ap.DueDate = newDueDate;

            // Re-evaluate overdue status after adjusting the date
            if (ap.Status == "Vencido" && newDueDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                ap.Status = "Pendiente";

            updatedCount++;
        }

        if (updatedCount > 0)
            _context.SaveChanges();

        return Ok(new RecalculateDueDatesResponse(updatedCount));
    }
}

public record RecalculateDueDatesResponse(int UpdatedCount);
