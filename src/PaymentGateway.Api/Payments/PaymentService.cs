using System.Collections.Concurrent;
using System.Net;

using ErrorOr;

using PaymentGateway.Api.Payments.Enums;
using PaymentGateway.Api.Payments.Process;
using PaymentGateway.Api.Payments.Process.Banking;
using PaymentGateway.Api.Payments.Retrieve;

namespace PaymentGateway.Api.Payments;

public class PaymentService(IBankClient bankClient, ILogger<PaymentService> logger) : IPaymentProcessor, IPaymentRetriever
{
    private readonly ConcurrentDictionary<Guid, (Payment, BankPaymentResponse)> _storage = new();
    public async Task<ErrorOr<Payment>> ProcessAsync(NewPayment newPayment, CancellationToken ct)
    {
        var bankRequest = new BankPaymentRequest
        {
            CardNumber = newPayment.CardNumber,
            ExpiryDate = $"{newPayment.ExpiryMonth}/{newPayment.ExpiryYear}",
            Currency = newPayment.Currency,
            Amount = newPayment.Amount,
            Cvv = newPayment.Cvv
        };
        var apiBankResponse = await bankClient.Process(bankRequest);

        if (!apiBankResponse.IsSuccessful)
        {
            logger.LogError("Bank request failed with status {StatusCode} for card ending {CardEnding}",
                apiBankResponse.StatusCode,
                newPayment.CardNumber[^4..]);

            return apiBankResponse.StatusCode switch
            {
                HttpStatusCode.ServiceUnavailable => Error.Failure("Bank.Unavailable",
                    "Banking service is temporarily unavailable"),
                HttpStatusCode.BadRequest => Error.Validation("Bank.BadRequest",
                    "Invalid request to bank"),
                _ => Error.Unexpected("Bank.Error", "Unexpected bank error")
            };
        }

        if (apiBankResponse.Content is null)
        {
            logger.LogError("Bank response content is null for card ending {CardEnding}",
                newPayment.CardNumber[^4..]);
            return Error.Unexpected("Bank.EmptyResponse", "Bank returned empty response");
        }

        var id = Guid.NewGuid();
        var status = apiBankResponse.Content.IsAuthorised ? PaymentStatus.Authorized : PaymentStatus.Declined;

        logger.LogInformation("Payment {PaymentId} processed with status {Status} for card ending {CardEnding}",
            id, status, newPayment.CardNumber[^4..]);

        var payment = new Payment
        {
            Id = id,
            Status = status,
            CardNumberLastFour = newPayment.CardNumber[^4..],
            Amount = newPayment.Amount,
            Currency = newPayment.Currency,
            ExpiryMonth = newPayment.ExpiryMonth,
            ExpiryYear = newPayment.ExpiryYear
        };
        _storage.TryAdd(id, (payment, apiBankResponse.Content));
        return payment;

    }

    public async Task<ErrorOr<Payment>> RetrieveAsync(Guid id, CancellationToken ct)
    {
        if (!_storage.TryGetValue(id, out (Payment Payment, BankPaymentResponse) value))
            return Error.NotFound();
        return value.Payment;
    }
}