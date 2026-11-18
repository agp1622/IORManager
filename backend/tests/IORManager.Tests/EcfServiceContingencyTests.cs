using IORManager.Models;
using IORManager.Services.Dgii;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace IORManager.Tests;

public class EcfServiceContingencyTests
{
    [Fact]
    public async Task EmitAsync_MarksContingency_WhenTransmissionFails()
    {
        using var scope = new TestScope();
        var client = new FakeDgiiEcfClient { FailEnvios = int.MaxValue };
        var service = scope.CreateService(client);
        var invoice = scope.AddElectronicInvoice();

        var submission = await service.EmitAsync(invoice.Id);

        Assert.Equal(EcfSubmissionStatus.Contingencia, submission.Status);
        Assert.NotNull(submission.ContingencySince);
        Assert.NotNull(submission.SignedXml);
        Assert.NotNull(submission.SecurityCode);
    }

    [Fact]
    public async Task RetrySendAsync_KeepsOriginalSignatureAndSecurityCode()
    {
        using var scope = new TestScope();
        var client = new FakeDgiiEcfClient { FailEnvios = int.MaxValue };
        var service = scope.CreateService(client);
        var invoice = scope.AddElectronicInvoice();

        var firstAttempt = await service.EmitAsync(invoice.Id);
        var signedXmlAfterFirstAttempt = firstAttempt.SignedXml;
        var securityCodeAfterFirstAttempt = firstAttempt.SecurityCode;

        client.FailEnvios = 0; // Connectivity is restored — EnviarAsync should succeed now.
        var retried = await service.RetrySendAsync(invoice.Id);

        Assert.Equal(EcfSubmissionStatus.Enviado, retried.Status);
        Assert.Null(retried.ContingencySince);
        Assert.Equal(signedXmlAfterFirstAttempt, retried.SignedXml);
        Assert.Equal(securityCodeAfterFirstAttempt, retried.SecurityCode);
        Assert.Equal("track-123", retried.TrackId);
    }

    [Fact]
    public async Task RetrySendAsync_ThrowsWhenNothingWasSignedYet()
    {
        using var scope = new TestScope();
        var client = new FakeDgiiEcfClient();
        var service = scope.CreateService(client);
        var invoice = scope.AddElectronicInvoice();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetrySendAsync(invoice.Id));
    }

    private sealed class TestScope : IDisposable
    {
        public TestScope()
        {
            Context = TestSupport.CreateContext();
        }

        public IORManager.Data.IORManagerContext Context { get; }

        public EcfService CreateService(IDgiiEcfClient client)
        {
            var options = Options.Create(new DgiiOptions
            {
                Emisor = new DgiiEmisorOptions
                {
                    Rnc = "131586465",
                    RazonSocial = "Papavelag Technologies & Soluciones S.R.L.",
                },
            });

            return new EcfService(
                Context,
                new EcfXmlBuilder(),
                new XmlDsigEcfSigner(),
                new FakeCertificateProvider(),
                client,
                options);
        }

        public Invoice AddElectronicInvoice()
        {
            var invoice = TestSupport.CreateInvoice(
                number: "INV-000001",
                ncfNumber: "E310000000001",
                ncfCategory: "E31");
            invoice.ItbisRate = 0.18m;
            invoice.Lines.Add(new DocumentLine
            {
                Description = "Servicio de prueba",
                Quantity = 1,
                UnitPrice = 100m,
                UnitOfMeasure = "unit",
            });

            Context.Invoices.Add(invoice);
            Context.SaveChanges();
            return invoice;
        }

        public void Dispose() => Context.Dispose();
    }

    private sealed class FakeCertificateProvider : IEcfCertificateProvider
    {
        public X509Certificate2 GetSigningCertificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=Test e-CF Signer",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            return X509CertificateLoader.LoadPkcs12(
                certificate.Export(X509ContentType.Pfx, "pw"),
                "pw",
                X509KeyStorageFlags.Exportable);
        }
    }

    /// <summary>Fake DGII client: Semilla/ValidarSemilla always succeed; EnviarAsync fails the next <see cref="FailEnvios"/> calls.</summary>
    private sealed class FakeDgiiEcfClient : IDgiiEcfClient
    {
        public int FailEnvios { get; set; }

        public Task<string> GetSemillaAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("<Semilla><Valor>123</Valor></Semilla>");

        public Task<DgiiAuthToken> ValidarSemillaAsync(string signedSemillaXml, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DgiiAuthToken("fake-token", DateTime.UtcNow.AddHours(1)));

        public Task<EcfReceptionResult> EnviarAsync(
            string signedEcfXml,
            string rncEmisor,
            string eNcf,
            string bearerToken,
            CancellationToken cancellationToken = default)
        {
            if (FailEnvios > 0)
            {
                FailEnvios--;
                throw new DgiiEcfClientException("Simulated DGII outage.");
            }

            return Task.FromResult(new EcfReceptionResult("track-123", "Enviado", "0", "Recibido"));
        }

        public Task<EcfStatusResult> ConsultarEstadoAsync(
            string trackId,
            string rncEmisor,
            string bearerToken,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
