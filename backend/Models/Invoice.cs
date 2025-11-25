using System.ComponentModel.DataAnnotations.Schema;

namespace IORManager.Models;

public class Invoice : FinancialDocument
{
    public Invoice()
    {
        Lines = new List<DocumentLine>();
    }

    [Column(TypeName = "decimal(5,4)")]
    public decimal? ItbisRate { get; set; }

    public string CustomerName
    {
        get => PartyName;
        set => PartyName = value;
    }

    public List<DocumentLine> Lines { get; set; }

    public DateOnly ExpirationDate => Date.AddDays(30);

    public (decimal Subtotal, decimal NormalizedItbisRate, decimal ItbisAmount) CalculateFinancials()
    {
        var subtotal = Lines.Sum(line => line.LineTotal);
        var normalizedRate = NormalizeItbisRate(ItbisRate);
        var itbisAmount = Math.Round(subtotal * normalizedRate, 2, MidpointRounding.AwayFromZero);
        return (subtotal, normalizedRate, itbisAmount);
    }

    public void RecalculateTotal()
    {
        var (subtotal, _, itbisAmount) = CalculateFinancials();
        TotalAmount = subtotal + itbisAmount;
    }

    private static decimal NormalizeItbisRate(decimal? rate) =>
        Math.Clamp(rate ?? 0m, 0m, 1m);
}
