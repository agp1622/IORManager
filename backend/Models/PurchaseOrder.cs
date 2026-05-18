using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IORManager.Models;

public class PurchaseOrder : FinancialDocument
{
    public PurchaseOrder()
    {
        Lines = new List<DocumentLine>();
        Attachments = new List<PurchaseOrderAttachment>();
    }

    [NotMapped]
    public string SupplierName
    {
        get => PartyName;
        set => PartyName = value;
    }

    /// <summary>The quote this expense was incurred for (optional project link).</summary>
    public Guid? QuoteId { get; set; }
    public Quote? Quote { get; set; }

    /// <summary>Free-form notes describing why this purchase is needed / the investment rationale.</summary>
    [MaxLength(4000)]
    public string? InvestmentNotes { get; set; }

    public List<DocumentLine> Lines { get; set; }

    public List<PurchaseOrderAttachment> Attachments { get; set; }

    public void RecalculateTotal() => TotalAmount = Lines.Sum(line => line.LineTotal);
}
