using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IORManager.Models;

public class AccountPayable : FinancialDocument
{
    /// <summary>Alias for <see cref="FinancialDocument.PartyName"/> — the customer on the linked invoice.</summary>
    [NotMapped]
    public string SupplierName
    {
        get => PartyName;
        set => PartyName = value;
    }

    /// <summary>Optional purchase-order number from the customer that covers this invoice.</summary>
    [MaxLength(100)]
    public string? CustomerPO { get; set; }

    /// <summary>Payment due date.</summary>
    public DateOnly DueDate { get; set; }

    /// <summary>Payment status: Pendiente | Vencido | Pagado.</summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pendiente";

    /// <summary>Free-form notes or approval references.</summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>The invoice that originated this payable (auto-populated when NCF is assigned).</summary>
    public Guid? InvoiceId { get; set; }

    [ForeignKey(nameof(InvoiceId))]
    public Invoice? Invoice { get; set; }
}
