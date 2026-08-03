namespace IORManager.Services.Dgii;

/// <summary>
/// Configuration for the DGII e-CF integration, bound from the "Dgii" section of appsettings.
/// </summary>
public class DgiiOptions
{
    public const string SectionName = "Dgii";

    /// <summary>Which DGII environment to talk to: "TesteCF", "CerteCF", or "Produccion".</summary>
    public string Environment { get; set; } = DgiiEnvironments.TesteCF;

    public DgiiEmisorOptions Emisor { get; set; } = new();

    public DgiiCertificateOptions Certificate { get; set; } = new();

    /// <summary>Base URLs per environment, keyed by the environment name (see <see cref="DgiiEnvironments"/>).</summary>
    public Dictionary<string, DgiiEnvironmentEndpoints> Endpoints { get; set; } = new();

    public DgiiEnvironmentEndpoints GetActiveEndpoints()
    {
        if (Endpoints.TryGetValue(Environment, out var endpoints))
        {
            return endpoints;
        }

        throw new InvalidOperationException(
            $"No DGII endpoints configured for environment \"{Environment}\". " +
            "Add a Dgii:Endpoints:{Environment} section to appsettings.");
    }
}

public static class DgiiEnvironments
{
    public const string TesteCF = "TesteCF";
    public const string CerteCF = "CerteCF";
    public const string Produccion = "Produccion";
}

public class DgiiEmisorOptions
{
    /// <summary>RNC of the taxpayer issuing e-CFs.</summary>
    public string Rnc { get; set; } = string.Empty;

    public string RazonSocial { get; set; } = string.Empty;

    public string? NombreComercial { get; set; }

    public string? Direccion { get; set; }
}

public class DgiiCertificateOptions
{
    /// <summary>Filesystem path to the .p12/.pfx digital certificate used to sign e-CFs.</summary>
    public string? PfxPath { get; set; }

    public string? PfxPassword { get; set; }
}

/// <summary>
/// Base URLs for one DGII environment. Endpoint URL patterns per DGII's published e-CF technical
/// documentation ("Descripción Técnica de Facturación Electrónica"); verify the exact paths against
/// the current official PDF before going live — see FACTURACION_ELECTRONICA.md.
/// </summary>
public class DgiiEnvironmentEndpoints
{
    /// <summary>Base URL for Semilla / ValidarSemilla (authentication).</summary>
    public string AuthBaseUrl { get; set; } = string.Empty;

    /// <summary>Base URL for submitting e-CF documents (Recepción).</summary>
    public string ReceptionBaseUrl { get; set; } = string.Empty;

    /// <summary>Base URL for querying submission status (ConsultaResultado).</summary>
    public string StatusBaseUrl { get; set; } = string.Empty;
}
