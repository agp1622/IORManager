using System.ComponentModel.DataAnnotations;

namespace IORManager.Models;

public class Customer
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Contact { get; set; } = string.Empty;

    /// <summary>
    /// Default number of days from invoice date until payment is due.
    /// Used when automatically creating an AccountPayable upon NCF assignment.
    /// </summary>
    public int DefaultPaymentTermsDays { get; set; } = 30;

    /// <summary>NCF category (e.g. B01, B02, B14) used by default when generating invoices for this
    /// customer. Null means no override — falls back to <see cref="Services.NcfCategoryCatalog.DefaultCategoryCode"/>.</summary>
    [MaxLength(3)]
    public string? DefaultNcfCategory { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Invoice> Invoices { get; set; } = [];
}
