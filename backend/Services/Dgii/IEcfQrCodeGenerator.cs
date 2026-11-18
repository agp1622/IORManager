namespace IORManager.Services.Dgii;

/// <summary>Builds the DGII's e-CF verification URL and renders it as a QR code.</summary>
public interface IEcfQrCodeGenerator
{
    /// <summary>
    /// Builds the "ConsultaTimbre" verification URL that must be encoded in the QR code printed on
    /// the e-CF's Representación Impresa (RI).
    /// </summary>
    string BuildVerificationUrl(EcfQrCodeData data);

    /// <summary>Renders the verification URL as a PNG QR code image.</summary>
    byte[] GeneratePng(EcfQrCodeData data);
}

/// <param name="RncEmisor">RNC of the taxpayer who issued the e-CF.</param>
/// <param name="RncComprador">RNC/Cédula of the buyer, when known. Omitted from the URL if null/blank.</param>
/// <param name="ENcf">The e-NCF (e.g. "E310000000001").</param>
/// <param name="FechaEmision">The e-CF's issue date.</param>
/// <param name="MontoTotal">The e-CF's total amount.</param>
/// <param name="FechaFirmaUtc">When the e-CF's XML was digitally signed.</param>
/// <param name="CodigoSeguridad">The 6-character security code from the digital signature.</param>
/// <param name="VerificationBaseUrl">
/// The DGII's "ConsultaTimbre" base URL for the active environment (see
/// <see cref="DgiiEnvironmentEndpoints.VerificationBaseUrl"/>).
/// </param>
public sealed record EcfQrCodeData(
    string RncEmisor,
    string? RncComprador,
    string ENcf,
    DateOnly FechaEmision,
    decimal MontoTotal,
    DateTime FechaFirmaUtc,
    string CodigoSeguridad,
    string VerificationBaseUrl);
