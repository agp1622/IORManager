using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        var resources = GetInvoiceResources(invoice.CultureName);
        var currencyFormat = CreateCurrencyFormat(resources.Culture, invoice.CurrencyCode);

        var headerItems = new[]
        {
            $"{resources.DateLabel}: {invoice.Date.ToString("d", resources.Culture)}",
            $"{resources.CustomerLabel}: {invoice.CustomerName}",
            $"{resources.NumberLabel}: {invoice.Number}"
        };

        return CreateDocument(
            title: $"{resources.TitleLabel} {invoice.Number}",
            headerItems: headerItems,
            content: container => ComposeDocumentLines(
                container,
                invoice.Lines,
                invoice.TotalAmount,
                currencyFormat,
                resources.Culture,
                resources.DescriptionLabel,
                resources.QuantityLabel,
                resources.UnitLabel,
                resources.UnitPriceLabel,
                resources.LineTotalLabel,
                resources.TotalLabel),
            culture: resources.Culture,
            footerLabel: resources.FooterGeneratedLabel);
    }

    public byte[] GenerateReceiptPdf(Receipt receipt)
    {
        var culture = GetCultureOrDefault(receipt.CultureName);
        var currencyFormat = CreateCurrencyFormat(culture, receipt.CurrencyCode);

        var headerItems = new[]
        {
            $"{Localize(culture, "Date", "Fecha")}: {receipt.Date.ToString("d", culture)}",
            $"{Localize(culture, "Customer", "Cliente")}: {receipt.CustomerName}",
            $"{Localize(culture, "Reference", "Referencia")}: {receipt.ReferenceNumber ?? "-"}"
        };

        return CreateDocument(
            title: $"{Localize(culture, "Receipt", "Recibo")} {receipt.Number}",
            headerItems: headerItems,
            content: container => ComposeReceiptPayments(
                container,
                receipt.Payments,
                receipt.TotalAmount,
                currencyFormat,
                Localize(culture, "Payment Method", "Método de pago"),
                Localize(culture, "Amount", "Monto"),
                Localize(culture, "Total", "Total")),
            culture: culture,
            footerLabel: Localize(culture, "Generated on ", "Generado el "));
    }

    public byte[] GeneratePurchaseOrderPdf(PurchaseOrder purchaseOrder)
    {
        var culture = GetCultureOrDefault(purchaseOrder.CultureName);
        var currencyFormat = CreateCurrencyFormat(culture, purchaseOrder.CurrencyCode);

        var headerItems = new[]
        {
            $"{Localize(culture, "Date", "Fecha")}: {purchaseOrder.Date.ToString("d", culture)}",
            $"{Localize(culture, "Supplier", "Proveedor")}: {purchaseOrder.SupplierName}",
            $"{Localize(culture, "PO #", "OC #")}: {purchaseOrder.Number}"
        };

        return CreateDocument(
            title: $"{Localize(culture, "Purchase Order", "Orden de compra")} {purchaseOrder.Number}",
            headerItems: headerItems,
            content: container => ComposeDocumentLines(
                container,
                purchaseOrder.Lines,
                purchaseOrder.TotalAmount,
                currencyFormat,
                culture,
                Localize(culture, "Description", "Descripción"),
                Localize(culture, "Quantity", "Cantidad"),
                Localize(culture, "Unit", "Unidad"),
                Localize(culture, "Unit Price", "Precio unitario"),
                Localize(culture, "Line Total", "Subtotal"),
                Localize(culture, "Total", "Total")),
            culture: culture,
            footerLabel: Localize(culture, "Generated on ", "Generado el "));
    }

    private static byte[] CreateDocument(
        string title,
        IEnumerable<string> headerItems,
        Action<IContainer> content,
        CultureInfo? culture = null,
        string footerLabel = "Generated on ")
    {
        culture ??= CultureInfo.GetCultureInfo("en-US");

        var doc = Document.Create(document =>
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

                page.Footer()
                    .AlignCenter()
                    .DefaultTextStyle(TextStyle.Default.FontSize(9))
                    .Text(text =>
                    {
                        text.Span(footerLabel);
                        text.Span(DateTime.Now.ToString("f", culture));
                    });
            });
        });

        using var ms = new MemoryStream();
        doc.GeneratePdf(ms);
        return ms.ToArray();
    }

    private static void ComposeDocumentLines(
        IContainer container,
        IReadOnlyCollection<DocumentLine> lines,
        decimal totalAmount,
        NumberFormatInfo currencyFormat,
        CultureInfo culture,
        string descriptionLabel,
        string quantityLabel,
        string unitLabel,
        string unitPriceLabel,
        string lineTotalLabel,
        string totalLabel)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(5);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCellStyle).Text(descriptionLabel);
                header.Cell().Element(HeaderCellStyle).AlignRight().Text(quantityLabel);
                header.Cell().Element(HeaderCellStyle).AlignRight().Text(unitLabel);
                header.Cell().Element(HeaderCellStyle).AlignRight().Text(unitPriceLabel);
                header.Cell().Element(HeaderCellStyle).AlignRight().Text(lineTotalLabel);
            });

            foreach (var line in lines)
            {
                table.Cell().Element(CellStyle).Text(line.Description);
                table.Cell().Element(CellStyle).AlignRight().Text(line.Quantity.ToString("N0", currencyFormat));
                table.Cell().Element(CellStyle).AlignRight().Text(FormatUnitOfMeasure(line.UnitOfMeasure, culture));
                table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(line.UnitPrice, currencyFormat));
                table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(line.LineTotal, currencyFormat));
            }

            table.Cell().ColumnSpan(4).Element(FooterCellStyle).AlignRight().Text(totalLabel);
            table.Cell().Element(FooterCellStyle).AlignRight().Text(FormatCurrency(totalAmount, currencyFormat));
        });
    }

    private static void ComposeReceiptPayments(
        IContainer container,
        IReadOnlyCollection<ReceiptPayment> payments,
        decimal totalAmount,
        NumberFormatInfo currencyFormat,
        string methodLabel,
        string amountLabel,
        string totalLabel)
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
                header.Cell().Element(HeaderCellStyle).Text(methodLabel);
                header.Cell().Element(HeaderCellStyle).AlignRight().Text(amountLabel);
            });

            foreach (var payment in payments)
            {
                table.Cell().Element(CellStyle).Text(payment.Method);
                table.Cell().Element(CellStyle).AlignRight().Text(FormatCurrency(payment.Amount, currencyFormat));
            }

            table.Cell().Element(FooterCellStyle).AlignRight().Text(totalLabel);
            table.Cell().Element(FooterCellStyle).AlignRight().Text(FormatCurrency(totalAmount, currencyFormat));
        });
    }

    private static IContainer HeaderCellStyle(IContainer container) =>
        container.DefaultTextStyle(TextStyle.Default.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);

    private static IContainer CellStyle(IContainer container) =>
        container.PaddingVertical(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);

    private static IContainer FooterCellStyle(IContainer container) =>
        container.PaddingVertical(5).BorderTop(1).BorderColor(Colors.Grey.Lighten2).DefaultTextStyle(TextStyle.Default.SemiBold());

    private static CultureInfo GetCultureOrDefault(string? cultureName)
    {
        if (!string.IsNullOrWhiteSpace(cultureName))
        {
            try
            {
                return CultureInfo.GetCultureInfo(cultureName);
            }
            catch (CultureNotFoundException)
            {
                // Fall back to default below.
            }
        }

        return CultureInfo.GetCultureInfo("en-US");
    }

    private static NumberFormatInfo CreateCurrencyFormat(CultureInfo culture, string currencyCode)
    {
        var format = (NumberFormatInfo)culture.NumberFormat.Clone();

        format.CurrencySymbol = currencyCode switch
        {
            "USD" when IsSpanishCulture(culture) => "US$",
            "USD" => "$",
            "DOP" => "RD$",
            _ => currencyCode
        };

        return format;
    }

    private static bool IsSpanishCulture(CultureInfo culture) =>
        string.Equals(culture.TwoLetterISOLanguageName, "es", StringComparison.OrdinalIgnoreCase);

    private static string Localize(CultureInfo culture, string english, string spanish) =>
        IsSpanishCulture(culture) ? spanish : english;

    private static string FormatCurrency(decimal amount, NumberFormatInfo currencyFormat) =>
        amount.ToString("C", currencyFormat);

    private static readonly Dictionary<string, (string English, string Spanish)> UnitLabels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "unit", ("Units", "Unidades") },
            { "kg", ("Kilograms (kg)", "Kilogramos (kg)") },
            { "g", ("Grams (g)", "Gramos (g)") },
            { "t", ("Metric tons (t)", "Toneladas métricas (t)") },
            { "m", ("Meters (m)", "Metros (m)") },
            { "cm", ("Centimeters (cm)", "Centímetros (cm)") },
            { "mm", ("Millimeters (mm)", "Milímetros (mm)") },
            { "km", ("Kilometers (km)", "Kilómetros (km)") },
            { "m2", ("Square meters (m²)", "Metros cuadrados (m²)") },
            { "m3", ("Cubic meters (m³)", "Metros cúbicos (m³)") },
            { "l", ("Liters (L)", "Litros (L)") },
            { "ml", ("Milliliters (mL)", "Mililitros (mL)") },
            { "lb", ("Pounds (lb)", "Libras (lb)") },
            { "oz", ("Ounces (oz)", "Onzas (oz)") },
            { "ft", ("Feet (ft)", "Pies (ft)") },
            { "in", ("Inches (in)", "Pulgadas (in)") },
            { "yd", ("Yards (yd)", "Yardas (yd)") },
            { "gal", ("Gallons (gal)", "Galones (gal)") },
        };

    private static string FormatUnitOfMeasure(string? unitCode, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(unitCode))
        {
            return IsSpanishCulture(culture) ? "Sin especificar" : "Not specified";
        }

        if (UnitLabels.TryGetValue(unitCode, out var labels))
        {
            return IsSpanishCulture(culture) ? labels.Spanish : labels.English;
        }

        return unitCode;
    }

    private static InvoicePdfResources GetInvoiceResources(string? cultureName)
    {
        var culture = GetCultureOrDefault(cultureName);
        var isSpanish = IsSpanishCulture(culture);

        return new InvoicePdfResources(
            culture,
            isSpanish ? "Factura" : "Invoice",
            isSpanish ? "Fecha" : "Date",
            isSpanish ? "Cliente" : "Customer",
            isSpanish ? "Factura #" : "Invoice #",
            isSpanish ? "Descripción" : "Description",
            isSpanish ? "Cantidad" : "Quantity",
            isSpanish ? "Unidad" : "Unit",
            isSpanish ? "Precio unitario" : "Unit Price",
            isSpanish ? "Subtotal" : "Line Total",
            isSpanish ? "Total" : "Total",
            isSpanish ? "Generado el " : "Generated on ");
    }

    private sealed record InvoicePdfResources(
        CultureInfo Culture,
        string TitleLabel,
        string DateLabel,
        string CustomerLabel,
        string NumberLabel,
        string DescriptionLabel,
        string QuantityLabel,
        string UnitLabel,
        string UnitPriceLabel,
        string LineTotalLabel,
        string TotalLabel,
        string FooterGeneratedLabel);
}
