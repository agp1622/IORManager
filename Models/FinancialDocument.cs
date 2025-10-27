using System.ComponentModel.DataAnnotations;

namespace IORManager.Models;

public abstract class FinancialDocument
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Number { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [Required]
    [MaxLength(10)]
    public string CultureName { get; set; } = "en-US";

    [Required]
    [MaxLength(200)]
    public string PartyName { get; set; } = string.Empty;

    [DataType(DataType.Currency)]
    public decimal TotalAmount { get; set; }
}
