using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace IORManager.Services.Dgii;

public class DgiiCertificateProvider : IEcfCertificateProvider
{
    private readonly DgiiOptions _options;

    public DgiiCertificateProvider(IOptions<DgiiOptions> options)
    {
        _options = options.Value;
    }

    public X509Certificate2 GetSigningCertificate()
    {
        var path = _options.Certificate.PfxPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "No e-CF signing certificate is configured. Set Dgii:Certificate:PfxPath (and " +
                "Dgii:Certificate:PfxPassword) in appsettings to the .p12/.pfx issued by your INDOTEL-" +
                "authorized certification entity.");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"The e-CF signing certificate was not found at \"{path}\".");
        }

        return X509CertificateLoader.LoadPkcs12FromFile(
            path,
            _options.Certificate.PfxPassword,
            X509KeyStorageFlags.Exportable);
    }
}
