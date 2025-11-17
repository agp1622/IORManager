namespace IORManager.Models;

public class Invoice : FinancialDocument
{
    public Invoice()
    {
        Lines = new List<DocumentLine>();
    }

    public string CustomerName
    {
        get => PartyName;
        set => PartyName = value;
    }

    public List<DocumentLine> Lines { get; set; }

    public DateOnly ExpirationDate => Date.AddDays(30);

    public void RecalculateTotal() => TotalAmount = Lines.Sum(line => line.LineTotal);
}
