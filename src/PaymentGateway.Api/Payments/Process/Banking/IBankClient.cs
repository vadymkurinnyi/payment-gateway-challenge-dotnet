using Refit;
namespace PaymentGateway.Api.Payments.Process.Banking;

public interface IBankClient
{
    [Post("/payments")]
    public Task<ApiResponse<BankPaymentResponse>> Process(BankPaymentRequest request);
}