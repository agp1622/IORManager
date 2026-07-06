using System.Linq;
using IORManager.Data;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Controllers;

/// <summary>
/// Read-only analytics over quotes and invoices — monthly trends, this month's totals, the
/// quote-to-invoice conversion rate, and a paid vs. unpaid invoice breakdown. All monetary
/// aggregates are scoped to a single currency (via the <c>currency</c> query parameter) since
/// summing amounts across currencies would be meaningless; count-based metrics (like the
/// conversion rate) are currency-independent and always cover every quote.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class InsightsController : ControllerBase
{
    private const int DefaultMonthsBack = 12;

    private readonly IORManagerContext _context;

    public InsightsController(IORManagerContext context)
    {
        _context = context;
    }

    [HttpGet]
    public ActionResult<InsightsResponse> GetInsights([FromQuery] string? currency, [FromQuery] int? months)
    {
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        var monthsBack = months is > 0 and <= 36 ? months.Value : DefaultMonthsBack;

        var today = DateTime.UtcNow;
        var earliestMonthStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-(monthsBack - 1));

        var quotes = _context.Quotes
            .Where(q => q.CurrencyCode == normalizedCurrency && q.Date >= earliestMonthStart)
            .Select(q => new { q.Date, q.TotalAmount })
            .ToList();

        var invoices = _context.Invoices
            .Where(i => i.CurrencyCode == normalizedCurrency && i.Date >= earliestMonthStart)
            .Select(i => new { i.Date, i.TotalAmount })
            .ToList();

        var monthly = new List<MonthlyInsight>();
        for (var i = 0; i < monthsBack; i++)
        {
            var monthStart = earliestMonthStart.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1);

            var monthQuotes = quotes.Where(q => q.Date >= monthStart && q.Date < monthEnd).ToList();
            var monthInvoices = invoices.Where(inv => inv.Date >= monthStart && inv.Date < monthEnd).ToList();

            monthly.Add(new MonthlyInsight(
                monthStart.Year,
                monthStart.Month,
                $"{monthStart.Year:0000}-{monthStart.Month:00}",
                monthQuotes.Count,
                monthQuotes.Sum(q => q.TotalAmount),
                monthInvoices.Count,
                monthInvoices.Sum(inv => inv.TotalAmount)));
        }

        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var currentMonthEnd = currentMonthStart.AddMonths(1);
        var currentMonthQuotes = quotes.Where(q => q.Date >= currentMonthStart && q.Date < currentMonthEnd).ToList();
        var currentMonthInvoices = invoices.Where(inv => inv.Date >= currentMonthStart && inv.Date < currentMonthEnd).ToList();

        var currentMonth = new MonthlyInsight(
            currentMonthStart.Year,
            currentMonthStart.Month,
            $"{currentMonthStart.Year:0000}-{currentMonthStart.Month:00}",
            currentMonthQuotes.Count,
            currentMonthQuotes.Sum(q => q.TotalAmount),
            currentMonthInvoices.Count,
            currentMonthInvoices.Sum(inv => inv.TotalAmount));

        // Conversion rate is count-based and currency-independent, so it looks at every quote,
        // not just the ones in the selected currency.
        var totalQuoteCount = _context.Quotes.Count();
        var convertedQuoteCount = _context.Quotes.Count(q => q.ConvertedInvoiceId != null);
        var conversion = new ConversionInsight(
            totalQuoteCount,
            convertedQuoteCount,
            totalQuoteCount == 0 ? 0 : (decimal)convertedQuoteCount / totalQuoteCount);

        var paidInvoices = _context.Invoices
            .Where(i => i.CurrencyCode == normalizedCurrency && i.PaidAt != null)
            .Select(i => i.TotalAmount)
            .ToList();
        var unpaidInvoices = _context.Invoices
            .Where(i => i.CurrencyCode == normalizedCurrency && i.PaidAt == null)
            .Select(i => i.TotalAmount)
            .ToList();

        var paidBreakdown = new PaidBreakdownInsight(
            paidInvoices.Count,
            paidInvoices.Sum(),
            unpaidInvoices.Count,
            unpaidInvoices.Sum());

        return Ok(new InsightsResponse(normalizedCurrency, monthly, currentMonth, conversion, paidBreakdown));
    }
}

public record InsightsResponse(
    string Currency,
    IReadOnlyList<MonthlyInsight> Monthly,
    MonthlyInsight CurrentMonth,
    ConversionInsight Conversion,
    PaidBreakdownInsight PaidBreakdown);

public record MonthlyInsight(
    int Year,
    int Month,
    string Label,
    int QuotesCount,
    decimal QuotesTotal,
    int InvoicesCount,
    decimal InvoicesTotal);

public record ConversionInsight(int TotalQuotes, int ConvertedQuotes, decimal ConversionRate);

public record PaidBreakdownInsight(int PaidCount, decimal PaidTotal, int UnpaidCount, decimal UnpaidTotal);
