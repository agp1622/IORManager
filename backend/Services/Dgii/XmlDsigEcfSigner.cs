using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace IORManager.Services.Dgii;

/// <summary>
/// Produces an enveloped XML-DSig signature (RSA-SHA256) plus a best-effort XAdES-BES
/// QualifyingProperties block (SigningTime + SigningCertificate digest), as the DGII requires e-CFs
/// to be signed with XAdES.
///
/// NOT independently verified against a DGII validator: this environment has no network access to
/// dgii.gov.do and no real DGII test certificate to submit against. The XML-DSig portion follows the
/// W3C standard exactly and is self-verifiable (see the unit tests), but the exact XAdES policy the
/// DGII enforces (which XAdES profile, whether SigningTime needs a timezone offset instead of "Z",
/// additional required qualifying properties, etc.) must be validated against a real submission in
/// the DGII's TesteCF environment before this is used in production.
/// </summary>
public class XmlDsigEcfSigner : IEcfSigner
{
    private const string XadesNamespace = "http://uri.etsi.org/01903/v1.3.2#";
    private const string DsNamespace = "http://www.w3.org/2000/09/xmldsig#";
    private const string XadesSignedPropertiesType = "http://uri.etsi.org/01903#SignedProperties";

    public EcfSignatureResult Sign(XDocument document, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(document.Root);

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("The signing certificate has no private key.");
        }

        using var rsa = certificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("The signing certificate does not use an RSA key.");

        var xmlDoc = new XmlDocument { PreserveWhitespace = true };
        using (var reader = document.CreateReader())
        {
            xmlDoc.Load(reader);
        }

        const string signatureId = "ecf-signature-1";
        const string signedPropertiesId = "ecf-signed-properties-1";
        var signingTime = DateTime.UtcNow;

        var signedXml = new EcfSignedXml(xmlDoc) { SigningKey = rsa };
        signedXml.Signature.Id = signatureId;
        // Exclusive C14N (rather than plain/inclusive C14N) so the SignedProperties digest below is
        // stable regardless of where the Signature element ends up in the document tree — with
        // inclusive C14N, canonicalizing the same element gives a *different* result once it's
        // embedded under <ds:Signature> (which declares its own xmlns:ds) than it does while detached
        // during signing, which breaks verification. This is the standard fix and matches how XAdES
        // signatures are produced in practice.
        signedXml.SignedInfo!.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
        signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        var documentReference = new Reference { Uri = "", DigestMethod = SignedXml.XmlDsigSHA256Url };
        documentReference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        documentReference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(documentReference);

        var qualifyingProperties = BuildQualifyingProperties(
            xmlDoc, certificate, signingTime, signedPropertiesId, signatureId);

        var fragment = xmlDoc.CreateDocumentFragment();
        fragment.AppendChild(qualifyingProperties);
        signedXml.AddObject(new DataObject { Data = fragment.ChildNodes });

        var propertiesReference = new Reference
        {
            Uri = "#" + signedPropertiesId,
            Type = XadesSignedPropertiesType,
            DigestMethod = SignedXml.XmlDsigSHA256Url,
        };
        propertiesReference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(propertiesReference);

        signedXml.KeyInfo = new KeyInfo();
        signedXml.KeyInfo.AddClause(new KeyInfoX509Data(certificate));

        signedXml.ComputeSignature();

        var signatureElement = signedXml.GetXml();
        xmlDoc.DocumentElement!.AppendChild(xmlDoc.ImportNode(signatureElement, true));

        var signatureValueNodes = xmlDoc.GetElementsByTagName("SignatureValue", DsNamespace);
        if (signatureValueNodes.Count == 0)
        {
            throw new InvalidOperationException("SignatureValue element not found after signing.");
        }

        var securityCode = ComputeSecurityCode(signatureValueNodes[0]!.InnerText);

