using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IORManager.Models;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IORManager.Services;

public class QuestPdfFinancialDocumentPdfService : IFinancialDocumentPdfService
{
    private const string PageBackgroundColor = "#f4f5f8";
    private const string CardBackgroundColor = "#ffffff";
    private const string CardBorderColor = "#d7dce5";
    private const string HeaderBackgroundColor = "#ffffff";
    private const string HeaderBorderColor = "#cfd5e1";
    private const string TableHeaderBackground = "#171f2c";
    private const string TableBorderColor = "#e1e4ec";
    private const string PrimaryTextColor = "#101828";
    private const string SecondaryTextColor = "#4b5565";
    private const int MinimumLineRows = 6;

    private const string CompanyLegalName = "Papavelag Technologies & Soluciones S.R.L.";
    private const string CompanySecondaryName = "Papavelag Technologies & Solutions S.R.L.";

    private static readonly IReadOnlyList<string> CompanyInformationLines = new[]
    {
        "Calle Jose Fco Peña Gomez #19, Manzana 4715, dif. 1 Apto. 3D",
        "Phone: 809-982-4449",
        "RNC 131586465",
        "ariaspavel2@gmail.com"
    };

    private static readonly Lazy<Image?> BrandRasterLogo = new(LoadRasterLogo);
    private static readonly Lazy<SvgImage?> BrandVectorLogo = new(LoadVectorLogo);

    static QuestPdfFinancialDocumentPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateQuotePdf(Invoice invoice) =>
        GenerateInvoiceDocument(invoice, isQuote: true);

    public byte[] GenerateInvoicePdf(Invoice invoice) =>
        GenerateInvoiceDocument(invoice, isQuote: false);

    private byte[] GenerateInvoiceDocument(Invoice invoice, bool isQuote)
    {
        var resources = GetInvoiceResources(invoice.CultureName, isQuote);
        var culture = resources.Culture;
        var currencyFormat = CreateCurrencyFormat(culture, invoice.CurrencyCode);
        var metadata = new List<(string Label, string Value)>
        {
            (resources.NumberLabel, invoice.Number),
            (resources.DateLabel, invoice.Date.ToString("d", culture))
        };

        var partyDetails = new List<(string Label, string Value)>
        {
            (Localize(culture, "Address", "Dirección"), GetPlaceholderValue(culture)),
            (Localize(culture, "Phone", "Teléfono"), GetPlaceholderValue(culture))
        };

        var subtotalLabel = isQuote
            ? Localize(culture, "Quote Subtotal", "Subtotal de cotización")
            : Localize(culture, "Invoice Subtotal", "Subtotal de factura");

        var totals = new List<(string Label, string Value)>
        {
            (subtotalLabel, FormatCurrency(invoice.TotalAmount, currencyFormat)),
            (Localize(culture, "Tax Rate", "Tasa de impuesto"), "0.00%"),
            (Localize(culture, "Sales Tax", "Impuesto"), FormatCurrency(0, currencyFormat)),
            (Localize(culture, "Deposit Received", "Depósito recibido"), FormatCurrency(0, currencyFormat)),
            ($"{resources.TotalLabel.ToUpperInvariant()} ({currencyFormat.CurrencySymbol})", FormatCurrency(invoice.TotalAmount, currencyFormat))
        };

        return CreateDocument(
            documentTypeLabel: resources.TitleLabel.ToUpperInvariant(),
            metadata: metadata,
            partyHeading: Localize(culture, "Bill to", "Facturar a"),
            partyPrimaryValue: invoice.CustomerName,
            partyDetails: partyDetails,
            content: container => ComposeDocumentLines(
                container,
                invoice.Lines,
                currencyFormat,
                culture,
                Localize(culture, "Item #", "Ítem #"),
                resources.DescriptionLabel,
                resources.QuantityLabel,
                resources.UnitPriceLabel,
                Localize(culture, "Discount", "Descuento"),
                Localize(culture, "Price", "Precio")),
            totals: totals,
            footerNotes: GetFooterNotes(culture),
            culture: culture);
    }

