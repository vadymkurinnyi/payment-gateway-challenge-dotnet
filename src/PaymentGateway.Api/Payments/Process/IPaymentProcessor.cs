using ErrorOr;

namespace PaymentGateway.Api.Payments.Process;

public interface IPaymentProcessor
{
    Task<ErrorOr<Payment>> ProcessAsync(NewPayment payment, CancellationToken ct);
}