using System.ComponentModel.DataAnnotations;

namespace IORManager.Models;

/// <summary>
/// Represents a Dominican Republic NCF fiscal regime (e.g. B01 Crédito Fiscal, B02 Consumo).
/// Each regime tracks how many invoices have been issued under it.
/// </summary>
public class FiscalRegime
{
    public int Id { get; set; }

    /// <summary>NCF category code (e.g. "B01", "B02").</summary>
    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable name of the regime.</summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Running count of invoices issued under this regime.</summary>
    public int InvoiceCount { get; set; }

    public List<Invoice> Invoices { get; set; } = new();
}
