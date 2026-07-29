using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IORManager.Models;

public class Quote : FinancialDocument
{
    public Quote()
    {
        Lines = new List<DocumentLine>();
        Attachments = new List<QuoteAttachment>();
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

    /// <summary>The customer's purchase order number / reference that authorized this quote.</summary>
    [MaxLength(100)]
    public string? CustomerPONumber { get; set; }

    /// <summary>Free-form internal or client-facing comments for this quote.</summary>
    [MaxLength(2000)]
    public string? Comments { get; set; }

    public Guid? ConvertedInvoiceId { get; set; }
    public DateTime? ConvertedAt { get; set; }

    [NotMapped]
    public bool InvoiceGenerated => ConvertedInvoiceId.HasValue;

    [NotMapped]
    public DateOnly ExpirationDate => Date.AddMonths(1);

    /// <summary>Whether at least one Order has been created for this quote. Populated by the controller, not persisted.</summary>
    [NotMapped]
    public bool HasOrder { get; set; }

    /// <summary>Sum of expense amounts (same currency as this quote) across all linked Orders. Populated by the controller, not persisted.</summary>
    [NotMapped]
    public decimal TotalExpenses { get; set; }

    /// <summary>Number of expenses across all Orders linked to this quote. Populated by the controller, not persisted.</summary>
    [NotMapped]
    public int ExpenseCount { get; set; }

    /// <summary>Sum of expense amounts logged in a currency other than this quote's. Populated by the controller, not persisted.</summary>
    [NotMapped]
    public decimal OtherCurrencyExpenses { get; set; }

    /// <summary>The currency code of <see cref="OtherCurrencyExpenses"/>, when it is non-zero. Populated by the controller, not persisted.</summary>
    [NotMapped]
    public string? OtherCurrencyCode { get; set; }

    /// <summary>Revenue (TotalAmount) minus same-currency linked expenses. Meaningful only when TotalExpenses has been populated.</summary>
    [NotMapped]
    public decimal Profit => TotalAmount - TotalExpenses;

    public List<DocumentLine> Lines { get; set; }
    public List<QuoteAttachment> Attachments { get; set; }

    public void RecalculateTotal()
    {
        var subtotal = Lines.Sum(line => line.LineTotal);
        var normalizedRate = Math.Clamp(ItbisRate ?? 0m, 0m, 1m);
        var itbisAmount = Math.Round(subtotal * normalizedRate, 2, MidpointRounding.AwayFromZero);
        TotalAmount = subtotal + itbisAmount;
    }
}
