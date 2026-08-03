using System.Globalization;
using System.Xml.Linq;

namespace IORManager.Services.Dgii;

/// <summary>
/// Builds the e-CF XML per the DGII's "Formato Comprobante Fiscal Electrónico (e-CF)" schema.
///
/// IMPORTANT — read before sending anything to a real DGII environment:
/// The element and attribute names below (ECF, Encabezado/IdDoc/Emisor/Comprador/Totales,
/// DetallesItems/Item, InformacionReferencia, NCFModificado, CodigoModificacion) are corroborated by
/// the DGII's official "Informe Técnico e-CF v1.0" and "Formato Comprobante Fiscal Electrónico (e-CF)
/// v1.0" documents. What is NOT independently verified against the XSD from this environment (no
/// network access to dgii.gov.do) is the *complete* field catalog per document type (in particular
/// item-level indicator codes, unit-of-measure codes, and the extra sections some document types
/// require, e.g. E41 compras, E46 exportaciones, E47 pagos al exterior). Before issuing real e-CFs,
/// download the current XSD/PDF from
/// https://dgii.gov.do/cicloContribuyente/facturacion/comprobantesFiscalesElectronicosE-CF and
/// reconcile field-by-field — see FACTURACION_ELECTRONICA.md.
/// </summary>
public class EcfXmlBuilder : IEcfXmlBuilder
{
    private const string DateFormat = "dd-MM-yyyy";

    public XDocument Build(EcfDocumentData data)
    {
        var lines = data.Lines;
        if (lines.Count == 0)
        {
            throw new ArgumentException("An e-CF requires at least one line item.", nameof(data));
        }

        var lineAmounts = lines
            .Select(line => (line, amount: Math.Round(line.Cantidad * line.PrecioUnitario, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        var subtotal = lineAmounts.Sum(entry => entry.amount);
        var itbisAmount = Math.Round(subtotal * data.ItbisRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + itbisAmount;

        var encabezado = new XElement("Encabezado",
            new XAttribute("Version", "1.0"),
            BuildIdDoc(data),
            BuildEmisor(data),
            BuildComprador(data),
            BuildTotales(subtotal, itbisAmount, total, data.ItbisRatePercent));

        var detallesItems = new XElement("DetallesItems",
            lineAmounts.Select(entry => BuildItem(entry.line, entry.amount)));

        var root = new XElement("ECF", encabezado, detallesItems);

        if (data.Referencia is not null)
        {
            root.Add(BuildInformacionReferencia(data.Referencia));
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private static XElement BuildIdDoc(EcfDocumentData data)
    {
        var idDoc = new XElement("IdDoc",
            new XElement("TipoeCF", data.DocumentTypeCode),
            new XElement("eNCF", data.ENcf));

        if (data.FechaVencimientoSecuencia.HasValue)
        {
            idDoc.Add(new XElement(
                "FechaVencimientoSecuencia",
                data.FechaVencimientoSecuencia.Value.ToString(DateFormat, CultureInfo.InvariantCulture)));
        }

        return idDoc;
    }

    private static XElement BuildEmisor(EcfDocumentData data)
    {
        var emisor = new XElement("Emisor",
            new XElement("RNCEmisor", data.Emisor.Rnc),
            new XElement("RazonSocialEmisor", data.Emisor.RazonSocial));

        if (!string.IsNullOrWhiteSpace(data.Emisor.NombreComercial))
        {
            emisor.Add(new XElement("NombreComercial", data.Emisor.NombreComercial));
        }

        if (!string.IsNullOrWhiteSpace(data.Emisor.Direccion))
        {
            emisor.Add(new XElement("DireccionEmisor", data.Emisor.Direccion));
        }

        emisor.Add(new XElement("FechaEmision", data.FechaEmision.ToString(DateFormat, CultureInfo.InvariantCulture)));

        return emisor;
    }

    private static XElement BuildComprador(EcfDocumentData data)
    {
        var comprador = new XElement("Comprador");

        if (!string.IsNullOrWhiteSpace(data.Comprador.Rnc))
        {
            comprador.Add(new XElement("RNCComprador", data.Comprador.Rnc));
        }

        comprador.Add(new XElement("RazonSocialComprador", data.Comprador.RazonSocial));
        return comprador;
    }

    private static XElement BuildTotales(decimal subtotal, decimal itbisAmount, decimal total, decimal itbisRatePercent) =>
        new("Totales",
            new XElement("MontoGravadoTotal", FormatAmount(subtotal)),
            new XElement("MontoGravadoI1", FormatAmount(subtotal)),
            new XElement("ITBIS1", FormatAmount(itbisRatePercent)),
            new XElement("TotalITBIS", FormatAmount(itbisAmount)),
            new XElement("TotalITBIS1", FormatAmount(itbisAmount)),
            new XElement("MontoTotal", FormatAmount(total)));

    private static XElement BuildItem(EcfLineItemData line, decimal amount) =>
        new("Item",
            new XElement("NumeroLinea", line.NumeroLinea),
            // Assumption: every line is taxed at the invoice's single ITBIS rate ("1" = gravado tasa
            // normal). This system doesn't support mixed per-line tax treatment today; if that's
            // needed, this indicator must vary per line — verify the code catalog against the XSD.
            new XElement("IndicadorFacturacion", "1"),
            new XElement("NombreItem", line.NombreItem),
            new XElement("CantidadItem", FormatQuantity(line.Cantidad)),
            new XElement("PrecioUnitarioItem", FormatAmount(line.PrecioUnitario)),
            new XElement("MontoItem", FormatAmount(amount)));

    private static XElement BuildInformacionReferencia(EcfReferenciaData referencia) =>
        new("InformacionReferencia",
            new XElement("NCFModificado", referencia.NcfModificado),
            new XElement("CodigoModificacion", referencia.CodigoModificacion));

    private static string FormatAmount(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatQuantity(decimal value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);
}
