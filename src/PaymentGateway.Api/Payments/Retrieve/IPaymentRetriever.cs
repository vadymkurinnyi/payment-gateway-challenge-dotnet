using ErrorOr;

namespace PaymentGateway.Api.Payments.Retrieve;

public interface IPaymentRetriever
{
    Task<ErrorOr<Payment>> RetrieveAsync(Guid id, CancellationToken ct);
}