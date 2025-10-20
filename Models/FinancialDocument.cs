namespace IORManager.Models;

public abstract record FinancialDocument(
    Guid Id,
    string Number,
    DateOnly Date,
    string PartyName,
    decimal TotalAmount);
