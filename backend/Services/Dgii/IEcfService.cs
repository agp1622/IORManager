using IORManager.Models;

namespace IORManager.Services.Dgii;

/// <summary>Orchestrates the e-CF lifecycle for an invoice: build, sign, submit, and check status.</summary>
public interface IEcfService
{
    /// <summary>
    /// Builds, signs, and submits the e-CF for the given invoice. The invoice must already have an
    /// electronic NCF (E31-E47) assigned via the normal NCF assignment flow.
    /// </summary>
    Task<EcfSubmission> EmitAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>Re-queries the DGII for the current validation status of a previously submitted e-CF.</summary>
    Task<EcfSubmission> RefreshStatusAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-attempts transmitting an already-signed e-CF that is stuck in
    /// <see cref="EcfSubmissionStatus.Contingencia"/> (e.g. after a DGII/connectivity outage), without
    /// rebuilding or re-signing it — the signed XML and security code must stay exactly as first
    /// produced. Used both by the automatic contingency retry job and for a manual "retry now".
    /// </summary>
    Task<EcfSubmission> RetrySendAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
