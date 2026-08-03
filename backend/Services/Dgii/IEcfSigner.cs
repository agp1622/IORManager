using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

namespace IORManager.Services.Dgii;

public interface IEcfSigner
{
    /// <summary>Signs the given e-CF XML with the taxpayer's certificate.</summary>
    EcfSignatureResult Sign(XDocument document, X509Certificate2 certificate);
}

/// <param name="SignedXml">The full signed XML document, serialized.</param>
/// <param name="SecurityCode">
/// The "código de seguridad" printed under the e-CF's QR code: the first 6 characters of the
/// signature value.
/// </param>
/// <param name="SignedAtUtc">Timestamp embedded in the signature's SigningTime property.</param>
public sealed record EcfSignatureResult(string SignedXml, string SecurityCode, DateTime SignedAtUtc);
