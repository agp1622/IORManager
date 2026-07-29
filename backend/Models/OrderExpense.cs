using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace IORManager.Models;

/// <summary>One expense line logged against an Order, optionally backed by a supplier receipt.</summary>
public class OrderExpense
{
    public int Id { get; set; }

    public Guid OrderId { get; set; }
    public PurchaseOrder Order { get; set; } = null!;

    [Required]
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.0", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    /// <summary>Whether the supplier issued an invoice for this expense (gates Rnc/receipt relevance).</summary>
    public bool HasInvoice { get; set; }

    /// <summary>RNC (Dominican tax ID) of the company that issued the invoice, when applicable.</summary>
    [MaxLength(20)]
    public string? Rnc { get; set; }

    [MaxLength(255)]
    public string? ReceiptFileName { get; set; }

    [MaxLength(100)]
    public string? ReceiptContentType { get; set; }

    public long? ReceiptFileSize { get; set; }

    /// <summary>Excluded from JSON responses — bulk list endpoints would otherwise serialize every receipt's
    /// full bytes. Fetched separately via the dedicated receipt download endpoint.</summary>
    [JsonIgnore]
    public byte[]? ReceiptFileData { get; set; }

    public DateTime? ReceiptUploadedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
