using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using IORManager.Services.Dgii;

namespace IORManager.Tests;

public class XmlDsigEcfSignerTests
{
    [Fact]
    public void Sign_ProducesAVerifiableSignatureAndSecurityCode()
    {
        using var certificate = CreateSelfSignedCertificate();

        var builder = new EcfXmlBuilder();
        var data = new EcfDocumentData(
            DocumentTypeCode: "32",
            ENcf: "E320000000001",
            FechaVencimientoSecuencia: new DateOnly(2027, 12, 31),
            Emisor: new EcfEmisorData("131586465", "Papavelag Technologies & Soluciones S.R.L.", null, null),
            Comprador: new EcfCompradorData(null, "Consumidor Final"),
            FechaEmision: new DateOnly(2026, 8, 3),
            ItbisRatePercent: 18m,
            Lines: [new EcfLineItemData(1, "Producto", 1m, 100m)]);

        var document = builder.Build(data);

        var signer = new XmlDsigEcfSigner();
        var result = signer.Sign(document, certificate);

        Assert.False(string.IsNullOrWhiteSpace(result.SignedXml));
        Assert.Equal(6, result.SecurityCode.Length);
        Assert.Equal(result.SecurityCode, result.SecurityCode.ToUpperInvariant());

        var xmlDoc = new XmlDocument { PreserveWhitespace = true };
        xmlDoc.LoadXml(result.SignedXml);

        var signatureNode = xmlDoc.GetElementsByTagName("Signature", "http://www.w3.org/2000/09/xmldsig#")[0];
        Assert.NotNull(signatureNode);

        var signedXml = new SignedXml(xmlDoc);
        signedXml.LoadXml((XmlElement)signatureNode!);
        Assert.True(signedXml.CheckSignature(certificate, verifySignatureOnly: true));

        // The security code is derived from the raw SignatureValue text.
        var signatureValueNode = xmlDoc.GetElementsByTagName("SignatureValue", "http://www.w3.org/2000/09/xmldsig#")[0]!;
        var expectedCode = signatureValueNode.InnerText.Trim()[..6].ToUpperInvariant();
        Assert.Equal(expectedCode, result.SecurityCode);
    }

    [Fact]
    public void Sign_ThrowsWhenCertificateHasNoPrivateKey()
    {
        using var withKey = CreateSelfSignedCertificate();
        using var publicOnly = X509CertificateLoader.LoadCertificate(withKey.Export(X509ContentType.Cert));

        var builder = new EcfXmlBuilder();
        var data = new EcfDocumentData(
            "32",
            "E320000000001",
            null,
            new EcfEmisorData("131586465", "Papavelag", null, null),
            new EcfCompradorData(null, "Consumidor Final"),
            new DateOnly(2026, 8, 3),
            18m,
            [new EcfLineItemData(1, "Producto", 1m, 100m)]);

        var document = builder.Build(data);
        var signer = new XmlDsigEcfSigner();

        Assert.Throws<InvalidOperationException>(() => signer.Sign(document, publicOnly));
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Test e-CF Signer, O=Papavelag, C=DO",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        // Round-trip through PFX so the private key is in an exportable, non-ephemeral form.
        var pfxBytes = certificate.Export(X509ContentType.Pfx, "test-password");
        certificate.Dispose();
        return X509CertificateLoader.LoadPkcs12(pfxBytes, "test-password", X509KeyStorageFlags.Exportable);
    }
}
