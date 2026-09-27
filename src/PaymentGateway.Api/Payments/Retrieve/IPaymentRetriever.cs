using ErrorOr;

namespace PaymentGateway.Api.Payments.Retrieve;

public interface IPaymentRetriever
{
    Task<ErrorOr<Payment>> GetAsync(Guid id, CancellationToken ct);
}