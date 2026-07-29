using IORManager.Data;
using IORManager.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IORManager.Controllers;

/// <summary>
/// A lightweight accounting summary: net income (revenue minus expenses), an ISR estimate at a
/// user-configured flat rate, and ITBIS payable (collected on B01 invoices minus paid on
/// receipted expenses). Scoped to DOP only — ISR/ITBIS are Dominican fiscal concepts and this
/// app has no exchange-rate mechanism to fold USD activity into the same figures honestly.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AccountingController : ControllerBase
{
    private const string ReportingCurrency = "DOP";
    private const string ItbisEligibleNcfCategory = "B01";

    /// <summary>Standard DR ITBIS rate, used to back out the tax portion of a tax-inclusive expense
    /// amount. Expenses don't carry a per-item rate the way invoice lines do.</summary>
    private const decimal StandardItbisRate = 0.18m;

    private readonly IORManagerContext _context;

    public AccountingController(IORManagerContext context)
    {
        _context = context;
    }

    [HttpGet("settings")]
    public ActionResult<AccountingSettings> GetSettings()
    {
        var settings = _context.AccountingSettings.FirstOrDefault(s => s.Id == 1);
        return Ok(settings ?? new AccountingSettings { Id = 1, IsrRatePercent = 0m });
    }

    [HttpPut("settings")]
    public ActionResult<AccountingSettings> UpdateSettings([FromBody] AccountingSettingsUpdateRequest request)
    {
        var settings = _context.AccountingSettings.FirstOrDefault(s => s.Id == 1);
        if (settings is null)
        {
            settings = new AccountingSettings { Id = 1 };
            _context.AccountingSettings.Add(settings);
        }

        settings.IsrRatePercent = request.IsrRatePercent;
        _context.SaveChanges();

        return Ok(settings);
    }

    [HttpGet("summary")]
    public ActionResult<AccountingSummaryResponse> GetSummary([FromQuery] int? year, [FromQuery] int? month)
    {
        var today = DateTime.UtcNow;
        var targetYear = year is > 0 ? year.Value : today.Year;
        var targetMonth = month is >= 1 and <= 12 ? month.Value : today.Month;

        var monthStart = new DateOnly(targetYear, targetMonth, 1);
        var monthEnd = monthStart.AddMonths(1);

        var invoices = _context.Invoices
            .Include(i => i.Lines)
            .Where(i => i.CurrencyCode == ReportingCurrency && i.Date >= monthStart && i.Date < monthEnd)
            .ToList();

        var revenueSubtotal = invoices.Sum(i => i.CalculateFinancials().Subtotal);
        var itbisCollected = invoices
            .Where(i => i.NcfCategory == ItbisEligibleNcfCategory)
            .Sum(i => i.CalculateFinancials().ItbisAmount);

        var expenses = _context.OrderExpenses
            .Where(e => e.CurrencyCode == ReportingCurrency && e.Date >= monthStart && e.Date < monthEnd)
            .ToList();

        var expensesTotal = expenses.Sum(e => e.Amount);
        var itbisPaid = expenses
            .Where(e => e.HasInvoice && !string.IsNullOrWhiteSpace(e.Rnc))
            .Sum(e => e.Amount * StandardItbisRate / (1 + StandardItbisRate));

        var netIncome = revenueSubtotal - expensesTotal;
        var settings = _context.AccountingSettings.FirstOrDefault(s => s.Id == 1);
        var isrRatePercent = settings?.IsrRatePercent ?? 0m;
        var estimatedIsr = netIncome > 0 ? netIncome * isrRatePercent / 100 : 0m;

        var itbisPayable = itbisCollected - itbisPaid;

        return Ok(new AccountingSummaryResponse(
            ReportingCurrency,
            targetYear,
            targetMonth,
            revenueSubtotal,
            expensesTotal,
            netIncome,
            isrRatePercent,
            estimatedIsr,
            itbisCollected,
            itbisPaid,
            itbisPayable,
            invoices.Count,
            expenses.Count));
    }
}

public record AccountingSettingsUpdateRequest(decimal IsrRatePercent);

public record AccountingSummaryResponse(
    string Currency,
    int Year,
    int Month,
    decimal RevenueSubtotal,
    decimal ExpensesTotal,
    decimal NetIncome,
    decimal IsrRatePercent,
    decimal EstimatedIsr,
    decimal ItbisCollected,
    decimal ItbisPaid,
    decimal ItbisPayable,
    int InvoiceCount,
    int ExpenseCount);
