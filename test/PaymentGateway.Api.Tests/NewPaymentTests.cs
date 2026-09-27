using ErrorOr;

using PaymentGateway.Api.Payments.Process;

namespace PaymentGateway.Api.Tests;

public class NewPaymentTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private const string ValidCardNumber = "2222405343248877";
    private const int ValidMonth = 12;
    private const int ValidYear = 2027;
    private const string ValidCurrency = "GBP";
    private const int ValidAmount = 100;
    private const string ValidCvv = "123";

    private static ErrorOr<NewPayment> Create(
        string? cardNumber = ValidCardNumber,
        int expiryMonth = ValidMonth,
        int expiryYear = ValidYear,
        string? currency = ValidCurrency,
        int amount = ValidAmount,
        string? cvv = ValidCvv) =>
        NewPayment.Create(cardNumber, expiryMonth, expiryYear, currency, amount, cvv, Now);

    private static void AssertSingleValidationError(ErrorOr<NewPayment> result, string expectedCode)
    {
        Assert.True(result.IsError);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public void Create_WithValidInput_ReturnsPayment()
    {
        var result = Create();

        Assert.False(result.IsError);
        var payment = result.Value;
        Assert.Equal(ValidCardNumber, payment.CardNumber);
        Assert.Equal(ValidMonth, payment.ExpiryMonth);
        Assert.Equal(ValidYear, payment.ExpiryYear);
        Assert.Equal(ValidCurrency, payment.Currency);
        Assert.Equal((uint)ValidAmount, payment.Amount);
        Assert.Equal(ValidCvv, payment.Cvv);
    }

    [Theory]
    [InlineData("22224053432488")] // 14 digits
    [InlineData("2222405343248877123")] // 19 digits
    public void Create_WithCardNumberAtLengthBoundary_Succeeds(string cardNumber)
    {
        Assert.False(Create(cardNumber: cardNumber).IsError);
    }

    [Theory]
    [InlineData("2222405343248")] // 13 digits
    [InlineData("22224053432488771234")] // 20 digits
    [InlineData("2222405343248a77")]
    [InlineData("2222 4053 4324 8877")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_WithInvalidCardNumber_ReturnsCardNumberError(string? cardNumber)
    {
        AssertSingleValidationError(Create(cardNumber: cardNumber), "CardNumber");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Create_WithInvalidExpiryMonth_ReturnsExpiryMonthError(int month)
    {
        AssertSingleValidationError(Create(expiryMonth: month), "ExpiryMonth");
    }

    [Fact]
    public void Create_WithCardExpiringThisMonth_Succeeds()
    {
        Assert.False(Create(expiryMonth: Now.Month, expiryYear: Now.Year).IsError);
    }

    [Theory]
    [InlineData(5, 2026)] // last month
    [InlineData(12, 2025)] // last year
    [InlineData(1, 2000)]
    public void Create_WithExpiredCard_ReturnsExpiryError(int month, int year)
    {
        AssertSingleValidationError(Create(expiryMonth: month, expiryYear: year), "Expiry");
    }

    [Fact]
    public void Create_OnLastMomentOfExpiryMonth_Succeeds()
    {
        var lastMoment = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);

        var result = NewPayment.Create(ValidCardNumber, 6, 2026, ValidCurrency, ValidAmount, ValidCvv, lastMoment);

        Assert.False(result.IsError);
    }

    [Fact]
    public void Create_OnFirstDayAfterExpiryMonth_ReturnsExpiryError()
    {
        var nextMonth = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = NewPayment.Create(ValidCardNumber, 6, 2026, ValidCurrency, ValidAmount, ValidCvv, nextMonth);

        AssertSingleValidationError(result, "Expiry");
    }

    [Fact]
    public void Create_InDecember_AcceptsJanuaryOfNextYear()
    {
        var december = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var result = NewPayment.Create(ValidCardNumber, 1, 2027, ValidCurrency, ValidAmount, ValidCvv, december);

        Assert.False(result.IsError);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000)]
    [InlineData(int.MaxValue)]
    public void Create_WithOutOfRangeExpiryYear_ReturnsErrorInsteadOfThrowing(int year)
    {
        var result = Create(expiryYear: year);

        Assert.True(result.IsError);
        Assert.All(result.Errors, e => Assert.Equal(ErrorType.Validation, e.Type));
    }

    [Fact]
    public void Create_WithMaxRepresentableExpiry_Succeeds()
    {
        Assert.False(Create(expiryMonth: 12, expiryYear: 9999).IsError);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    public void Create_WithAllowedCurrency_Succeeds(string currency)
    {
        Assert.False(Create(currency: currency).IsError);
    }

    [Theory]
    [InlineData("JPY")]
    [InlineData("gbp")]
    [InlineData("GBPX")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_WithInvalidCurrency_ReturnsCurrencyError(string? currency)
    {
        AssertSingleValidationError(Create(currency: currency), "Currency");
    }

    [Fact]
    public void Create_WithMinimalAmount_Succeeds()
    {
        Assert.Equal(1u, Create(amount: 1).Value.Amount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_WithNonPositiveAmount_ReturnsAmountError(int amount)
    {
        AssertSingleValidationError(Create(amount: amount), "Amount");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    public void Create_WithValidCvv_Succeeds(string cvv)
    {
        Assert.False(Create(cvv: cvv).IsError);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_WithInvalidCvv_ReturnsCvvError(string? cvv)
    {
        AssertSingleValidationError(Create(cvv: cvv), "Cvv");
    }

    [Fact]
    public void Create_WithMultipleInvalidFields_ReturnsAllErrors()
    {
        var result = NewPayment.Create(null, 13, ValidYear, "JPY", 0, "1", Now);

        Assert.True(result.IsError);
        Assert.Equal(
            ["CardNumber", "ExpiryMonth", "Currency", "Amount", "Cvv"],
            result.Errors.Select(e => e.Code));
    }
}
