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

    /// <summary>The Order this invoice was generated from.</summary>
    public Guid? OrderId { get; set; }

    /// <summary>FK to the fiscal regime (B01, B02) used when the NCF was assigned.</summary>
    public int? FiscalRegimeId { get; set; }
    public FiscalRegime? FiscalRegime { get; set; }

    [MaxLength(50)]
    public string? NcfNumber { get; set; }

    [MaxLength(10)]
    public string? NcfCategory { get; set; }

    public DateTime? InvoiceGeneratedAt { get; set; }

    [NotMapped]
    public bool InvoiceGenerated => InvoiceGeneratedAt.HasValue;

    /// <summary>When set, the customer has paid this invoice as of this UTC timestamp.</summary>
    public DateTime? PaidAt { get; set; }

    [NotMapped]
    public bool IsPaid => PaidAt.HasValue;

    /// <summary>When set, the invoice has been delivered/emailed to the customer as of this UTC timestamp.</summary>
    public DateTime? SentAt { get; set; }

    [NotMapped]
    public bool IsSent => SentAt.HasValue;

    /// <summary>Populated by the controller from the linked customer's payment terms; not persisted.</summary>
    [NotMapped]
    public int? CustomerPaymentTermsDays { get; set; }

    /// <summary>The date payment is due: when the invoice was sent, plus the customer's payment terms.</summary>
    [NotMapped]
    public DateTime? PaymentDueDate => SentAt?.AddDays(CustomerPaymentTermsDays ?? 30);

    /// <summary>True once the invoice has been sent, its due date has arrived, and it still hasn't been paid.</summary>
    [NotMapped]
    public bool IsPaymentDue => !IsPaid && PaymentDueDate.HasValue && PaymentDueDate.Value <= DateTime.UtcNow;

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
