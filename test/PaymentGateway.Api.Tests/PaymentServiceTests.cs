using System.Net;

using ErrorOr;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using PaymentGateway.Api.Payments;
using PaymentGateway.Api.Payments.Enums;
using PaymentGateway.Api.Payments.Process;
using PaymentGateway.Api.Payments.Process.Banking;

using Refit;

namespace PaymentGateway.Api.Tests;

public class PaymentServiceTests
{
    private readonly Mock<IBankClient> _bankClient = new();
    private readonly PaymentService _sut;

    public PaymentServiceTests()
    {
        _sut = new PaymentService(_bankClient.Object, NullLogger<PaymentService>.Instance);
    }

    private static NewPayment CreateNewPayment(int expiryMonth = 4, int expiryYear = 2030) =>
        NewPayment.Create("2222405343248877", expiryMonth, expiryYear, "GBP", 1050, "123",
            new DateTime(2026, 1, 1)).Value;

    private void SetupBank(HttpStatusCode statusCode, BankPaymentResponse? content = null) =>
        _bankClient
            .Setup(x => x.Process(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(new ApiResponse<BankPaymentResponse>(
                new HttpResponseMessage(statusCode) { RequestMessage = new HttpRequestMessage() },
                content,
                new RefitSettings()));

    private void SetupBankAuthorised(bool isAuthorised) =>
        SetupBank(HttpStatusCode.OK, new BankPaymentResponse { IsAuthorised = isAuthorised, Code = "code" });

    [Theory]
    [InlineData(true, PaymentStatus.Authorized)]
    [InlineData(false, PaymentStatus.Declined)]
    public async Task ProcessAsync_MapsBankDecisionToStatus(bool isAuthorised, PaymentStatus expected)
    {
        SetupBankAuthorised(isAuthorised);

        var result = await _sut.ProcessAsync(CreateNewPayment(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(expected, result.Value.Status);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsPaymentWithMaskedCardAndRequestDetails()
    {
        SetupBankAuthorised(true);

        var result = await _sut.ProcessAsync(CreateNewPayment(), CancellationToken.None);

        var payment = result.Value;
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal(4, payment.ExpiryMonth);
        Assert.Equal(2030, payment.ExpiryYear);
        Assert.Equal("GBP", payment.Currency);
        Assert.Equal(1050u, payment.Amount);
    }

    [Fact]
    public async Task ProcessAsync_GeneratesUniqueIds()
    {
        SetupBankAuthorised(true);

        var first = await _sut.ProcessAsync(CreateNewPayment(), CancellationToken.None);
        var second = await _sut.ProcessAsync(CreateNewPayment(), CancellationToken.None);

        Assert.NotEqual(first.Value.Id, second.Value.Id);
    }

    [Theory]
    [InlineData(4, 2030, "04/2030")]
    [InlineData(12, 2027, "12/2027")]
    public async Task ProcessAsync_SendsExpectedRequestToBank(int month, int year, string expectedExpiry)
    {
        BankPaymentRequest? sent = null;
        _bankClient
            .Setup(x => x.Process(It.IsAny<BankPaymentRequest>()))
            .Callback<BankPaymentRequest>(r => sent = r)
            .ReturnsAsync(new ApiResponse<BankPaymentResponse>(
                new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = new HttpRequestMessage() },
                new BankPaymentResponse { IsAuthorised = true, Code = "code" },
                new RefitSettings()));

        await _sut.ProcessAsync(CreateNewPayment(month, year), CancellationToken.None);

        Assert.NotNull(sent);
        Assert.Equal("2222405343248877", sent!.CardNumber);
        Assert.Equal(expectedExpiry, sent.ExpiryDate);
        Assert.Equal("GBP", sent.Currency);
        Assert.Equal(1050u, sent.Amount);
        Assert.Equal("123", sent.Cvv);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, ErrorType.Failure)]
    [InlineData(HttpStatusCode.BadRequest, ErrorType.Validation)]
    [InlineData(HttpStatusCode.InternalServerError, ErrorType.Unexpected)]
    [InlineData(HttpStatusCode.Unauthorized, ErrorType.Unexpected)]
    public async Task ProcessAsync_WhenBankFails_ReturnsMappedError(HttpStatusCode statusCode, ErrorType expected)
    {
        SetupBank(statusCode);

        var result = await _sut.ProcessAsync(CreateNewPayment(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(expected, result.FirstError.Type);
    }

    [Fact]
    public async Task ProcessAsync_WhenBankReturnsEmptyBody_ReturnsUnexpectedError()
    {
        SetupBank(HttpStatusCode.OK);

        var result = await _sut.ProcessAsync(CreateNewPayment(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Unexpected, result.FirstError.Type);
        Assert.Equal("Bank.EmptyResponse", result.FirstError.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAsync_ReturnsProcessedPayment(bool isAuthorised)
    {
        SetupBankAuthorised(isAuthorised);
        var processed = await _sut.ProcessAsync(CreateNewPayment(), CancellationToken.None);

        var retrieved = await _sut.GetAsync(processed.Value.Id, CancellationToken.None);

        Assert.False(retrieved.IsError);
        Assert.Equal(processed.Value, retrieved.Value);
    }

    [Fact]
    public async Task GetAsync_WithUnknownId_ReturnsNotFound()
    {
        var result = await _sut.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }
}
