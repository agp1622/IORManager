using System.ComponentModel.DataAnnotations;
using IORManager.Models;

namespace IORManager.Dtos;

public record ReceiptCreateRequest
{
    [Required]
    public string ReceiptNumber { get; init; } = string.Empty;

    [Required]
    public DateOnly ReceiptDate { get; init; }
        = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    public string CustomerName { get; init; } = string.Empty;

    public string? ReferenceNumber { get; init; }
        = null;

    [MinLength(1)]
    public IReadOnlyList<ReceiptPaymentRequest> Payments { get; init; } = Array.Empty<ReceiptPaymentRequest>();

    public Receipt ToReceipt()
    {
        var payments = Payments.Select(payment => payment.ToPayment()).ToList();
        var total = payments.Sum(payment => payment.Amount);

        return new Receipt(
            Guid.NewGuid(),
            ReceiptNumber,
            ReceiptDate,
            CustomerName,
            total,
            ReferenceNumber,
            payments);
    }
}

public record ReceiptPaymentRequest
{
    [Required]
    public string Method { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.0", "79228162514264337593543950335")]
    public decimal Amount { get; init; }
        = 0m;

    public ReceiptPayment ToPayment() => new(Method, Amount);
}
