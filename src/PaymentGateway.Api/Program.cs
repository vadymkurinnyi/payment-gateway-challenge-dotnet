using PaymentGateway.Api.Payments;
using PaymentGateway.Api.Payments.Process;
using PaymentGateway.Api.Payments.Process.Banking;
using PaymentGateway.Api.Payments.Retrieve;
using Refit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Refit client for bank simulator
var bankBaseUrl = builder.Configuration["BankSimulator:BaseUrl"] ?? "http://localhost:8080";
builder.Services.AddRefitClient<IBankClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(bankBaseUrl));

builder.Services.AddSingleton<PaymentService>();
builder.Services.AddSingleton<IPaymentProcessor>(sp => sp.GetRequiredService<PaymentService>());
builder.Services.AddSingleton<IPaymentRetriever>(sp => sp.GetRequiredService<PaymentService>());
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
