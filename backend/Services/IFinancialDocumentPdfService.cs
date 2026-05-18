using IORManager.Models;

namespace IORManager.Services;

public interface IFinancialDocumentPdfService
{
    byte[] GenerateQuotePdf(Invoice invoice, string? comments = null);

    byte[] GenerateInvoicePdf(Invoice invoice, string? ncfNumber = null);

    byte[] GenerateInvoiceWord(Invoice invoice, string? ncfNumber = null);

    byte[] GenerateReceiptPdf(Receipt receipt);

    byte[] GeneratePurchaseOrderPdf(PurchaseOrder purchaseOrder);
}
