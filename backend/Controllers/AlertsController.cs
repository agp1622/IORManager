using System.Linq;
using IORManager.Data;
using IORManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Controllers;

/// <summary>
/// Read-only, computed-on-the-fly action items for the internal-order → invoice → payment workflow:
/// <list type="bullet">
/// <item>"Send invoice" — a purchase order (internal order) was marked Completada but its linked quote hasn't
/// been converted into an invoice yet.</item>
/// <item>"Payment due" — an invoice was marked Sent, its customer's payment terms have elapsed, and it still
/// hasn't been paid.</item>
/// </list>
/// Nothing here is persisted as its own row — alerts are derived from existing document state each time this
/// endpoint is called, so an alert disappears automatically the moment its underlying condition is resolved
/// (e.g. the invoice gets generated, or gets marked paid).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IORManagerContext _context;

    public AlertsController(IORManagerContext context)
    {
        _context = context;
    }

    [HttpGet]
    public ActionResult<IReadOnlyCollection<AlertItem>> GetAlerts()
    {
        var now = DateTime.UtcNow;
        var alerts = new List<AlertItem>();

        // ── "Send invoice" alerts ───────────────────────────────────────────
        var completedPos = _context.PurchaseOrders
            .Where(po => po.Status == "Completada" && po.QuoteId != null)
            .Select(po => new { po.Id, po.Number, po.QuoteId })
            .ToList();

        if (completedPos.Count > 0)
        {
            var quoteIds = completedPos.Select(po => po.QuoteId!.Value).Distinct().ToList();
            var pendingQuotes = _context.Quotes
                .Where(q => quoteIds.Contains(q.Id) && q.ConvertedInvoiceId == null)
                .Select(q => new { q.Id, q.Number })
                .ToDictionary(q => q.Id, q => q.Number);

            foreach (var po in completedPos)
            {
                if (po.QuoteId is null || !pendingQuotes.TryGetValue(po.QuoteId.Value, out var quoteNumber))
                {
                    continue;
                }

                alerts.Add(new AlertItem(
                    $"send-invoice-{po.Id}",
                    "SendInvoice",
                    "warning",
                    $"Internal order {po.Number} is completed — send the invoice for quote {quoteNumber}.",
                    "Quote",
                    po.QuoteId.Value,
                    quoteNumber,
                    null,
                    null));
            }
        }

        // ── "Payment due" alerts ────────────────────────────────────────────
        var sentUnpaidInvoices = _context.Invoices
            .Where(i => i.SentAt != null && i.PaidAt == null)
            .Select(i => new { i.Id, i.Number, i.SentAt, i.CustomerId })
            .ToList();

        if (sentUnpaidInvoices.Count > 0)
        {
            var customerIds = sentUnpaidInvoices
                .Where(i => i.CustomerId.HasValue)
                .Select(i => i.CustomerId!.Value)
                .Distinct()
                .ToList();

            var paymentTerms = _context.Customers
                .Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.DefaultPaymentTermsDays })
                .ToDictionary(c => c.Id, c => c.DefaultPaymentTermsDays);

            foreach (var invoice in sentUnpaidInvoices)
            {
                var termsDays = invoice.CustomerId.HasValue && paymentTerms.TryGetValue(invoice.CustomerId.Value, out var days)
                    ? days
                    : 30;

                var dueDate = invoice.SentAt!.Value.AddDays(termsDays);
                if (dueDate > now)
                {
                    continue;
                }

                var invoiceNumber = DocumentNumberFormatter.ToInvoiceNumber(invoice.Number);
                var daysOverdue = (int)Math.Floor((now - dueDate).TotalDays);

                alerts.Add(new AlertItem(
                    $"payment-due-{invoice.Id}",
                    "PaymentDue",
                    daysOverdue > 0 ? "error" : "warning",
                    daysOverdue > 0
                        ? $"Invoice {invoiceNumber} payment is {daysOverdue} day(s) overdue."
                        : $"Invoice {invoiceNumber} payment is due today.",
                    "Invoice",
                    invoice.Id,
                    invoiceNumber,
                    dueDate,
                    daysOverdue));
            }
        }

        return Ok(alerts.OrderByDescending(a => a.Severity == "error").ThenBy(a => a.DueDate));
    }
}

public record AlertItem(
    string Id,
    string Type,
    string Severity,
    string Message,
    string DocumentType,
    Guid DocumentId,
    string DocumentNumber,
    DateTime? DueDate,
    int? DaysOverdue);
