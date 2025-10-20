using System.ComponentModel.DataAnnotations;
using IORManager.Models;

namespace IORManager.Dtos;

public record PurchaseOrderCreateRequest
{
    [Required]
    public string PurchaseOrderNumber { get; init; } = string.Empty;

    [Required]
    public DateOnly PurchaseOrderDate { get; init; }
        = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    public string SupplierName { get; init; } = string.Empty;

    [MinLength(1)]
    public IReadOnlyList<PurchaseOrderLineRequest> Lines { get; init; } = Array.Empty<PurchaseOrderLineRequest>();

    public PurchaseOrder ToPurchaseOrder()
    {
        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            Number = PurchaseOrderNumber,
            Date = PurchaseOrderDate,
            SupplierName = SupplierName,
            Lines = Lines.Select(line => line.ToDocumentLine()).ToList()
        };

        purchaseOrder.RecalculateTotal();
        return purchaseOrder;
    }
}

public record PurchaseOrderLineRequest
{
    [Required]
    public string Description { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
        = 1;

    [Range(typeof(decimal), "0.0", "79228162514264337593543950335")]
    public decimal UnitPrice { get; init; }
        = 0m;

    public DocumentLine ToDocumentLine() => new()
    {
        Description = Description,
        Quantity = Quantity,
        UnitPrice = UnitPrice
    };
}
