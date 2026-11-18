using System.Globalization;
using QRCoder;

namespace IORManager.Services.Dgii;

/// <summary>
/// Builds the DGII "ConsultaTimbre" verification URL and renders it as a QR code.
///
/// NOT independently verified against a real DGII environment: the parameter names
/// (RncEmisor/RncComprador/ENCF/FechaEmision/MontoTotal/FechaFirma/CodigoSeguridad) and date/amount
/// formats below follow the pattern documented informally for the e-CF verification QR, but this
/// environment had no network access to dgii.gov.do to confirm them character-for-character. Verify
/// against the official "Preguntas Frecuentes e-CF Generales" document and a real scan in TesteCF
/// before relying on this for production — see FACTURACION_ELECTRONICA.md.
/// </summary>
public class EcfQrCodeGenerator : IEcfQrCodeGenerator
{
    public string BuildVerificationUrl(EcfQrCodeData data)
    {
        var baseUrl = data.VerificationBaseUrl.TrimEnd('/');
        var parameters = new List<string> { $"RncEmisor={Uri.EscapeDataString(data.RncEmisor)}" };

        if (!string.IsNullOrWhiteSpace(data.RncComprador))
        {
            parameters.Add($"RncComprador={Uri.EscapeDataString(data.RncComprador)}");
        }

        parameters.Add($"ENCF={Uri.EscapeDataString(data.ENcf)}");
        parameters.Add($"FechaEmision={data.FechaEmision.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}");
        parameters.Add($"MontoTotal={data.MontoTotal.ToString("0.00", CultureInfo.InvariantCulture)}");
        parameters.Add($"FechaFirma={Uri.EscapeDataString(data.FechaFirmaUtc.ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture))}");
        parameters.Add($"CodigoSeguridad={Uri.EscapeDataString(data.CodigoSeguridad)}");

        return $"{baseUrl}?{string.Join("&", parameters)}";
    }

    public byte[] GeneratePng(EcfQrCodeData data)
    {
        var url = BuildVerificationUrl(data);
        using var generator = new QRCodeGenerator();
        using var qrCodeData = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var pngQrCode = new PngByteQRCode(qrCodeData);
        return pngQrCode.GetGraphic(10);
    }
}
