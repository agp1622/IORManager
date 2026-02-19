using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IORManager.Models;

public class Quote : FinancialDocument
{
    public Quote()
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

    public Guid? ConvertedInvoiceId { get; set; }
    public DateTime? ConvertedAt { get; set; }

    [NotMapped]
    public bool InvoiceGenerated => ConvertedInvoiceId.HasValue;

    [NotMapped]
    public DateOnly ExpirationDate => Date.AddMonths(1);

    public List<DocumentLine> Lines { get; set; }

    public void RecalculateTotal()
    {
        var subtotal = Lines.Sum(line => line.LineTotal);
        var normalizedRate = Math.Clamp(ItbisRate ?? 0m, 0m, 1m);
        var itbisAmount = Math.Round(subtotal * normalizedRate, 2, MidpointRounding.AwayFromZero);
        TotalAmount = subtotal + itbisAmount;
    }
}
