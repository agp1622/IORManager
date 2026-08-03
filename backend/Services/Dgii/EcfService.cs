using IORManager.Data;
using IORManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Xml.Linq;

namespace IORManager.Services.Dgii;

public class EcfService : IEcfService
{
    private readonly IORManagerContext _context;
    private readonly IEcfXmlBuilder _xmlBuilder;
    private readonly IEcfSigner _signer;
    private readonly IEcfCertificateProvider _certificateProvider;
    private readonly IDgiiEcfClient _dgiiClient;
    private readonly DgiiOptions _options;

    public EcfService(
        IORManagerContext context,
        IEcfXmlBuilder xmlBuilder,
        IEcfSigner signer,
        IEcfCertificateProvider certificateProvider,
        IDgiiEcfClient dgiiClient,
        IOptions<DgiiOptions> options)
    {
        _context = context;
        _xmlBuilder = xmlBuilder;
        _signer = signer;
        _certificateProvider = certificateProvider;
        _dgiiClient = dgiiClient;
        _options = options.Value;
    }

    public async Task<EcfSubmission> EmitAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _context.Invoices
            .Include(existing => existing.Lines)
            .Include(existing => existing.Customer)
            .FirstOrDefaultAsync(existing => existing.Id == invoiceId, cancellationToken)
            ?? throw new InvalidOperationException($"Invoice \"{invoiceId}\" was not found.");

        if (string.IsNullOrWhiteSpace(invoice.NcfNumber) || string.IsNullOrWhiteSpace(invoice.NcfCategory))
        {
            throw new InvalidOperationException(
                "This invoice has no NCF assigned yet. Assign an electronic NCF (e.g. E31/E32) before emitting the e-CF.");
        }

        if (!NcfCategoryCatalog.TryGetByCode(invoice.NcfCategory, out var categoryDefinition) || !categoryDefinition.IsElectronic)
        {
            throw new InvalidOperationException(
                $"NCF category \"{invoice.NcfCategory}\" is not an electronic e-CF category.");
        }

        var documentTypeCode = categoryDefinition.Code[1..];
        var range = await _context.EcfRanges
            .AsNoTracking()
            .FirstOrDefaultAsync(existing => existing.DocumentTypeCode == documentTypeCode, cancellationToken);

        var certificate = _certificateProvider.GetSigningCertificate();
        var data = BuildDocumentData(invoice, documentTypeCode, range?.ExpiresAt);
        var unsignedDocument = _xmlBuilder.Build(data);
        var signature = _signer.Sign(unsignedDocument, certificate);

        var submission = await _context.EcfSubmissions
            .FirstOrDefaultAsync(existing => existing.InvoiceId == invoiceId, cancellationToken);
        if (submission is null)
        {
            submission = new EcfSubmission { Id = Guid.NewGuid(), InvoiceId = invoiceId };
            _context.EcfSubmissions.Add(submission);
        }

        submission.DocumentTypeCode = documentTypeCode;
        submission.ENcf = invoice.NcfNumber;
        submission.UnsignedXml = unsignedDocument.ToString(SaveOptions.DisableFormatting);
        submission.SignedXml = signature.SignedXml;
        submission.SecurityCode = signature.SecurityCode;
        submission.SignedAt = signature.SignedAtUtc;
        submission.Status = EcfSubmissionStatus.Firmado;
        submission.UpdatedAt = DateTime.UtcNow;

        try
        {
            var token = await AuthenticateAsync(certificate, cancellationToken);
            var result = await _dgiiClient.EnviarAsync(
                signature.SignedXml,
                _options.Emisor.Rnc,
                invoice.NcfNumber,
                token.Token,
                cancellationToken);

            submission.SubmittedAt = DateTime.UtcNow;
            submission.TrackId = result.TrackId;
            submission.Status = MapDgiiStatus(result.Estado) ?? EcfSubmissionStatus.Enviado;
            submission.ResponseMessage = result.Mensaje;
            if (submission.Status is EcfSubmissionStatus.Aceptado or EcfSubmissionStatus.Rechazado or EcfSubmissionStatus.AceptadoCondicional)
            {
                submission.RespondedAt = DateTime.UtcNow;
            }
        }
        catch (Exception ex) when (ex is DgiiEcfClientException or HttpRequestException)
        {
            submission.Status = EcfSubmissionStatus.Error;
            submission.ResponseMessage = ex.Message;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return submission;
    }

    public async Task<EcfSubmission> RefreshStatusAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var submission = await _context.EcfSubmissions
            .FirstOrDefaultAsync(existing => existing.InvoiceId == invoiceId, cancellationToken)
            ?? throw new InvalidOperationException("This invoice has no e-CF submission yet.");

        if (string.IsNullOrWhiteSpace(submission.TrackId))
        {
            throw new InvalidOperationException("This e-CF submission has no track id to query yet.");
        }

        var certificate = _certificateProvider.GetSigningCertificate();
        var token = await AuthenticateAsync(certificate, cancellationToken);
        var status = await _dgiiClient.ConsultarEstadoAsync(
            submission.TrackId,
            _options.Emisor.Rnc,
            token.Token,
            cancellationToken);

        submission.Status = MapDgiiStatus(status.Estado) ?? submission.Status;
        submission.ResponseMessage = status.Mensajes.Count > 0
            ? string.Join(" | ", status.Mensajes)
            : status.Mensaje ?? submission.ResponseMessage;
        submission.RespondedAt = DateTime.UtcNow;
        submission.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return submission;
    }

    private async Task<DgiiAuthToken> AuthenticateAsync(
        System.Security.Cryptography.X509Certificates.X509Certificate2 certificate,
        CancellationToken cancellationToken)
    {
        var semilla = await _dgiiClient.GetSemillaAsync(cancellationToken);
        var signedSemilla = _signer.Sign(XDocument.Parse(semilla), certificate);
        return await _dgiiClient.ValidarSemillaAsync(signedSemilla.SignedXml, cancellationToken);
    }

    private EcfDocumentData BuildDocumentData(Invoice invoice, string documentTypeCode, DateOnly? sequenceExpiresAt)
    {
        var (subtotal, normalizedRate, _) = invoice.CalculateFinancials();
        _ = subtotal;

        var lines = invoice.Lines
            .Select((line, index) => new EcfLineItemData(index + 1, line.Description, line.Quantity, line.UnitPrice))
            .ToList();

        return new EcfDocumentData(
            DocumentTypeCode: documentTypeCode,
            ENcf: invoice.NcfNumber!,
            FechaVencimientoSecuencia: sequenceExpiresAt,
            Emisor: new EcfEmisorData(
                _options.Emisor.Rnc,
                _options.Emisor.RazonSocial,
                _options.Emisor.NombreComercial,
                _options.Emisor.Direccion),
            Comprador: new EcfCompradorData(invoice.Customer?.Rnc, invoice.CustomerName),
            FechaEmision: invoice.InvoiceDate,
            ItbisRatePercent: normalizedRate * 100m,
            Lines: lines);
    }

    private static string? MapDgiiStatus(string? estado) => estado?.Trim().ToLowerInvariant() switch
    {
        "aceptado" => EcfSubmissionStatus.Aceptado,
        "rechazado" => EcfSubmissionStatus.Rechazado,
        "aceptadocondicional" or "aceptado condicional" or "condicional" => EcfSubmissionStatus.AceptadoCondicional,
        "enviado" or "enproceso" or "en proceso" => EcfSubmissionStatus.Enviado,
        _ => null,
    };
}
