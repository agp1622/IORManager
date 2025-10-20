using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IORManager.Models;

public class ReceiptPayment
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Method { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public Guid ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }
}
