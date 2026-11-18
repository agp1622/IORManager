using IORManager.Models;
using IORManager.Services.Dgii;

namespace IORManager.Services;

public interface IFinancialDocumentPdfService
{
    byte[] GenerateQuotePdf(Invoice invoice, string? comments = null);

    /// <param name="ecfPrintData">
    /// When the invoice has an e-CF, the QR code and security code to print on the Representación
    /// Impresa (RI) per DGII requirements. Null for a traditional (non-electronic) NCF.
    /// </param>
    byte[] GenerateInvoicePdf(Invoice invoice, string? ncfNumber = null, EcfPrintData? ecfPrintData = null);

    /// <summary>Combines multiple invoices into a single multi-page PDF, one invoice per page, in the given order.</summary>
    byte[] GenerateInvoicesBatchPdf(IReadOnlyCollection<Invoice> invoices);

    /// <summary>Same as <see cref="GenerateInvoicesBatchPdf(IReadOnlyCollection{Invoice})"/>, printing each invoice's e-CF QR stamp when present in the map.</summary>
    byte[] GenerateInvoicesBatchPdf(
        IReadOnlyCollection<Invoice> invoices,
        IReadOnlyDictionary<Guid, EcfPrintData>? ecfPrintDataByInvoiceId);

    byte[] GenerateInvoiceWord(Invoice invoice, string? ncfNumber = null);

    byte[] GenerateReceiptPdf(Receipt receipt);

    byte[] GeneratePurchaseOrderPdf(PurchaseOrder purchaseOrder);
}
