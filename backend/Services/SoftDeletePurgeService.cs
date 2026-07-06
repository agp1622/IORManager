using IORManager.Data;
using IORManager.Models;
using Microsoft.EntityFrameworkCore;

namespace IORManager.Services;

/// <summary>
/// Background job that permanently removes invoices and quotes once their 1-year soft-delete
/// recovery window (<see cref="FinancialDocument.SoftDeleteRecoveryWindow"/>) has elapsed.
/// Runs once at startup and then on a daily interval — soft-deleted rows are otherwise kept
/// forever (excluded from normal queries via the global query filter), so this is what actually
/// frees the storage and finalizes the deletion.
/// </summary>
public class SoftDeletePurgeService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SoftDeletePurgeService> _logger;

    public SoftDeletePurgeService(IServiceProvider serviceProvider, ILogger<SoftDeletePurgeService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeExpiredDocumentsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to purge expired soft-deleted documents.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Shutting down — exit the loop.
            }
        }
    }

    private async Task PurgeExpiredDocumentsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IORManagerContext>();

        var cutoff = DateTime.UtcNow - FinancialDocument.SoftDeleteRecoveryWindow;

        var expiredInvoices = await context.Invoices
            .IgnoreQueryFilters()
            .Include(invoice => invoice.Lines)
            .Where(invoice => invoice.DeletedAt != null && invoice.DeletedAt < cutoff)
            .ToListAsync(stoppingToken);

        var expiredQuotes = await context.Quotes
            .IgnoreQueryFilters()
            .Include(quote => quote.Lines)
            .Include(quote => quote.Attachments)
            .Where(quote => quote.DeletedAt != null && quote.DeletedAt < cutoff)
            .ToListAsync(stoppingToken);

        if (expiredInvoices.Count == 0 && expiredQuotes.Count == 0)
        {
            return;
        }

        // Child rows (DocumentLine, QuoteAttachment) use ON DELETE NO ACTION on their foreign
        // keys, so they must be removed explicitly before their parent document.
        foreach (var invoice in expiredInvoices)
        {
            if (invoice.Lines.Count > 0)
            {
                context.DocumentLines.RemoveRange(invoice.Lines);
            }
        }

        foreach (var quote in expiredQuotes)
        {
            if (quote.Lines.Count > 0)
            {
                context.DocumentLines.RemoveRange(quote.Lines);
            }

            if (quote.Attachments.Count > 0)
            {
                context.QuoteAttachments.RemoveRange(quote.Attachments);
            }
        }

        context.Invoices.RemoveRange(expiredInvoices);
        context.Quotes.RemoveRange(expiredQuotes);
        await context.SaveChangesAsync(stoppingToken);

        _logger.LogInformation(
            "Purged {InvoiceCount} invoice(s) and {QuoteCount} quote(s) past their 1-year recovery window.",
            expiredInvoices.Count,
            expiredQuotes.Count);
    }
}
