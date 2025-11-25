using System.ComponentModel.DataAnnotations;
using IORManager.Models;

namespace IORManager.Dtos;

public record InvoiceCreateRequest
{
    [Required]
    public DateOnly InvoiceDate { get; init; }
        = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    public string CustomerName { get; init; } = string.Empty;

    [Required]
    [RegularExpression("USD|DOP")]
    public string CurrencyCode { get; init; } = "USD";

    [Required]
    [RegularExpression("en-US|es-DO")]
    public string Locale { get; init; } = "es-DO";

    [MinLength(1)]
    public IReadOnlyList<InvoiceLineRequest> Lines { get; init; } = Array.Empty<InvoiceLineRequest>();

    [Range(typeof(decimal), "0.0", "1.0")]
    public decimal? ItbisRate { get; init; }

    public Invoice ToInvoice()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Date = InvoiceDate,
            CustomerName = CustomerName,
            CurrencyCode = CurrencyCode,
            CultureName = Locale,
            Lines = Lines.Select(line => line.ToDocumentLine()).ToList(),
            ItbisRate = ItbisRate
        };

        invoice.RecalculateTotal();
        return invoice;
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

    [Required]
    [MaxLength(50)]
    public string UnitOfMeasure { get; init; } = "unit";

    public DocumentLine ToDocumentLine() => new()
    {
        Description = Description,
        Quantity = Quantity,
        UnitPrice = UnitPrice,
        UnitOfMeasure = UnitOfMeasure
    };
}
