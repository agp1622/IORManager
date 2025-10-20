namespace IORManager.Models;

public record Invoice(
    Guid Id,
    string Number,
    DateOnly Date,
    string CustomerName,
    decimal TotalAmount,
    IReadOnlyList<DocumentLine> Lines)
    : FinancialDocument(Id, Number, Date, CustomerName, TotalAmount);
