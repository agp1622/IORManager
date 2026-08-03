namespace IORManager.Services.Dgii;

/// <summary>Talks to the DGII's e-CF web services: authentication, submission, and status lookup.</summary>
public interface IDgiiEcfClient
{
    /// <summary>Requests a "semilla" (seed) XML document that must be signed and sent back to authenticate.</summary>
    Task<string> GetSemillaAsync(CancellationToken cancellationToken = default);

    /// <summary>Exchanges a signed semilla for a bearer token.</summary>
    Task<DgiiAuthToken> ValidarSemillaAsync(string signedSemillaXml, CancellationToken cancellationToken = default);

    /// <summary>Submits a signed e-CF document for validation.</summary>
    Task<EcfReceptionResult> EnviarAsync(
        string signedEcfXml,
        string rncEmisor,
        string eNcf,
        string bearerToken,
        CancellationToken cancellationToken = default);

    /// <summary>Queries the validation result for a previously submitted e-CF by its track id.</summary>
    Task<EcfStatusResult> ConsultarEstadoAsync(
        string trackId,
        string rncEmisor,
        string bearerToken,
        CancellationToken cancellationToken = default);
}

public sealed record DgiiAuthToken(string Token, DateTime? ExpiresAtUtc);

public sealed record EcfReceptionResult(string? TrackId, string? Estado, string? Codigo, string? Mensaje);

public sealed record EcfStatusResult(
    string? TrackId,
    string? Estado,
    string? Codigo,
    string? Mensaje,
    IReadOnlyList<string> Mensajes);
