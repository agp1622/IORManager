using System.ComponentModel.DataAnnotations;
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

    [NotMapped]
    public string CustomerName
    {
        get => PartyName;
        set => PartyName = value;
    }

    [MaxLength(300)]
    [Required]
    public string CustomerAddress { get; set; } = string.Empty;

    [MaxLength(150)]
    [Required]
    public string CustomerContact { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid? QuoteId { get; set; }

    [NotMapped]
    public string? QuoteNumber { get; set; }

    [MaxLength(50)]
    public string? NcfNumber { get; set; }

    [MaxLength(10)]
    public string? NcfCategory { get; set; }

    public DateTime? InvoiceGeneratedAt { get; set; }

    [NotMapped]
    public bool InvoiceGenerated => InvoiceGeneratedAt.HasValue;

    [NotMapped]
    public DateOnly QuoteDate => Date;

    [NotMapped]
    public DateOnly InvoiceDate => InvoiceGeneratedAt.HasValue
        ? DateOnly.FromDateTime(InvoiceGeneratedAt.Value)
        : DateOnly.FromDateTime(DateTime.UtcNow);

    public List<DocumentLine> Lines { get; set; }

    public DateOnly? ExpirationDateOverride { get; set; }

    [NotMapped]
    public DateOnly QuoteExpirationDate => QuoteDate.AddMonths(1);

    [NotMapped]
    public DateOnly InvoiceExpirationDate => new DateOnly(InvoiceDate.Year, 12, 31);

    [NotMapped]
    public DateOnly ExpirationDate
    {
        get
        {
            return InvoiceGenerated
                ? InvoiceExpirationDate
                : QuoteExpirationDate;
        }
    }

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
