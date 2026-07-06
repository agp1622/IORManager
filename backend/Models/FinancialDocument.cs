using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    /// <summary>
    /// When set, the document has been soft-deleted (moved to trash) as of this UTC timestamp.
    /// Excluded from normal queries via a global query filter. Records are permanently purged
    /// once <see cref="SoftDeleteRecoveryWindow"/> has elapsed since this timestamp.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    [NotMapped]
    public bool IsDeleted => DeletedAt.HasValue;

    /// <summary>How long a soft-deleted document remains recoverable before permanent purge.</summary>
    public static TimeSpan SoftDeleteRecoveryWindow => TimeSpan.FromDays(365);
}
