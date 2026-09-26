namespace PaymentGateway.Api.Payments.Process.Banking;

public record BankPaymentRequest
{
    public required string CardNumber { get; init; }
    public required string ExpiryDate{ get; init; }
    public required string Currency { get; init; }
    public required uint Amount { get; init; }
    public required string Cvv{ get; init; }
}