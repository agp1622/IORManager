using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace IORManager.Services.Dgii;

/// <summary>
/// HTTP client for the DGII's e-CF web services.
///
/// IMPORTANT: the exact endpoint paths and JSON field names below follow the patterns documented in
/// the DGII's "Descripción Técnica de Facturación Electrónica" and corroborated by third-party
/// integrations, but were NOT verified against the live DGII API or the official PDF from this
/// environment (no network access to dgii.gov.do, no DGII sandbox credentials). Before pointing this
/// at a real DGII environment: confirm the path suffixes and response field names against the
/// current official documentation and adjust the constants/parsing below — see
/// FACTURACION_ELECTRONICA.md. JSON parsing here is intentionally case-insensitive and tries a
/// couple of likely field name variants to reduce (not eliminate) the blast radius of a wrong guess.
/// </summary>
public class DgiiEcfClient : IDgiiEcfClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly DgiiOptions _options;

    public DgiiEcfClient(HttpClient httpClient, IOptions<DgiiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> GetSemillaAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.GetActiveEndpoints().AuthBaseUrl.TrimEnd('/');
        using var response = await _httpClient.GetAsync($"{baseUrl}/semilla", cancellationToken);
        await EnsureSuccessAsync(response, "obtener la semilla", cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<DgiiAuthToken> ValidarSemillaAsync(string signedSemillaXml, CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.GetActiveEndpoints().AuthBaseUrl.TrimEnd('/');
        using var content = new MultipartFormDataContent
        {
            { new StringContent(signedSemillaXml), "xml", "semilla.xml" },
        };

        using var response = await _httpClient.PostAsync($"{baseUrl}/validarsemilla", content, cancellationToken);
        await EnsureSuccessAsync(response, "validar la semilla", cancellationToken);

        using var document = await ParseJsonAsync(response, cancellationToken);
        var root = document.RootElement;
        var token = ReadString(root, "token", "Token")
            ?? throw new InvalidOperationException("The DGII response to validarsemilla did not include a token.");
        var expires = ReadDateTime(root, "expira", "Expira");
        return new DgiiAuthToken(token, expires);
    }

    public async Task<EcfReceptionResult> EnviarAsync(
        string signedEcfXml,
        string rncEmisor,
        string eNcf,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.GetActiveEndpoints().ReceptionBaseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
        {
            Content = JsonContent.Create(
                new { rncEmisor, encf = eNcf, xmlBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(signedEcfXml)) },
                options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "enviar el e-CF", cancellationToken);

        using var document = await ParseJsonAsync(response, cancellationToken);
        var root = document.RootElement;
        return new EcfReceptionResult(
            ReadString(root, "trackId", "TrackId"),
            ReadString(root, "estado", "Estado"),
            ReadString(root, "codigo", "Codigo"),
            ReadString(root, "mensaje", "Mensaje"));
    }

    public async Task<EcfStatusResult> ConsultarEstadoAsync(
        string trackId,
        string rncEmisor,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.GetActiveEndpoints().StatusBaseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/estado?rncEmisor={Uri.EscapeDataString(rncEmisor)}&trackId={Uri.EscapeDataString(trackId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "consultar el estado del e-CF", cancellationToken);

        using var document = await ParseJsonAsync(response, cancellationToken);
        var root = document.RootElement;
        var mensajes = new List<string>();
        if (root.TryGetProperty("mensajes", out var mensajesElement) && mensajesElement.ValueKind == JsonValueKind.Array)
        {
            mensajes.AddRange(mensajesElement.EnumerateArray()
                .Select(element => element.ToString())
                .Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        return new EcfStatusResult(
            ReadString(root, "trackId", "TrackId") ?? trackId,
            ReadString(root, "estado", "Estado"),
            ReadString(root, "codigo", "Codigo"),
            ReadString(root, "mensaje", "Mensaje"),
            mensajes);
    }

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new DgiiEcfClientException(
            $"La DGII respondió con un error al {action} ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
    }

    private static string? ReadString(JsonElement root, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static DateTime? ReadDateTime(JsonElement root, params string[] propertyNames)
    {
        var text = ReadString(root, propertyNames);
        return DateTime.TryParse(text, out var parsed) ? parsed : null;
    }
}

/// <summary>Thrown when the DGII's e-CF API returns an unsuccessful response.</summary>
public class DgiiEcfClientException(string message) : Exception(message);
