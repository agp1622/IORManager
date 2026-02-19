using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WordDoc = DocumentFormat.OpenXml.Wordprocessing.Document;
using WordColor = DocumentFormat.OpenXml.Wordprocessing.Color;
using IORManager.Models;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPdfDocument = QuestPDF.Fluent.Document;
using WordprocessingDocument = DocumentFormat.OpenXml.Packaging.WordprocessingDocument;

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
    private const string NcfFieldColor = "#c53030";
    private const string NcfWordColor = "C53030";

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
        GenerateInvoiceDocument(invoice, isQuote: true, ncfNumber: null);

    public byte[] GenerateInvoicePdf(Invoice invoice, string? ncfNumber = null) =>
        GenerateInvoiceDocument(invoice, isQuote: false, ncfNumber);

    public byte[] GenerateInvoiceWord(Invoice invoice, string? ncfNumber = null)
    {
        var resources = GetInvoiceResources(invoice.CultureName, asQuote: false);
        var culture = resources.Culture;
        var currencyFormat = CreateCurrencyFormat(culture, invoice.CurrencyCode);
        var documentNumber = DocumentNumberFormatter.ToInvoiceNumber(invoice.Number);
        var totals = GetInvoiceTotals(invoice);
        var subtotalLabel = Localize(culture, "Invoice Subtotal", "Subtotal");
        var itbisRateLabel = Localize(culture, "ITBIS Rate", "Tasa ITBIS");
        var itbisAmountLabel = Localize(culture, "ITBIS Amount", "Monto ITBIS");
        var addressLabel = Localize(culture, "Address", "Dirección");
        var contactLabel = Localize(culture, "Contact", "Contacto");
        var invoiceDocumentDate = invoice.InvoiceDate;
        var invoiceExpirationDate = invoice.InvoiceExpirationDate;

        using var ms = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new WordDoc(new Body());
            var body = mainPart.Document.Body!;

            body.Append(CreateHeadingParagraph($"{resources.TitleLabel} {documentNumber}"));
            body.Append(CreateLabelValueParagraph(resources.NumberLabel, documentNumber));
            body.Append(CreateLabelValueParagraph(resources.DateLabel, invoiceDocumentDate.ToString("D", culture)));
            body.Append(CreateLabelValueParagraph(resources.ExpirationLabel, invoiceExpirationDate.ToString("D", culture)));
            body.Append(CreateLabelValueParagraph(resources.CustomerLabel, invoice.CustomerName));
            if (!string.IsNullOrWhiteSpace(invoice.CustomerAddress))
            {
                body.Append(CreateLabelValueParagraph(addressLabel, invoice.CustomerAddress));
            }
            if (!string.IsNullOrWhiteSpace(invoice.CustomerContact))
            {
                body.Append(CreateLabelValueParagraph(contactLabel, invoice.CustomerContact));
            }
            body.Append(CreateLabelValueParagraph(Localize(culture, "Currency", "Moneda"), invoice.CurrencyCode));
            var displayNcf = ncfNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(displayNcf))
            {
                body.Append(CreateLabelValueParagraph(
                    Localize(culture, "NCF", "NCF"),
                    displayNcf,
                    NcfWordColor,
                    valueSeparator: "-"));
            }
            body.Append(new Paragraph(new Run(new Text(string.Empty))));
            body.Append(CreateHeadingParagraph(Localize(culture, "Line Items", "Conceptos")));

            body.Append(CreateLinesWordTable(
                invoice.Lines,
                culture,
                currencyFormat,
                Localize(culture, "Item #", "Ítem #"),
                resources.DescriptionLabel,
                resources.QuantityLabel,
                resources.UnitPriceLabel,
                Localize(culture, "Price", "Precio")));

            body.Append(new Paragraph(new Run(new Text(string.Empty))));
            body.Append(CreateLabelValueParagraph(subtotalLabel, FormatCurrency(totals.Subtotal, currencyFormat)));
            body.Append(CreateLabelValueParagraph(itbisRateLabel, FormatPercentage(totals.Rate, culture)));
            body.Append(CreateLabelValueParagraph(itbisAmountLabel, FormatCurrency(totals.ItbisAmount, currencyFormat)));
            body.Append(CreateLabelValueParagraph(Localize(culture, "Total", "Total"), FormatCurrency(totals.Total, currencyFormat)));

            mainPart.Document.Save();
        }

        return ms.ToArray();
    }

    private byte[] GenerateInvoiceDocument(Invoice invoice, bool isQuote, string? ncfNumber)
    {
        var resources = GetInvoiceResources(invoice.CultureName, isQuote);
        var culture = resources.Culture;
        var currencyFormat = CreateCurrencyFormat(culture, invoice.CurrencyCode);
        var invoiceNcf = isQuote ? null : (string.IsNullOrWhiteSpace(ncfNumber) ? null : ncfNumber.Trim());
        var documentNumber = isQuote
            ? invoice.Number
            : DocumentNumberFormatter.ToInvoiceNumber(invoice.Number);
        var documentDate = isQuote ? invoice.QuoteDate : invoice.InvoiceDate;
        var expirationDate = isQuote
            ? invoice.QuoteExpirationDate
            : invoice.InvoiceExpirationDate;

        var invoiceTotals = GetInvoiceTotals(invoice);
        var subtotalLabel = isQuote
            ? Localize(culture, "Quote Subtotal", "Subtotal de cotización")
            : Localize(culture, "Invoice Subtotal", "Subtotal de factura");
        var itbisRateLabel = Localize(culture, "ITBIS Rate", "Tasa ITBIS");
        var itbisAmountLabel = Localize(culture, "ITBIS Amount", "Monto ITBIS");
        var addressLabel = Localize(culture, "Address", "Dirección");
        var contactLabel = Localize(culture, "Contact", "Contacto");

        var metadata = new List<(string Label, string Value)>
        {
            (resources.NumberLabel, documentNumber),
            (resources.DateLabel, documentDate.ToString("d", culture)),
            (resources.ExpirationLabel, expirationDate.ToString("d", culture))
        };
        metadata.Add((resources.CustomerLabel, invoice.CustomerName));
        if (!string.IsNullOrWhiteSpace(invoice.CustomerAddress))
        {
            metadata.Add((addressLabel, invoice.CustomerAddress));
        }
        if (!string.IsNullOrWhiteSpace(invoice.CustomerContact))
        {
            metadata.Add((contactLabel, invoice.CustomerContact));
        }

        var totals = new List<(string Label, string Value)>
        {
            (subtotalLabel, FormatCurrency(invoiceTotals.Subtotal, currencyFormat)),
            (itbisRateLabel, FormatPercentage(invoiceTotals.Rate, culture)),
            (itbisAmountLabel, FormatCurrency(invoiceTotals.ItbisAmount, currencyFormat)),
            (Localize(culture, "Deposit Received", "Depósito recibido"), FormatCurrency(0, currencyFormat)),
            ($"{resources.TotalLabel.ToUpperInvariant()} ({currencyFormat.CurrencySymbol})", FormatCurrency(invoiceTotals.Total, currencyFormat))
        };

        return CreateDocument(
            documentTypeLabel: resources.TitleLabel.ToUpperInvariant(),
            metadata: metadata,
            content: container => ComposeDocumentLines(
                container,
                invoice.Lines,
                currencyFormat,
                culture,
                Localize(culture, "Item #", "Ítem #"),
                resources.DescriptionLabel,
                resources.QuantityLabel,
                resources.UnitPriceLabel,
                Localize(culture, "Price", "Precio")),
            totals: totals,
            footerNotes: GetFooterNotes(culture),
            culture: culture,
            ncfNumber: invoiceNcf);
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

        metadata.Add((Localize(culture, "Customer", "Cliente"), receipt.CustomerName));
        metadata.Add((Localize(culture, "Currency", "Moneda"), receipt.CurrencyCode));

        var totals = new List<(string Label, string Value)>
        {
            (Localize(culture, "Total received", "Total recibido"), FormatCurrency(receipt.TotalAmount, currencyFormat))
        };

        return CreateDocument(
            documentTypeLabel: Localize(culture, "Receipt", "Recibo").ToUpperInvariant(),
            metadata: metadata,
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

        metadata.Add((Localize(culture, "Supplier", "Proveedor"), purchaseOrder.SupplierName));

        var totals = new List<(string Label, string Value)>
        {
            (Localize(culture, "Order Subtotal", "Subtotal"), FormatCurrency(purchaseOrder.TotalAmount, currencyFormat)),
            ($"{Localize(culture, "Total", "Total").ToUpperInvariant()} ({currencyFormat.CurrencySymbol})", FormatCurrency(purchaseOrder.TotalAmount, currencyFormat))
        };

        return CreateDocument(
            documentTypeLabel: Localize(culture, "Purchase Order", "Orden de compra").ToUpperInvariant(),
            metadata: metadata,
            content: container => ComposeDocumentLines(
                container,
                purchaseOrder.Lines,
                currencyFormat,
                culture,
                Localize(culture, "Item #", "Ítem #"),
                Localize(culture, "Description", "Descripción"),
                Localize(culture, "Qty", "Cant."),
                Localize(culture, "Unit Price", "Precio unitario"),
                Localize(culture, "Price", "Precio")),
            totals: totals,
            footerNotes: GetFooterNotes(culture),
            culture: culture);
    }

    private static InvoiceTotals GetInvoiceTotals(Invoice invoice)
    {
        var (subtotal, normalizedRate, itbisAmount) = invoice.CalculateFinancials();
        var total = subtotal + itbisAmount;
        return new InvoiceTotals(subtotal, normalizedRate, itbisAmount, total);
    }

    private static byte[] CreateDocument(
        string documentTypeLabel,
        IReadOnlyCollection<(string Label, string Value)> metadata,
        Action<IContainer> content,
        IReadOnlyCollection<(string Label, string Value)> totals,
        IReadOnlyCollection<string> footerNotes,
        CultureInfo culture,
        string? ncfNumber = null)
    {
        metadata ??= Array.Empty<(string, string)>();
        totals ??= Array.Empty<(string, string)>();
        footerNotes ??= Array.Empty<string>();

        var doc = QuestPdfDocument.Create(document =>
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
                        ComposeHeader(header, documentTypeLabel, metadata, ncfNumber, culture));
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
        IReadOnlyCollection<(string Label, string Value)> metadata,
        string? ncfNumber,
        CultureInfo culture)
    {
        var rasterLogo = BrandRasterLogo.Value;
        var vectorLogo = BrandVectorLogo.Value;

        container
            .Background(HeaderBackgroundColor)
            .Border(1)
            .BorderColor(HeaderBorderColor)
            .Padding(20)
            .Column(column =>
            {
                column.Spacing(10);
                column.Item().Row(row =>
                {
                    row.RelativeItem(0.55f).Row(brandRow =>
                    {
                        brandRow.ConstantItem(120).Height(120).Element(logoContainer =>
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
                            col.Item().Text(CompanyLegalName).FontSize(18).SemiBold();
                            col.Item().Text(CompanySecondaryName).FontColor(SecondaryTextColor);
                        });
                    });

                    row.RelativeItem(0.45f).AlignRight().Column(col =>
                    {
                        col.Spacing(4);
                        col.Item()
                            .AlignRight()
                            .Text(documentTypeLabel)
                            .FontSize(26)
                            .SemiBold();

                        foreach (var entry in metadata.Where(item => !string.IsNullOrWhiteSpace(item.Value)))
                        {
                            col.Item().AlignRight().Text(text =>
                            {
                                text.Span($"{entry.Label}: ").SemiBold();
                                text.Span(entry.Value);
                            });
                        }

                        var displayNcf = ncfNumber?.Trim();
                        if (!string.IsNullOrWhiteSpace(displayNcf))
                        {
                            col.Item().AlignRight().Text(text =>
                            {
                                text.Span($"{Localize(culture, "NCF", "NCF")}-").SemiBold().FontColor(NcfFieldColor);
                                text.Span(displayNcf).FontColor(NcfFieldColor);
                            });
                        }
                    });
                });

                column.Item().Text(text =>
                {
                    text.DefaultTextStyle(TextStyle.Default.FontSize(9).FontColor(SecondaryTextColor));
                    text.Span(CompanyLegalName + " • ");
                    text.Span(string.Join(" • ", CompanyInformationLines));
                });
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
        string priceLabel)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(40);
                columns.RelativeColumn(6);
                columns.ConstantColumn(55);
                columns.ConstantColumn(100);
                columns.ConstantColumn(100);
            });

            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCellStyle).Text(itemLabel);
                header.Cell().Element(TableHeaderCellStyle).Text(descriptionLabel);
                header.Cell().Element(TableHeaderCellStyle).AlignCenter().Text(quantityLabel);
                header.Cell().Element(TableHeaderCellStyle).AlignRight().Text(unitPriceLabel);
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
                table.Cell().Element(TableBodyCellStyle).AlignRight().Text(FormatCurrency(line.LineTotal, currencyFormat));
                lineIndex++;
            }

        });
    }

    private static Table CreateLinesWordTable(
        IReadOnlyCollection<DocumentLine> lines,
        CultureInfo culture,
        NumberFormatInfo currencyFormat,
        string itemLabel,
        string descriptionLabel,
        string quantityLabel,
        string unitPriceLabel,
        string priceLabel)
    {
        var table = new Table();
        var borders = new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 6 },
            new BottomBorder { Val = BorderValues.Single, Size = 6 },
            new LeftBorder { Val = BorderValues.Single, Size = 6 },
            new RightBorder { Val = BorderValues.Single, Size = 6 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 });

        table.AppendChild(new TableProperties(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }, borders));

        var headerRow = new TableRow();
        headerRow.Append(CreateHeaderCell(itemLabel));
        headerRow.Append(CreateHeaderCell(descriptionLabel));
        headerRow.Append(CreateHeaderCell(quantityLabel));
        headerRow.Append(CreateHeaderCell(unitPriceLabel));
        headerRow.Append(CreateHeaderCell(priceLabel));
        table.Append(headerRow);

        var lineIndex = 1;
        foreach (var line in lines)
        {
            var row = new TableRow();
            row.Append(CreateCell(lineIndex.ToString(culture)));
            row.Append(CreateCell(line.Description));
            row.Append(CreateCell(line.Quantity.ToString("N0", culture)));
            row.Append(CreateCell(FormatCurrency(line.UnitPrice, currencyFormat)));
            row.Append(CreateCell(FormatCurrency(line.LineTotal, currencyFormat)));
            table.Append(row);
            lineIndex++;
        }

        return table;
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

    private static string FormatPercentage(decimal rate, CultureInfo culture) =>
        rate.ToString("P2", culture);

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
                "ariaspavel2@gmail.com"
            }
            : new[]
            {
                CompanySecondaryName,
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
            isSpanish ? "Vence" : "Expires",
            isSpanish ? "Cliente" : "Customer",
            numberLabel,
            isSpanish ? "Descripción" : "Description",
            isSpanish ? "Cant." : "Qty.",
            isSpanish ? "Precio unitario" : "Unit Price",
            isSpanish ? "Subtotal" : "Line Total",
            isSpanish ? "Total" : "Total");
    }

    private static Paragraph CreateHeadingParagraph(string text)
    {
        var run = new Run(new RunProperties(new Bold(), new FontSize { Val = "30" }), new Text(text ?? string.Empty));
        return new Paragraph(run)
        {
            ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines { After = "160" })
        };
    }

    private static Paragraph CreateLabelValueParagraph(
        string label,
        string value,
        string? hexColor = null,
        string valueSeparator = ": ")
    {
        var paragraph = new Paragraph();
        var labelRunProps = new RunProperties(new Bold());
        if (!string.IsNullOrEmpty(hexColor))
        {
            labelRunProps.AppendChild(new WordColor { Val = hexColor });
        }

        paragraph.Append(new Run(labelRunProps, new Text($"{label ?? string.Empty}{valueSeparator}")));

        var valueRunProps = new RunProperties();
        if (!string.IsNullOrEmpty(hexColor))
        {
            valueRunProps.AppendChild(new WordColor { Val = hexColor });
        }

        paragraph.Append(new Run(valueRunProps, new Text(value ?? string.Empty)));
        return paragraph;
    }

    private static TableCell CreateHeaderCell(string text)
    {
        var cell = new TableCell(new Paragraph(new Run(new RunProperties(new Bold()), new Text(text ?? string.Empty))));
        cell.Append(new TableCellProperties(new Shading { Fill = "e1e4ec", Val = ShadingPatternValues.Clear }));
        return cell;
    }

    private static TableCell CreateCell(string text) =>
        new(new Paragraph(new Run(new Text(text ?? string.Empty))));

private sealed record InvoiceTotals(decimal Subtotal, decimal Rate, decimal ItbisAmount, decimal Total);

private sealed record InvoicePdfResources(
    CultureInfo Culture,
    string TitleLabel,
    string DateLabel,
    string ExpirationLabel,
    string CustomerLabel,
    string NumberLabel,
    string DescriptionLabel,
    string QuantityLabel,
    string UnitPriceLabel,
    string LineTotalLabel,
    string TotalLabel);
}
