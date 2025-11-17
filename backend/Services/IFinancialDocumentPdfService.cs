using IORManager.Models;

namespace IORManager.Services;

public interface IFinancialDocumentPdfService
{
    byte[] GenerateQuotePdf(Invoice invoice);

    byte[] GenerateInvoicePdf(Invoice invoice);

    byte[] GenerateReceiptPdf(Receipt receipt);

    byte[] GeneratePurchaseOrderPdf(PurchaseOrder purchaseOrder);
}