        var builder = new StringBuilder();
        var writerSettings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = false,
            OmitXmlDeclaration = false,
        };
        using (var stringWriter = new Utf8StringWriter(builder))
        using (var xmlWriter = XmlWriter.Create(stringWriter, writerSettings))
        {
            xmlDoc.Save(xmlWriter);
        }

        return new EcfSignatureResult(builder.ToString(), securityCode, signingTime);
    }

    private static XmlElement BuildQualifyingProperties(
        XmlDocument document,
        X509Certificate2 certificate,
        DateTime signingTimeUtc,
        string signedPropertiesId,
        string signatureId)
    {
        var qualifyingProperties = document.CreateElement("xades", "QualifyingProperties", XadesNamespace);
        qualifyingProperties.SetAttribute("Target", "#" + signatureId);

        var signedProperties = document.CreateElement("xades", "SignedProperties", XadesNamespace);
        signedProperties.SetAttribute("Id", signedPropertiesId);
        qualifyingProperties.AppendChild(signedProperties);

        var signedSignatureProperties = document.CreateElement("xades", "SignedSignatureProperties", XadesNamespace);
        signedProperties.AppendChild(signedSignatureProperties);

        var signingTimeElement = document.CreateElement("xades", "SigningTime", XadesNamespace);
        signingTimeElement.InnerText = signingTimeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        signedSignatureProperties.AppendChild(signingTimeElement);

        var signingCertificate = document.CreateElement("xades", "SigningCertificate", XadesNamespace);
        signedSignatureProperties.AppendChild(signingCertificate);

        var cert = document.CreateElement("xades", "Cert", XadesNamespace);
        signingCertificate.AppendChild(cert);

        var certDigest = document.CreateElement("xades", "CertDigest", XadesNamespace);
        cert.AppendChild(certDigest);

        var digestMethod = document.CreateElement("ds", "DigestMethod", DsNamespace);
        digestMethod.SetAttribute("Algorithm", SignedXml.XmlDsigSHA256Url);
        certDigest.AppendChild(digestMethod);

        var digestValue = document.CreateElement("ds", "DigestValue", DsNamespace);
        digestValue.InnerText = Convert.ToBase64String(SHA256.HashData(certificate.RawData));
        certDigest.AppendChild(digestValue);

        var issuerSerial = document.CreateElement("xades", "IssuerSerial", XadesNamespace);
        cert.AppendChild(issuerSerial);

        var issuerName = document.CreateElement("ds", "X509IssuerName", DsNamespace);
        issuerName.InnerText = certificate.IssuerName.Name;
        issuerSerial.AppendChild(issuerName);

        var serialNumber = document.CreateElement("ds", "X509SerialNumber", DsNamespace);
        serialNumber.InnerText = GetSerialNumberDecimal(certificate);
        issuerSerial.AppendChild(serialNumber);

        return qualifyingProperties;
    }

    private static string GetSerialNumberDecimal(X509Certificate2 certificate)
    {
        var value = new BigInteger(certificate.SerialNumberBytes.Span, isUnsigned: true, isBigEndian: true);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string ComputeSecurityCode(string signatureValueBase64)
    {
        var compact = new string(signatureValueBase64.Where(character => !char.IsWhiteSpace(character)).ToArray());
        var code = compact.Length >= 6 ? compact[..6] : compact.PadRight(6, '0');
        return code.ToUpperInvariant();
    }

    private sealed class Utf8StringWriter(StringBuilder builder) : System.IO.StringWriter(builder)
    {
        public override Encoding Encoding => Encoding.UTF8;
    }

    /// <summary>
    /// <see cref="SignedXml.GetIdElement"/> only resolves "#id" references against nodes already
    /// attached to the target <see cref="XmlDocument"/>. The XAdES SignedProperties element added via
    /// <see cref="SignedXml.AddObject"/> only gets materialized into the document when
    /// <see cref="SignedXml.GetXml"/> runs — which is too late, since ComputeSignature needs to
    /// resolve it first to compute its digest. This override also searches the signature's own
    /// pending object list, which is the standard workaround for signing XAdES SignedProperties with
    /// .NET's SignedXml.
    /// </summary>
    private sealed class EcfSignedXml(XmlDocument document) : SignedXml(document)
    {
        public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
        {
            var fromDocument = base.GetIdElement(document, idValue);
            if (fromDocument is not null)
            {
                return fromDocument;
            }

            foreach (DataObject dataObject in Signature.ObjectList)
            {
                if (dataObject.Data is null)
                {
                    continue;
                }

                foreach (XmlNode node in dataObject.Data)
                {
                    if (node is not XmlElement element)
                    {
                        continue;
                    }

                    var match = FindById(element, idValue);
                    if (match is not null)
                    {
                        return match;
                    }
                }
            }

            return null;
        }

        private static XmlElement? FindById(XmlElement element, string idValue)
        {
            if (element.GetAttribute("Id") == idValue)
            {
                return element;
            }

            foreach (var child in element.ChildNodes.OfType<XmlElement>())
            {
                var match = FindById(child, idValue);
                if (match is not null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
