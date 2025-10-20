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
        = string.Empty;

    [MinLength(1)]
    public IReadOnlyList<ReceiptPaymentRequest> Payments { get; init; } = Array.Empty<ReceiptPaymentRequest>();

    public Receipt ToReceipt()
    {
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            Number = ReceiptNumber,
            Date = ReceiptDate,
            CustomerName = CustomerName,
            ReferenceNumber = ReferenceNumber,
            Payments = Payments.Select(payment => payment.ToReceiptPayment()).ToList()
        };

        receipt.RecalculateTotal();
        return receipt;
    }
}

public record ReceiptPaymentRequest
{
    [Required]
    public string Method { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.0", "79228162514264337593543950335")]
    public decimal Amount { get; init; }
        = 0m;

    public ReceiptPayment ToReceiptPayment() => new()
    {
        Method = Method,
        Amount = Amount
    };
}
