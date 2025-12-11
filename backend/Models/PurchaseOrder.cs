using System.ComponentModel.DataAnnotations.Schema;

namespace IORManager.Models;

public class PurchaseOrder : FinancialDocument
{
    public PurchaseOrder()
    {
        Lines = new List<DocumentLine>();
    }

    [NotMapped]
    public string SupplierName
    {
        get => PartyName;
        set => PartyName = value;
    }

    public List<DocumentLine> Lines { get; set; }

    public void RecalculateTotal() => TotalAmount = Lines.Sum(line => line.LineTotal);
}
