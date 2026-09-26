using ErrorOr;

namespace PaymentGateway.Api.Payments.Process;

public record NewPayment
{
    private static readonly HashSet<string> AllowedCurrencies = new() { "USD", "EUR", "GBP" };

    public string CardNumber { get; }
    public int ExpiryMonth { get; }
    public int ExpiryYear { get; }
    public string Currency { get; }
    public uint Amount { get; }
    public string Cvv { get; }

    private NewPayment(string cardNumber, int expiryMonth, int expiryYear,
        string currency, uint amount, string cvv)
    {
        CardNumber = cardNumber;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Currency = currency;
        Amount = amount;
        Cvv = cvv;
    }

    public static ErrorOr<NewPayment> Create(
        string? cardNumber, int expiryMonth, int expiryYear,
        string? currency, int amount, string? cvv, DateTime now)
    {
        var errors = new List<Error>();

        if (cardNumber is null || cardNumber.Length is < 14 or > 19 || !cardNumber.All(char.IsDigit))
            errors.Add(Error.Validation("CardNumber", "Card number must be 14-19 digits."));

        if (expiryMonth is < 1 or > 12)
            errors.Add(Error.Validation("ExpiryMonth", "Expiry month must be 1-12."));
        else if (new DateTime(expiryYear, expiryMonth, 1).AddMonths(1) <= now)
            errors.Add(Error.Validation("Expiry", "Card has expired."));

        if (string.IsNullOrEmpty(currency) || !AllowedCurrencies.Contains(currency))
            errors.Add(Error.Validation("Currency", "Currency must be one of: USD, EUR, GBP."));

        if (amount <= 0)
            errors.Add(Error.Validation("Amount", "Amount must be positive."));

        if (cvv is null || cvv.Length is < 3 or > 4 || !cvv.All(char.IsDigit))
            errors.Add(Error.Validation("Cvv", "CVV must be 3-4 digits."));

        if (errors.Count > 0)
            return errors;

        return new NewPayment(cardNumber!, expiryMonth, expiryYear, currency!, (uint)amount, cvv!);
    }
}