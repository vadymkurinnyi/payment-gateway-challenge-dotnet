using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Payments.Process.Banking;

public record BankPaymentRequest
{
    [JsonPropertyName("card_number")]
    public required string CardNumber { get; init; }
    [JsonPropertyName("expiry_date")]
    public required string ExpiryDate{ get; init; }
    [JsonPropertyName("currency")]
    public required string Currency { get; init; }
    [JsonPropertyName("amount")]
    public required uint Amount { get; init; }
    [JsonPropertyName("cvv")]
    public required string Cvv{ get; init; }
}