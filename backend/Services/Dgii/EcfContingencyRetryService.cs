using IORManager.Data;
using IORManager.Models;
using Microsoft.EntityFrameworkCore;

namespace IORManager.Services.Dgii;

/// <summary>
/// Background job implementing the DGII's "falta de conectividad" contingency rule: an e-CF that was
/// generated and signed locally but couldn't be transmitted (DGII/network unreachable) must still be
/// sent within 72 hours of the first failed attempt. This periodically retries every submission
/// stuck in <see cref="EcfSubmissionStatus.Contingencia"/> and logs loudly once one has gone past its
/// 72-hour window, since at that point it needs a human decision (DGII may require falling back to a
/// non-electronic NCF — see FACTURACION_ELECTRONICA.md).
/// </summary>
public class EcfContingencyRetryService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EcfContingencyRetryService> _logger;

    public EcfContingencyRetryService(IServiceProvider serviceProvider, ILogger<EcfContingencyRetryService> logger)
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
                await RetryPendingContingenciesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed while retrying e-CF submissions stuck in contingency.");
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

    private async Task RetryPendingContingenciesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IORManagerContext>();
        var ecfService = scope.ServiceProvider.GetRequiredService<IEcfService>();

        var pending = await context.EcfSubmissions
            .AsNoTracking()
            .Where(submission => submission.Status == EcfSubmissionStatus.Contingencia)
            .Select(submission => new { submission.InvoiceId, submission.ENcf, submission.ContingencySince })
            .ToListAsync(stoppingToken);

        foreach (var item in pending)
        {
            var overdue = item.ContingencySince.HasValue &&
                DateTime.UtcNow - item.ContingencySince.Value > EcfService.ContingencyWindow;

            try
            {
                var submission = await ecfService.RetrySendAsync(item.InvoiceId, stoppingToken);
                if (submission.Status == EcfSubmissionStatus.Contingencia)
                {
                    if (overdue)
                    {
                        _logger.LogWarning(
                            "e-CF {ENcf} (invoice {InvoiceId}) has been in contingency since {ContingencySince} — " +
                            "past the DGII's 72-hour window. Needs a manual decision (resend once connectivity is " +
                            "restored, or fall back to a non-electronic NCF per Norma General 06-2018).",
                            item.ENcf,
                            item.InvoiceId,
                            item.ContingencySince);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "e-CF {ENcf} (invoice {InvoiceId}) still could not be sent; will retry again.",
                            item.ENcf,
                            item.InvoiceId);
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "e-CF {ENcf} (invoice {InvoiceId}) left contingency with status {Status}.",
                        item.ENcf,
                        item.InvoiceId,
                        submission.Status);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to retry e-CF {ENcf} (invoice {InvoiceId}).", item.ENcf, item.InvoiceId);
            }
        }
    }
}