    public byte[] GenerateReceiptPdf(Receipt receipt)
    {
        var culture = GetCultureOrDefault(receipt.CultureName);
        var currencyFormat = CreateCurrencyFormat(culture, receipt.CurrencyCode);
        var metadata = new List<(string Label, string Value)>
        {
            (Localize(culture, "Receipt #", "Recibo #"), receipt.Number),
            (Localize(culture, "Date", "Fecha"), receipt.Date.ToString("d", culture)),
            (Localize(culture, "Reference", "Referencia"), receipt.ReferenceNumber ?? GetPlaceholderValue(culture))
        };

        var partyDetails = new List<(string Label, string Value)>
        {
            (Localize(culture, "Currency", "Moneda"), receipt.CurrencyCode),
            (Localize(culture, "Customer Email", "Correo del cliente"), GetPlaceholderValue(culture))
        };

        var totals = new List<(string Label, string Value)>
        {
            (Localize(culture, "Total received", "Total recibido"), FormatCurrency(receipt.TotalAmount, currencyFormat))
        };

        return CreateDocument(
            documentTypeLabel: Localize(culture, "Receipt", "Recibo").ToUpperInvariant(),
            metadata: metadata,
            partyHeading: Localize(culture, "Customer", "Cliente"),
            partyPrimaryValue: receipt.CustomerName,
            partyDetails: partyDetails,
            content: container => ComposeReceiptPayments(
                container,
                receipt.Payments,
                currencyFormat,
                Localize(culture, "Payment Method", "Método de pago"),
                Localize(culture, "Amount", "Monto")),
            totals: totals,
            footerNotes: GetFooterNotes(culture),
            culture: culture);
    }

    public byte[] GeneratePurchaseOrderPdf(PurchaseOrder purchaseOrder)
    {
        var culture = GetCultureOrDefault(purchaseOrder.CultureName);
        var currencyFormat = CreateCurrencyFormat(culture, purchaseOrder.CurrencyCode);
        var metadata = new List<(string Label, string Value)>
        {
            (Localize(culture, "PO #", "OC #"), purchaseOrder.Number),
            (Localize(culture, "Date", "Fecha"), purchaseOrder.Date.ToString("d", culture))
        };

        var partyDetails = new List<(string Label, string Value)>
        {
            (Localize(culture, "Address", "Dirección"), GetPlaceholderValue(culture)),
            (Localize(culture, "Phone", "Teléfono"), GetPlaceholderValue(culture))
        };

        var totals = new List<(string Label, string Value)>
        {
            (Localize(culture, "Order Subtotal", "Subtotal"), FormatCurrency(purchaseOrder.TotalAmount, currencyFormat)),
            ($"{Localize(culture, "Total", "Total").ToUpperInvariant()} ({currencyFormat.CurrencySymbol})", FormatCurrency(purchaseOrder.TotalAmount, currencyFormat))
        };

        return CreateDocument(
            documentTypeLabel: Localize(culture, "Purchase Order", "Orden de compra").ToUpperInvariant(),
            metadata: metadata,
            partyHeading: Localize(culture, "Supplier", "Proveedor"),
            partyPrimaryValue: purchaseOrder.SupplierName,
            partyDetails: partyDetails,
            content: container => ComposeDocumentLines(
                container,
                purchaseOrder.Lines,
                currencyFormat,
                culture,
                Localize(culture, "Item #", "Ítem #"),
                Localize(culture, "Description", "Descripción"),
                Localize(culture, "Quantity", "Cantidad"),
                Localize(culture, "Unit Price", "Precio unitario"),
                Localize(culture, "Discount", "Descuento"),
                Localize(culture, "Price", "Precio")),
            totals: totals,
            footerNotes: GetFooterNotes(culture),
            culture: culture);
    }

