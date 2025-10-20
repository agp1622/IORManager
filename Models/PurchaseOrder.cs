namespace IORManager.Models;

public record PurchaseOrder(
    Guid Id,
    string Number,
    DateOnly Date,
    string SupplierName,
    decimal TotalAmount,
    IReadOnlyList<DocumentLine> Lines)
    : FinancialDocument(Id, Number, Date, SupplierName, TotalAmount);
