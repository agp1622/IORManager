using System.Security.Cryptography.X509Certificates;

namespace IORManager.Services.Dgii;

public interface IEcfCertificateProvider
{
    /// <summary>Loads the taxpayer's e-CF signing certificate (with its private key).</summary>
    X509Certificate2 GetSigningCertificate();
}
