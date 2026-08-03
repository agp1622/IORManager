using IORManager.Services.Dgii;

namespace IORManager.Tests;

public class EcfXmlBuilderTests
{
    private static EcfDocumentData CreateData(EcfReferenciaData? referencia = null) => new(
        DocumentTypeCode: "31",
        ENcf: "E310000000001",
        FechaVencimientoSecuencia: new DateOnly(2027, 12, 31),
        Emisor: new EcfEmisorData("131586465", "Papavelag Technologies & Soluciones S.R.L.", "Papavelag", "Calle Principal 1"),
        Comprador: new EcfCompradorData("101123456", "Cliente de Prueba SRL"),
        FechaEmision: new DateOnly(2026, 8, 3),
        ItbisRatePercent: 18m,
        Lines:
        [
            new EcfLineItemData(1, "Servicio de consultoría", 2m, 500m),
            new EcfLineItemData(2, "Licencia de software", 1m, 1000m),
        ],
        Referencia: referencia);

    [Fact]
    public void Build_ProducesExpectedStructureAndTotals()
    {
        var builder = new EcfXmlBuilder();
        var document = builder.Build(CreateData());
        var root = document.Root!;

        Assert.Equal("ECF", root.Name.LocalName);

        var encabezado = root.Element("Encabezado")!;
        Assert.Equal("1.0", encabezado.Attribute("Version")?.Value);
        Assert.Equal("31", encabezado.Element("IdDoc")!.Element("TipoeCF")!.Value);
        Assert.Equal("E310000000001", encabezado.Element("IdDoc")!.Element("eNCF")!.Value);
        Assert.Equal("31-12-2027", encabezado.Element("IdDoc")!.Element("FechaVencimientoSecuencia")!.Value);

        var emisor = encabezado.Element("Emisor")!;
        Assert.Equal("131586465", emisor.Element("RNCEmisor")!.Value);
        Assert.Equal("03-08-2026", emisor.Element("FechaEmision")!.Value);

        var comprador = encabezado.Element("Comprador")!;
        Assert.Equal("101123456", comprador.Element("RNCComprador")!.Value);

        // Subtotal = 2*500 + 1*1000 = 2000; ITBIS 18% = 360; total = 2360
        var totales = encabezado.Element("Totales")!;
        Assert.Equal("2000.00", totales.Element("MontoGravadoTotal")!.Value);
        Assert.Equal("360.00", totales.Element("TotalITBIS")!.Value);
        Assert.Equal("2360.00", totales.Element("MontoTotal")!.Value);

        var items = root.Element("DetallesItems")!.Elements("Item").ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("1000.00", items[0].Element("MontoItem")!.Value);
        Assert.Equal("1000.00", items[1].Element("MontoItem")!.Value);

        Assert.Null(root.Element("InformacionReferencia"));
    }

    [Fact]
    public void Build_IncludesInformacionReferenciaForNotes()
    {
        var builder = new EcfXmlBuilder();
        var document = builder.Build(CreateData(new EcfReferenciaData("E310000000009", 1)));

        var referencia = document.Root!.Element("InformacionReferencia");
        Assert.NotNull(referencia);
        Assert.Equal("E310000000009", referencia!.Element("NCFModificado")!.Value);
        Assert.Equal("1", referencia.Element("CodigoModificacion")!.Value);
    }

    [Fact]
    public void Build_ThrowsWhenNoLines()
    {
        var builder = new EcfXmlBuilder();
        var data = CreateData() with { Lines = [] };

        Assert.Throws<ArgumentException>(() => builder.Build(data));
    }
}
