using IORManager.Models;

namespace IORManager.Services;

public interface IFinancialDocumentPdfService
{
    byte[] GenerateQuotePdf(Invoice invoice, string? comments = null);

    byte[] GenerateInvoicePdf(Invoice invoice, string? ncfNumber = null);

    /// <summary>Combines multiple invoices into a single multi-page PDF, one invoice per page, in the given order.</summary>
    byte[] GenerateInvoicesBatchPdf(IReadOnlyCollection<Invoice> invoices);

    byte[] GenerateInvoiceWord(Invoice invoice, string? ncfNumber = null);

    byte[] GenerateReceiptPdf(Receipt receipt);

    byte[] GeneratePurchaseOrderPdf(PurchaseOrder purchaseOrder);
}
