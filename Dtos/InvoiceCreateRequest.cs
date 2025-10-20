using System.ComponentModel.DataAnnotations;
using IORManager.Models;

namespace IORManager.Dtos;

public record InvoiceCreateRequest
{
    [Required]
    public string InvoiceNumber { get; init; } = string.Empty;

    [Required]
    public DateOnly InvoiceDate { get; init; }
        = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    public string CustomerName { get; init; } = string.Empty;

    [MinLength(1)]
    public IReadOnlyList<InvoiceLineRequest> Lines { get; init; } = Array.Empty<InvoiceLineRequest>();

    public Invoice ToInvoice()
    {
        var lines = Lines.Select(line => line.ToDocumentLine()).ToList();
        var total = lines.Sum(line => line.LineTotal);

        return new Invoice(
            Guid.NewGuid(),
            InvoiceNumber,
            InvoiceDate,
            CustomerName,
            total,
            lines);
    }
}

public record InvoiceLineRequest
{
    [Required]
    public string Description { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
        = 1;

    [Range(typeof(decimal), "0.0", "79228162514264337593543950335")]
    public decimal UnitPrice { get; init; }
        = 0m;

    public DocumentLine ToDocumentLine() => new(Description, Quantity, UnitPrice);
}
