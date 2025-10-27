using System.Globalization;
using IORManager.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IORManager.Services;

public class QuestPdfFinancialDocumentPdfService : IFinancialDocumentPdfService
{
    static QuestPdfFinancialDocumentPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateInvoicePdf(Invoice invoice)
    {
        return CreateDocument(
            title: $"Invoice {invoice.Number}",
            headerItems: new[]
            {
                $"Date: {invoice.Date:yyyy-MM-dd}",
                $"Customer: {invoice.CustomerName}",
                $"Invoice #: {invoice.Number}"
            },
            content: container => ComposeDocumentLines(container, invoice.Lines, invoice.TotalAmount));
    }

    public byte[] GenerateReceiptPdf(Receipt receipt)
    {
        return CreateDocument(
            title: $"Receipt {receipt.Number}",
            headerItems: new[]
            {
                $"Date: {receipt.Date:yyyy-MM-dd}",
                $"Customer: {receipt.CustomerName}",
                $"Reference: {receipt.ReferenceNumber ?? "-"}"
            },
            content: container => ComposeReceiptPayments(container, receipt.Payments, receipt.TotalAmount));
    }

    public byte[] GeneratePurchaseOrderPdf(PurchaseOrder purchaseOrder)
    {
        return CreateDocument(
            title: $"Purchase Order {purchaseOrder.Number}",
            headerItems: new[]
            {
                $"Date: {purchaseOrder.Date:yyyy-MM-dd}",
                $"Supplier: {purchaseOrder.SupplierName}",
                $"PO #: {purchaseOrder.Number}"
            },
            content: container => ComposeDocumentLines(container, purchaseOrder.Lines, purchaseOrder.TotalAmount));
    }

    private static byte[] CreateDocument(string title, IEnumerable<string> headerItems, Action<IContainer> content)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(TextStyle.Default.FontSize(12));

                page.Header().Column(column =>
                {
                    column.Spacing(5);
                    column.Item().Text(title).FontSize(22).SemiBold();

                    foreach (var item in headerItems)
                    {
                        column.Item().Text(item);
                    }
                });

                page.Content().PaddingTop(20).Column(column =>
                {
                    column.Spacing(15);
                    column.Item().Element(content);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated on ");
                    text.Span(DateTime.Now.ToString("u"));
                }).FontSize(9);
            });
        }).GeneratePdf();
    }

    private static void ComposeDocumentLines(IContainer container, IReadOnlyCollection<DocumentLine> lines, decimal totalAmount)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(6);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("Description");
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Quantity");
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Unit Price");
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Line Total");
            });

            foreach (var line in lines)
            {
                table.Cell().Element(CellStyle).Text(line.Description);
                table.Cell().Element(CellStyle).AlignRight().Text(line.Quantity.ToString("N0", CultureInfo.CurrentCulture));
                table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(line.UnitPrice));
                table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(line.LineTotal));
            }

            table.Cell().ColumnSpan(3).Element(FooterCellStyle).AlignRight().Text("Total");
            table.Cell().Element(FooterCellStyle).AlignRight().Text(FormatCurrency(totalAmount));
        });
    }

    private static void ComposeReceiptPayments(IContainer container, IReadOnlyCollection<ReceiptPayment> payments, decimal totalAmount)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(8);
                columns.RelativeColumn(4);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text("Payment Method");
                header.Cell().Element(HeaderCellStyle).AlignRight().Text("Amount");
            });

            foreach (var payment in payments)
            {
                table.Cell().Element(CellStyle).Text(payment.Method);
                table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(payment.Amount));
            }

            table.Cell().Element(FooterCellStyle).AlignRight().Text("Total");
            table.Cell().Element(FooterCellStyle).AlignRight().Text(FormatCurrency(totalAmount));
        });
    }

    private static IContainer HeaderCellStyle(IContainer container) =>
        container.DefaultTextStyle(TextStyle.Default.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);

    private static IContainer CellStyle(IContainer container) =>
        container.PaddingVertical(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);

    private static IContainer FooterCellStyle(IContainer container) =>
        container.PaddingVertical(5).BorderTop(1).BorderColor(Colors.Grey.Lighten2).DefaultTextStyle(TextStyle.Default.SemiBold());

    private static string FormatCurrency(decimal amount) => amount.ToString("C", CultureInfo.CurrentCulture);
}
