using System.ComponentModel.DataAnnotations;
using IORManager.Models;

namespace IORManager.Dtos;

public record AccountPayableCreateRequest
{
    [Required]
    [MaxLength(50)]
    public string Number { get; init; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string SupplierName { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? CustomerPO { get; init; }

    [Range(typeof(decimal), "0.0", "79228162514264337593543950335")]
    public decimal Amount { get; init; }

    [MaxLength(3)]
    public string CurrencyCode { get; init; } = "USD";

    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly DueDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

    [MaxLength(20)]
    public string Status { get; init; } = "Pendiente";

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public AccountPayable ToAccountPayable()
    {
        var ap = new AccountPayable
        {
            Id = Guid.NewGuid(),
            Number = Number.Trim(),
            SupplierName = SupplierName.Trim(),
            CustomerPO = string.IsNullOrWhiteSpace(CustomerPO) ? null : CustomerPO.Trim(),
            TotalAmount = Amount,
            CurrencyCode = string.IsNullOrWhiteSpace(CurrencyCode) ? "USD" : CurrencyCode.Trim().ToUpperInvariant(),
            CultureName = "es-DO",
            Date = Date,
            DueDate = DueDate,
            Status = string.IsNullOrWhiteSpace(Status) ? "Pendiente" : Status.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        };

        // Auto-flag overdue records created in the past
        if (ap.Status == "Pendiente" && ap.DueDate < DateOnly.FromDateTime(DateTime.UtcNow))
            ap.Status = "Vencido";

        return ap;
    }
}

public record AccountPayableUpdateRequest
{
    [Required]
    [MaxLength(200)]
    public string SupplierName { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? CustomerPO { get; init; }

    [Range(typeof(decimal), "0.0", "79228162514264337593543950335")]
    public decimal Amount { get; init; }

    [MaxLength(3)]
    public string CurrencyCode { get; init; } = "USD";

    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public DateOnly DueDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

    [MaxLength(20)]
    public string Status { get; init; } = "Pendiente";

    [MaxLength(1000)]
    public string? Notes { get; init; }
}
