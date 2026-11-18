using IORManager.Services;
using IORManager.Services.Dgii;

namespace IORManager.Tests;

public class QuestPdfFinancialDocumentPdfServiceEcfTests
{
    [Fact]
    public void GenerateInvoicePdf_WithEcfPrintData_ProducesPdfWithoutThrowing()
    {
        var service = new QuestPdfFinancialDocumentPdfService();
        var invoice = TestSupport.CreateInvoice(number: "INV-000001", ncfNumber: "E310000000001", ncfCategory: "E31");
        invoice.ItbisRate = 0.18m;
        invoice.Lines.Add(new IORManager.Models.DocumentLine
        {
            Description = "Servicio de prueba",
            Quantity = 2,
            UnitPrice = 500m,
            UnitOfMeasure = "unit",
        });

        var qrGenerator = new EcfQrCodeGenerator();
        var qrData = new EcfQrCodeData(
            RncEmisor: "131586465",
            RncComprador: "101123456",
            ENcf: invoice.NcfNumber!,
            FechaEmision: invoice.Date,
            MontoTotal: 1180m,
            FechaFirmaUtc: DateTime.UtcNow,
            CodigoSeguridad: "AB12CD",
            VerificationBaseUrl: "https://ecf.dgii.gov.do/testecf/ConsultaTimbre");

        var ecfPrintData = new EcfPrintData(invoice.NcfNumber!, "AB12CD", DateTime.UtcNow, qrGenerator.GeneratePng(qrData));

        var pdfBytes = service.GenerateInvoicePdf(invoice, invoice.NcfNumber, ecfPrintData);

        Assert.NotEmpty(pdfBytes);
        // PDF signature: "%PDF"
        Assert.Equal((byte)'%', pdfBytes[0]);
        Assert.Equal((byte)'P', pdfBytes[1]);
        Assert.Equal((byte)'D', pdfBytes[2]);
        Assert.Equal((byte)'F', pdfBytes[3]);
    }

    [Fact]
    public void GenerateInvoicePdf_WithoutEcfPrintData_StillProducesPdf()
    {
        var service = new QuestPdfFinancialDocumentPdfService();
        var invoice = TestSupport.CreateInvoice(number: "INV-000002", ncfNumber: "B0200000001", ncfCategory: "B02");
        invoice.Lines.Add(new IORManager.Models.DocumentLine
        {
            Description = "Producto",
            Quantity = 1,
            UnitPrice = 100m,
            UnitOfMeasure = "unit",
        });

        var pdfBytes = service.GenerateInvoicePdf(invoice, invoice.NcfNumber);

        Assert.NotEmpty(pdfBytes);
    }
}
