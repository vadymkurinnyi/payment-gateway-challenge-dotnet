# PaymentGateway.Api

## Architecture

The code structure has a feature-oriented approach. The `Payments` directory has 2 main sections: `Process` and `Retrieve`. These have specific abstractions: `IPaymentProcessor` and `IPaymentRetriever`. In a real system, it would have separate implementations; in this task, I decided to implement it in `PaymentService`. It holds the payment in memory as a dictionary, with `paymentId` as the key.

- [Refit](https://github.com/reactiveui/refit) library is used for HTTP calls, which makes it easier to maintain.
- [ErrorOr](https://github.com/amantinband/error-or) library is used for treating errors as values.

## Things to implement/improve

- Add an exception handler (just in case of errors outside the business logic)
- Add retries and a circuit breaker to the banking API
- Consider adding metrics
- Have health checks for the banking API
- Use structured logging (such as Serilog)
- Last but not least, have DI registrations in separate places and call them in `Program.cs` as `services.AddPaymentProcessor()` and `services.AddPaymentRetriever()`
