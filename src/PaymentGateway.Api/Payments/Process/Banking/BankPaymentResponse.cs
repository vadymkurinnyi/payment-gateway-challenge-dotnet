using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Payments.Process.Banking;

public class BankPaymentResponse
{
    [JsonPropertyName("authorized")]
    public bool IsAuthorised { get; set; }

    [JsonPropertyName("authorization_code")]
    public string Code { get; set; }
}