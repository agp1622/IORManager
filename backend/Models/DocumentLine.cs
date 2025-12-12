using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IORManager.Models;

public class DocumentLine
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Required]
    [MaxLength(50)]
    public string UnitOfMeasure { get; set; } = "unit";

    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public Guid? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    [NotMapped]
    public decimal LineTotal => Quantity * UnitPrice;
}
