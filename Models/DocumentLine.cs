namespace IORManager.Models;

public record DocumentLine(string Description, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}
