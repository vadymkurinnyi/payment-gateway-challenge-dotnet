using PaymentGateway.Api.Payments.Enums;

namespace PaymentGateway.Api.Payments;

public class PaymentResponse
{
    public required Guid Id { get; set; }
    public required PaymentStatus Status { get; set; }
    public required string CardNumberLastFour { get; set; }
    public required int ExpiryMonth { get; set; }
    public required int ExpiryYear { get; set; }
    public required string Currency { get; set; }
    public required uint Amount { get; set; }

    public static PaymentResponse From(Payment payment) =>
        new ()
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount
        };
}