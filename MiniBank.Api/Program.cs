using MiniBank.Domain.Models;
using Banking.Repositories;
using Banking.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Auth;
using MiniBank.AI.Agents;
using MiniBank.AI.Telemetry;
using MiniBank.AI.Tools;
using MiniBank.AI.Workflows;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(dispose: false);
builder.Services.AddMiniBankTracing(builder.Configuration, "MiniBank.Api");

var app = builder.Build();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("MiniBank.Api");

Bank bank;
ICustomerIdentityService identity = new InMemoryCustomerIdentityService();
try
{
    bank = await CreateBankAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Failed to initialise MiniBank");
    await Log.CloseAndFlushAsync();
    throw;
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/login", async (ApiLoginRequest request, CancellationToken cancellationToken) =>
{
    var result = await identity.SignInAsync(request.Username, request.Password, cancellationToken);
    if (result is null)
        return Results.Unauthorized();

    logger.LogInformation("Authenticated API customer: {Customer}", result.Principal.Owner);
    return Results.Ok(new ApiLoginResponse(result.Token, result.Principal.Owner));
});

app.MapPost("/chat", async (HttpRequest httpRequest, ApiChatRequest request, CancellationToken cancellationToken) =>
{
    var principal = await AuthenticateAsync(identity, httpRequest, cancellationToken);
    if (principal is null)
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new { error = "Question cannot be empty" });
    }

    var workflow = CreateWorkflow(bank.ForCustomer(principal.Owner), loggerFactory, app.Configuration);

    logger.LogInformation(
        "Sending question to MiniBank workflow for {Customer}: {Question}",
        principal.Owner,
        request.Question);

    try
    {
        var result = await workflow.RunDetailedAsync(request.Question, cancellationToken);
        return Results.Ok(new ApiChatResponse(result.Output, result.ExecutorIds));
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        logger.LogError(ex, "Workflow failed for question: {Question}", request.Question);
        return Results.Problem(
            title: "Workflow error",
            detail: "An error occurred while processing your question.",
            statusCode: 500);
    }
});

try
{
    await app.RunAsync();
}
finally
{
    await Log.CloseAndFlushAsync();
}

static BankingWorkflow CreateWorkflow(
    CustomerBank customerBank,
    ILoggerFactory loggerFactory,
    IConfiguration configuration)
    => BankingWorkflow.Create(
        new AccountTools(customerBank),
        new CustomerTools(customerBank),
        new TransactionTools(customerBank),
        new OperationTools(customerBank),
        OllamaOptions.FromConfiguration(configuration),
        loggerFactory: loggerFactory);

static async Task<CustomerPrincipal?> AuthenticateAsync(
    ICustomerIdentityService identity,
    HttpRequest request,
    CancellationToken cancellationToken)
{
    var token = ReadBearerToken(request);
    return token is null ? null : await identity.ValidateTokenAsync(token, cancellationToken);
}

static string? ReadBearerToken(HttpRequest request)
{
    if (!request.Headers.TryGetValue("Authorization", out var authorization))
        return null;

    var header = authorization.FirstOrDefault();
    if (header is null)
        return null;

    const string prefix = "Bearer ";
    return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        ? header[prefix.Length..].Trim()
        : null;
}

static async Task<Bank> CreateBankAsync()
{
    var bank = new Bank(new InMemoryAccountRepository(), new NoOpAuditLogger());

    await bank.AddAccountAsync(new CurrentAccount("Alice Example", 1234567890, overdraftLimit: 250m));
    await bank.DepositAsync(1234567890, 2_450.00m);

    await bank.AddAccountAsync(new CurrentAccount("John Smith", 10001, overdraftLimit: 500m));
    await bank.DepositAsync(10001, 1_532.42m);

    await bank.AddAccountAsync(new SavingsAccount("John Smith", 10002, interestRate: 0.02m));
    await bank.DepositAsync(10002, 800.00m);

    await bank.AddAccountAsync(new CurrentAccount("Jane Doe", 20001, overdraftLimit: 0m));
    await bank.DepositAsync(20001, 5_000.00m);

    return bank;
}

public sealed record ApiLoginRequest(string Username, string Password);

public sealed record ApiLoginResponse(string Token, string Customer);

public sealed record ApiChatRequest(string Question);

public sealed record ApiChatResponse(string Output, IReadOnlyList<string> ExecutorIds);

file sealed class NoOpAuditLogger : IAuditLogger
{
    public Task LogAsync(long accountNumber, AccountAction action, decimal amount)
        => Task.CompletedTask;
}

public partial class Program;