    private static byte[] CreateDocument(
        string documentTypeLabel,
        IReadOnlyCollection<(string Label, string Value)> metadata,
        string partyHeading,
        string partyPrimaryValue,
        IReadOnlyCollection<(string Label, string Value)> partyDetails,
        Action<IContainer> content,
        IReadOnlyCollection<(string Label, string Value)> totals,
        IReadOnlyCollection<string> footerNotes,
        CultureInfo culture)
    {
        metadata ??= Array.Empty<(string, string)>();
        partyDetails ??= Array.Empty<(string, string)>();
        totals ??= Array.Empty<(string, string)>();
        footerNotes ??= Array.Empty<string>();
        partyPrimaryValue ??= GetPlaceholderValue(culture);

        var doc = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.PageColor(PageBackgroundColor);
                page.DefaultTextStyle(
                    TextStyle.Default
                        .FontFamily("Helvetica")
                        .FontSize(10.5f)
                        .FontColor(PrimaryTextColor));

                page.Content().Column(column =>
                {
                    column.Spacing(18);
                    column.Item().Element(header =>
                        ComposeHeader(header, documentTypeLabel, metadata));
                    column.Item().Element(info =>
                        ComposeDetailsRow(info, partyHeading, partyPrimaryValue, partyDetails));
                    column.Item().Element(body =>
                        ComposeContentCard(body, content));
                    if (totals.Count > 0)
                    {
                        column.Item().Element(totalContainer => ComposeTotals(totalContainer, totals));
                    }

                });

                page.Footer().Element(footerContainer =>
                {
                    if (footerNotes.Count > 0)
                    {
                        ComposeFooterNotes(footerContainer, footerNotes);
                    }
                });
            });
        });

        using var ms = new MemoryStream();
        doc.GeneratePdf(ms);
        return ms.ToArray();
    }

    private static void ComposeHeader(
        IContainer container,
        string documentTypeLabel,
        IReadOnlyCollection<(string Label, string Value)> metadata)
    {
        var rasterLogo = BrandRasterLogo.Value;
        var vectorLogo = BrandVectorLogo.Value;

        container
            .Background(HeaderBackgroundColor)
            .Border(1)
            .BorderColor(HeaderBorderColor)
            .Padding(20)
            .Row(row =>
            {
                row.RelativeItem().Row(brandRow =>
                {
                    brandRow.ConstantItem(130).Height(130).Element(logoContainer =>
                    {
                        if (rasterLogo is not null)
                        {
                            logoContainer.Image(rasterLogo).FitHeight();
                        }
                        else if (vectorLogo is not null)
                        {
                            logoContainer.Svg(vectorLogo);
                        }
                        else
                        {
                            logoContainer
                                .Background("#ff7b2b")
                                .AlignCenter()
                                .AlignMiddle()
                                .Text("PV")
                                .FontSize(28)
                                .SemiBold()
                                .FontColor("#ffffff");
                        }
                    });

                    brandRow.RelativeItem().Column(col =>
                    {
                        col.Item().Text(CompanyLegalName).FontSize(16).SemiBold();
                        col.Item().Text(CompanySecondaryName).FontColor(SecondaryTextColor);
                    });
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Spacing(4);
                    col.Item()
                        .AlignRight()
                        .Text(documentTypeLabel)
                        .FontSize(24)
                        .SemiBold();

                    foreach (var entry in metadata.Where(item => !string.IsNullOrWhiteSpace(item.Value)))
                    {
                        col.Item().AlignRight().Text(text =>
                        {
                            text.Span($"{entry.Label}: ").SemiBold();
                            text.Span(entry.Value);
                        });
                    }
                });
            });
    }

    private static void ComposeDetailsRow(
        IContainer container,
        string partyHeading,
        string partyPrimaryValue,
        IReadOnlyCollection<(string Label, string Value)> partyDetails)
    {
        container.Row(row =>
        {
            row.RelativeItem(0.55f).Element(company =>
                ComposeCompanyDetails(company));
            row.RelativeItem(0.45f).Element(party =>
                ComposePartyDetails(party, partyHeading, partyPrimaryValue, partyDetails));
        });
    }

    private static void ComposeCompanyDetails(IContainer container)
    {
        container
            .Background(CardBackgroundColor)
            .Border(1)
            .BorderColor(CardBorderColor)
            .Padding(18)
            .Column(column =>
            {
                column.Spacing(6);
                column.Item().Text(CompanyLegalName).SemiBold();
                foreach (var line in CompanyInformationLines)
                {
                    column.Item().Text(line).FontColor(SecondaryTextColor);
                }
            });
    }

    private static void ComposePartyDetails(
        IContainer container,
        string partyHeading,
        string partyPrimaryValue,
        IReadOnlyCollection<(string Label, string Value)> partyDetails)
    {
        container
            .Background(CardBackgroundColor)
            .Border(1)
            .BorderColor(CardBorderColor)
            .Padding(18)
            .Column(column =>
            {
                column.Spacing(6);
                column.Item().Text(text =>
                {
                    text.Span($"{partyHeading}: ").SemiBold();
                    text.Span(partyPrimaryValue);
                });

                foreach (var entry in partyDetails.Where(item => !string.IsNullOrWhiteSpace(item.Label)))
                {
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(110).Text($"{entry.Label}:").SemiBold().FontColor(SecondaryTextColor);
                        row.RelativeItem().Text(string.IsNullOrWhiteSpace(entry.Value) ? "-" : entry.Value);
                    });
                }
            });
    }

    private static void ComposeContentCard(IContainer container, Action<IContainer> content)
    {
        container
            .Background(CardBackgroundColor)
            .Border(1)
            .BorderColor(CardBorderColor)
            .Padding(20)
            .Element(content);
    }

    private static void ComposeTotals(
        IContainer container,
        IReadOnlyCollection<(string Label, string Value)> totals)
    {
        container.AlignRight().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
            });

            foreach (var entry in totals)
            {
                table.Cell().Element(t => t.PaddingVertical(4).PaddingHorizontal(12)).AlignRight().Text(entry.Label).SemiBold();
                table.Cell().Element(t => t.PaddingVertical(4).PaddingHorizontal(12)).AlignRight().Text(entry.Value).SemiBold();
            }
        });
    }

    private static void ComposeFooterNotes(
        IContainer container,
        IReadOnlyCollection<string> footerNotes)
    {
        container
            .PaddingTop(8)
            .PaddingBottom(8)
            .AlignCenter()
            .Column(column =>
            {
                column.Spacing(3);
                foreach (var note in footerNotes)
                {
                    column.Item()
                        .AlignCenter()
                        .Text(note)
                        .FontColor(SecondaryTextColor);
                }
            });
    }

    private static void ComposeDocumentLines(
        IContainer container,
        IReadOnlyCollection<DocumentLine> lines,
        NumberFormatInfo currencyFormat,
        CultureInfo culture,
        string itemLabel,
        string descriptionLabel,
        string quantityLabel,
        string unitPriceLabel,
        string discountLabel,
        string priceLabel)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(5.8f);
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(1.6f);
                columns.RelativeColumn(1.6f);
                columns.RelativeColumn(1.8f);
            });

            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCellStyle).Text(itemLabel);
                header.Cell().Element(TableHeaderCellStyle).Text(descriptionLabel);
                header.Cell().Element(TableHeaderCellStyle).AlignCenter().Text(quantityLabel);
                header.Cell().Element(TableHeaderCellStyle).AlignRight().Text(unitPriceLabel);
                header.Cell().Element(TableHeaderCellStyle).AlignRight().Text(discountLabel);
                header.Cell().Element(TableHeaderCellStyle).AlignRight().Text(priceLabel);
            });

            var lineIndex = 1;
            foreach (var line in lines)
            {
                table.Cell().Element(TableBodyCellStyle).AlignCenter().Text(lineIndex.ToString(culture));
                table.Cell().Element(TableBodyDescriptionCellStyle).Column(column =>
                {
                    column.Item().Text(line.Description);
                    column.Item().Text(FormatUnitOfMeasure(line.UnitOfMeasure, culture))
                        .FontColor(SecondaryTextColor)
                        .FontSize(9);
                });
                table.Cell().Element(TableBodyCellStyle).AlignCenter().Text(line.Quantity.ToString("N0", culture));
                table.Cell().Element(TableBodyCellStyle).AlignRight().Text(FormatCurrency(line.UnitPrice, currencyFormat));
                table.Cell().Element(TableBodyCellStyle).AlignRight().Text(FormatCurrency(0, currencyFormat));
                table.Cell().Element(TableBodyCellStyle).AlignRight().Text(FormatCurrency(line.LineTotal, currencyFormat));
                lineIndex++;
            }

            for (var row = lines.Count + 1; row <= MinimumLineRows; row++)
            {
                table.Cell().Element(TableBodyCellStyle).AlignCenter().Text(row.ToString(culture));
                for (var col = 0; col < 5; col++)
                {
                    table.Cell().Element(TableBodyCellStyle).Text(string.Empty);
                }
            }
        });
    }

    private static void ComposeReceiptPayments(
        IContainer container,
        IReadOnlyCollection<ReceiptPayment> payments,
        NumberFormatInfo currencyFormat,
        string methodLabel,
        string amountLabel)
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
                header.Cell().Element(TableHeaderCellStyle).Text(methodLabel);
                header.Cell().Element(TableHeaderCellStyle).AlignRight().Text(amountLabel);
            });

            foreach (var payment in payments)
            {
                table.Cell().Element(TableBodyDescriptionCellStyle).Text(payment.Method);
                table.Cell().Element(TableBodyCellStyle).AlignRight().Text(FormatCurrency(payment.Amount, currencyFormat));
            }

            if (!payments.Any())
            {
                table.Cell().ColumnSpan(2).Element(TableBodyCellStyle).AlignCenter().Text("-");
            }
        });
    }

    private static IContainer TableHeaderCellStyle(IContainer container) =>
        container
            .Background(TableHeaderBackground)
            .BorderBottom(1)
            .BorderColor(TableBorderColor)
            .PaddingVertical(10)
            .PaddingHorizontal(8)
            .DefaultTextStyle(TextStyle.Default.SemiBold().FontColor("#ffffff").FontSize(11));

    private static IContainer TableBodyCellStyle(IContainer container) =>
        container
            .Background(CardBackgroundColor)
            .BorderBottom(1)
            .BorderColor(TableBorderColor)
            .PaddingVertical(10)
            .PaddingHorizontal(8);

    private static IContainer TableBodyDescriptionCellStyle(IContainer container) =>
        TableBodyCellStyle(container).AlignTop();

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

        return CultureInfo.GetCultureInfo("es-DO");
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

    private static string GetPlaceholderValue(CultureInfo culture) =>
        Localize(culture, "Not provided", "No especificado");

    private static string FormatCurrency(decimal amount, NumberFormatInfo currencyFormat) =>
        amount.ToString("C", currencyFormat);

    private static string FormatUnitOfMeasure(string? unitCode, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(unitCode))
        {
            return IsSpanishCulture(culture) ? "Unidad no especificada" : "Unit not specified";
        }

        if (UnitLabels.TryGetValue(unitCode, out var labels))
        {
            return IsSpanishCulture(culture) ? labels.Spanish : labels.English;
        }

        return unitCode;
    }

    private static IReadOnlyCollection<string> GetFooterNotes(CultureInfo culture) =>
        IsSpanishCulture(culture)
            ? new[]
            {
                CompanyLegalName,
                "40% de anticipo y 60 a plazo acordado. Cotización válida por 30 días continuos.",
                "ariaspavel2@gmail.com"
            }
            : new[]
            {
                CompanySecondaryName,
                "40% upfront and 60 on the agreed term. Quote valid for 30 continuous days.",
                "ariaspavel2@gmail.com"
            };

    private static Image? LoadRasterLogo()
    {
        try
        {
            var pngPath = Path.Combine(AppContext.BaseDirectory, "Assets", "papavelag-logo.png");
            return File.Exists(pngPath) ? Image.FromFile(pngPath) : null;
        }
        catch
        {
            return null;
        }
    }

    private static SvgImage? LoadVectorLogo()
    {
        try
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "papavelag-logo.svg");
            return File.Exists(logoPath) ? SvgImage.FromFile(logoPath) : null;
        }
        catch
        {
            return null;
        }
    }

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

    private static InvoicePdfResources GetInvoiceResources(string? cultureName, bool asQuote)
    {
        var culture = GetCultureOrDefault(cultureName);
        var isSpanish = IsSpanishCulture(culture);

        var title = asQuote
            ? (isSpanish ? "Cotización" : "Quote")
            : (isSpanish ? "Factura" : "Invoice");
        var numberLabel = asQuote
            ? (isSpanish ? "Cotización #" : "Quote #")
            : (isSpanish ? "Factura #" : "Invoice #");
        return new InvoicePdfResources(
            culture,
            title,
            isSpanish ? "Fecha" : "Date",
            isSpanish ? "Cliente" : "Customer",
            numberLabel,
            isSpanish ? "Descripción" : "Description",
            isSpanish ? "Cantidad" : "Quantity",
            isSpanish ? "Precio unitario" : "Unit Price",
            isSpanish ? "Subtotal" : "Line Total",
            isSpanish ? "Total" : "Total");
    }

    private sealed record InvoicePdfResources(
        CultureInfo Culture,
        string TitleLabel,
        string DateLabel,
        string CustomerLabel,
        string NumberLabel,
        string DescriptionLabel,
        string QuantityLabel,
        string UnitPriceLabel,
        string LineTotalLabel,
        string TotalLabel);
}
