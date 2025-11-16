using IORManager.Models;

namespace IORManager.Services;

public interface IFinancialDocumentPdfService
{
    byte[] GenerateInvoicePdf(Invoice invoice);

    byte[] GenerateReceiptPdf(Receipt receipt);

    byte[] GeneratePurchaseOrderPdf(PurchaseOrder purchaseOrder);
}
