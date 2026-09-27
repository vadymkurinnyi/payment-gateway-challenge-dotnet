using ErrorOr;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Payments;
using PaymentGateway.Api.Payments.Enums;
using PaymentGateway.Api.Payments.Process;
using PaymentGateway.Api.Payments.Retrieve;

namespace PaymentGateway.Api.Controllers;

[Route("api/payments")]
[ApiController]
public class PaymentsController(
    IPaymentProcessor paymentProcessor,
    IPaymentRetriever paymentRetriever,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<Results<Ok<PaymentResponse>, NotFound, ProblemHttpResult>>
        GetPaymentAsync(Guid id, CancellationToken ct)
    {
        var result = await paymentRetriever.GetAsync(id, ct);

        return result.Match<Results<Ok<PaymentResponse>, NotFound, ProblemHttpResult>>(
            payment => TypedResults.Ok(PaymentResponse.From(payment)),
            errors => errors.First().Type switch
            {
                ErrorType.NotFound => TypedResults.NotFound(),
                _ => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError)
            });
    }

    [HttpPost("")]
    public async Task<Results<Ok<PaymentResponse>, BadRequest<ValidationProblemDetails>, ProblemHttpResult>>
        ProcessPaymentAsync(PaymentRequest request, CancellationToken ct)
    {
        var newPayment = NewPayment.Create(
            request.CardNumber,
            request.ExpiryMonth,
            request.ExpiryYear,
            request.Currency,
            request.Amount,
            request.Cvv,
            timeProvider.GetUtcNow().DateTime);

        if (newPayment.IsError)
        {
            var problem = CreateValidationProblem(newPayment.Errors);
            problem.Extensions["status"] = nameof(PaymentStatus.Rejected);
            return TypedResults.BadRequest(problem);
        }

        var result = await paymentProcessor.ProcessAsync(newPayment.Value, ct);

        return result.Match<Results<Ok<PaymentResponse>, BadRequest<ValidationProblemDetails>, ProblemHttpResult>>(
            payment => TypedResults.Ok(PaymentResponse.From(payment)),
            errors => errors[0].Type switch
            {
                ErrorType.Validation => TypedResults.BadRequest(CreateValidationProblem(errors)),
                _ => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError)
            });
    }

    private static ValidationProblemDetails CreateValidationProblem(IEnumerable<Error> errors) =>
        new(errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
}