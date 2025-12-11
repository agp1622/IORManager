using System.ComponentModel.DataAnnotations.Schema;

namespace IORManager.Models;

public class Receipt : FinancialDocument
{
    public Receipt()
    {
        Payments = new List<ReceiptPayment>();
    }

    [NotMapped]
    public string CustomerName
    {
        get => PartyName;
        set => PartyName = value;
    }

    public string? ReferenceNumber { get; set; }

    public List<ReceiptPayment> Payments { get; set; }

    public void RecalculateTotal() => TotalAmount = Payments.Sum(payment => payment.Amount);
}
