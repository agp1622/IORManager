namespace IORManager.Models;

public record Receipt(
    Guid Id,
    string Number,
    DateOnly Date,
    string CustomerName,
    decimal TotalAmount,
    string? ReferenceNumber,
    IReadOnlyList<ReceiptPayment> Payments)
    : FinancialDocument(Id, Number, Date, CustomerName, TotalAmount);
