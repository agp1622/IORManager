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
    /// RNC (Registro Nacional del Contribuyente) or Cédula of this customer. Required to issue an
    /// e-CF to them once the total exceeds the DGII's threshold for identifying the buyer.
    /// </summary>
    [MaxLength(11)]
    public string? Rnc { get; set; }

    /// <summary>
    /// Default number of days from invoice date until payment is due.
    /// Used when automatically creating an AccountPayable upon NCF assignment.
    /// </summary>
    public int DefaultPaymentTermsDays { get; set; } = 30;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Invoice> Invoices { get; set; } = [];
}
