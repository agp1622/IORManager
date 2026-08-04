using System.ComponentModel.DataAnnotations;
using IORManager.Models;

namespace IORManager.Dtos;

public record InvoiceCreateRequest
{
    [Required]
    public DateOnly InvoiceDate { get; init; }
        = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly? ExpirationDate { get; init; }
        = null;

    [Required]
    public string CustomerName { get; init; } = string.Empty;

    [Required]
    [RegularExpression("USD|DOP")]
    public string CurrencyCode { get; init; } = "USD";

    [Required]
    [RegularExpression("en-US|es-DO")]
    public string Locale { get; init; } = "es-DO";

    [Required]
    [MaxLength(300)]
    public string CustomerAddress { get; init; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string CustomerContact { get; init; } = string.Empty;

    [MinLength(1)]
    public IReadOnlyList<InvoiceLineRequest> Lines { get; init; } = Array.Empty<InvoiceLineRequest>();

    [Range(typeof(decimal), "0.0", "1.0")]
    public decimal? ItbisRate { get; init; }

    [MaxLength(100)]
    public string? CustomerPONumber { get; init; }

    [MaxLength(2000)]
    public string? Comments { get; init; }

    public Quote ToQuote()
    {
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            Date = InvoiceDate,
            CustomerName = CustomerName,
            CustomerAddress = CustomerAddress.Trim(),
            CustomerContact = CustomerContact.Trim(),
            CurrencyCode = CurrencyCode,
            CultureName = Locale,
            Lines = Lines.Select(line => line.ToDocumentLine()).ToList(),
            ItbisRate = ItbisRate,
            CustomerPONumber = string.IsNullOrWhiteSpace(CustomerPONumber) ? null : CustomerPONumber.Trim(),
            Comments = string.IsNullOrWhiteSpace(Comments) ? null : Comments.Trim()
        };

        quote.RecalculateTotal();
        return quote;
    }

    public Invoice ToInvoice()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Date = InvoiceDate,
            ExpirationDateOverride = null,
            CustomerName = CustomerName,
            CustomerAddress = CustomerAddress.Trim(),
            CustomerContact = CustomerContact.Trim(),
            CurrencyCode = CurrencyCode,
            CultureName = Locale,
            Lines = Lines.Select(line => line.ToDocumentLine()).ToList(),
            ItbisRate = ItbisRate,
            CustomerPONumber = string.IsNullOrWhiteSpace(CustomerPONumber) ? null : CustomerPONumber.Trim(),
            Comments = string.IsNullOrWhiteSpace(Comments) ? null : Comments.Trim(),
            NcfNumber = null,
            NcfCategory = null
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

public record InvoiceUpdateRequest
{
    [Required]
    public DateOnly InvoiceDate { get; init; }
        = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly? ExpirationDate { get; init; }
        = null;

    [Required]
    public string CustomerName { get; init; } = string.Empty;

    [Required]
    [RegularExpression("USD|DOP")]
    public string CurrencyCode { get; init; } = "USD";

    [Required]
    [RegularExpression("en-US|es-DO")]
    public string Locale { get; init; } = "es-DO";

    [Required]
    [MaxLength(300)]
    public string CustomerAddress { get; init; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string CustomerContact { get; init; } = string.Empty;

    [MinLength(1)]
    public IReadOnlyList<InvoiceLineRequest> Lines { get; init; } = Array.Empty<InvoiceLineRequest>();

    [Range(typeof(decimal), "0.0", "1.0")]
    public decimal? ItbisRate { get; init; }
        = null;

    [MaxLength(100)]
    public string? CustomerPONumber { get; init; }

    [MaxLength(2000)]
    public string? Comments { get; init; }
}
