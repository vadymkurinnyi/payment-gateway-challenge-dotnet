using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Payments;
using PaymentGateway.Api.Payments.Enums;
using PaymentGateway.Api.Payments.Process.Banking;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private readonly Random _random = new();

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var payment = new Payment
        {
            Id = paymentId,
            Status = PaymentStatus.Authorized,
            ExpiryYear = _random.Next(2023, 2030),
            ExpiryMonth = _random.Next(1, 12),
            Amount = (uint)_random.Next(1, 10000),
            CardNumberLastFour = _random.Next(1111, 9999).ToString(),
            Currency = "GBP"
        };

        var mockBankClient = new Mock<IBankClient>();
        var mockLogger = new Mock<ILogger<PaymentService>>();
        var paymentService = new PaymentService(mockBankClient.Object, mockLogger.Object);

        // Setup mock bank client to return successful response
        var bankResponse = new BankPaymentResponse
        {
            IsAuthorised = true,
            Code = "auth123"
        };

        var apiResponse = new ApiResponse<BankPaymentResponse>(
            new HttpResponseMessage(HttpStatusCode.OK),
            bankResponse,
            new RefitSettings());

        mockBankClient.Setup(x => x.Process(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(apiResponse);

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(paymentService);
            }))
            .CreateClient();

        // First create a payment to retrieve later
        var createRequest = new
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount,
            Cvv = "123"
        };
        var createResponse = await client.PostAsJsonAsync("/api/payments", createRequest);
        var createdPayment = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(createdPayment);

        // Act
        var response = await client.GetAsync($"/api/payments/{createdPayment!.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(createdPayment!.Id, paymentResponse!.Id);
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_WithEvenCardNumber_ReturnsDeclined()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var mockLogger = new Mock<ILogger<PaymentService>>();
        var paymentService = new PaymentService(mockBankClient.Object, mockLogger.Object);

        var bankResponse = new BankPaymentResponse
        {
            IsAuthorised = false,
            Code = "decline123"
        };

        var apiResponse = new ApiResponse<BankPaymentResponse>(
            new HttpResponseMessage(HttpStatusCode.OK),
            bankResponse,
            new RefitSettings());

        mockBankClient.Setup(x => x.Process(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(apiResponse);

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(paymentService);
            }))
            .CreateClient();

        var request = new
        {
            CardNumber = "2222405343248112", // Even ending
            ExpiryMonth = 12,
            ExpiryYear = 2027,
            Currency = "USD",
            Amount = 1000,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatus.Declined, payment!.Status);
    }

    [Fact]
    public async Task ProcessPayment_WithCardEndingInZero_Returns500()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var mockLogger = new Mock<ILogger<PaymentService>>();
        var paymentService = new PaymentService(mockBankClient.Object, mockLogger.Object);

        var apiResponse = new ApiResponse<BankPaymentResponse>(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            null,
            new RefitSettings());

        mockBankClient.Setup(x => x.Process(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(apiResponse);

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(paymentService);
            }))
            .CreateClient();

        var request = new
        {
            CardNumber = "2222405343248110", // Ending in 0
            ExpiryMonth = 12,
            ExpiryYear = 2027,
            Currency = "EUR",
            Amount = 1000,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Theory]
    [InlineData("123")] // Too short
    [InlineData("12345678901234567890")] // Too long
    [InlineData("123456789012ab")] // Contains letters
    [InlineData(null)] // Null
    public async Task ProcessPayment_WithInvalidCardNumber_ReturnsBadRequest(string? cardNumber)
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new
        {
            CardNumber = cardNumber,
            ExpiryMonth = 12,
            ExpiryYear = 2027,
            Currency = "USD",
            Amount = 1000,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)] // Too low
    [InlineData(13)] // Too high
    public async Task ProcessPayment_WithInvalidExpiryMonth_ReturnsBadRequest(int month)
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = month,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 1000,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_WithExpiredCard_ReturnsBadRequest()
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 1,
            ExpiryYear = 2020, // Expired
            Currency = "USD",
            Amount = 1000,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("12")] // Too short
    [InlineData("12345")] // Too long
    [InlineData("abc")] // Non-numeric
    [InlineData(null)] // Null
    public async Task ProcessPayment_WithInvalidCvv_ReturnsBadRequest(string? cvv)
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2027,
            Currency = "EUR",
            Amount = 1000,
            Cvv = cvv
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("123")] // Valid 3 digits
    [InlineData("1234")] // Valid 4 digits
    public async Task ProcessPayment_WithValidCvv_ReturnsSuccess(string cvv)
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var mockLogger = new Mock<ILogger<PaymentService>>();
        var paymentService = new PaymentService(mockBankClient.Object, mockLogger.Object);

        var bankResponse = new BankPaymentResponse
        {
            IsAuthorised = true,
            Code = "auth123"
        };

        var apiResponse = new ApiResponse<BankPaymentResponse>(
            new HttpResponseMessage(HttpStatusCode.OK),
            bankResponse,
            new RefitSettings());

        mockBankClient.Setup(x => x.Process(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(apiResponse);

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(paymentService);
            }))
            .CreateClient();

        var request = new
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2027,
            Currency = "USD",
            Amount = 1000,
            Cvv = cvv
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("JPY")] // Not in allowed list
    [InlineData("CHF")] // Not in allowed list
    [InlineData(null)] // Null
    [InlineData("")] // Empty
    public async Task ProcessPayment_WithInvalidCurrency_ReturnsBadRequest(string? currency)
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2027,
            Currency = currency,
            Amount = 1000,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    public async Task ProcessPayment_WithValidCurrency_ReturnsSuccess(string currency)
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var mockLogger = new Mock<ILogger<PaymentService>>();
        var paymentService = new PaymentService(mockBankClient.Object, mockLogger.Object);

        var bankResponse = new BankPaymentResponse
        {
            IsAuthorised = true,
            Code = "auth123"
        };

        var apiResponse = new ApiResponse<BankPaymentResponse>(
            new HttpResponseMessage(HttpStatusCode.OK),
            bankResponse,
            new RefitSettings());

        mockBankClient.Setup(x => x.Process(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(apiResponse);

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(paymentService);
            }))
            .CreateClient();

        var request = new
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2027,
            Currency = currency,
            Amount = 1000,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(0)] // Zero
    [InlineData(-100)] // Negative
    public async Task ProcessPayment_WithInvalidAmount_ReturnsBadRequest(int amount)
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        var request = new
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2027,
            Currency = "USD",
            Amount = amount,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_ReturnsLastFourDigitsOnly()
    {
        // Arrange
        var mockBankClient = new Mock<IBankClient>();
        var mockLogger = new Mock<ILogger<PaymentService>>();
        var paymentService = new PaymentService(mockBankClient.Object, mockLogger.Object);

        var bankResponse = new BankPaymentResponse
        {
            IsAuthorised = true,
            Code = "auth123"
        };

        var apiResponse = new ApiResponse<BankPaymentResponse>(
            new HttpResponseMessage(HttpStatusCode.OK),
            bankResponse,
            new RefitSettings());

        mockBankClient.Setup(x => x.Process(It.IsAny<BankPaymentRequest>()))
            .ReturnsAsync(apiResponse);

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(paymentService);
            }))
            .CreateClient();

        var request = new
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 12,
            ExpiryYear = 2027,
            Currency = "GBP",
            Amount = 1000,
            Cvv = "123"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payment);
        Assert.Equal("8877", payment!.CardNumberLastFour);
    }
}