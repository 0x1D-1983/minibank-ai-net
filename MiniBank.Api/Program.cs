using MiniBank.Domain.Models;
using Banking.Repositories;
using Banking.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Agents;
using MiniBank.Api;
using MiniBank.Auth;
using MiniBank.AI.Telemetry;
using MiniBank.AI.Tools;
using MiniBank.AI.Workflows;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(dispose: false);
builder.Services.AddMiniBankTracing(builder.Configuration, "MiniBank.Api", aspNetCore: true);
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("MiniBank.Api");

Bank bank;
IAuthenticationService authService;
OllamaOptions ollamaOptions;
try
{
    bank = await CreateBankAsync();
    authService = new InMemoryAuthenticationService();
    ollamaOptions = OllamaOptions.FromConfiguration(app.Configuration);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Failed to initialise MiniBank");
    await Log.CloseAndFlushAsync();
    throw;
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/login", async (LoginRequest request, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Username and password are required" });
    }

    var token = await authService.AuthenticateAsync(request.Username, request.Password, cancellationToken);
    if (token is null)
    {
        logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
        return Results.Json(new { error = "Invalid credentials" }, statusCode: 401);
    }

    var principal = await authService.ValidateTokenAsync(token, cancellationToken);
    logger.LogInformation("Customer authenticated: {Customer}", principal?.Owner);

    return Results.Ok(new LoginResponse(token));
});

app.MapPost("/chat", async (ChatRequest request, HttpContext context, CancellationToken cancellationToken) =>
{
    var principal = await AuthenticateRequestAsync(context, authService, cancellationToken);
    if (principal is null)
    {
        return Results.Json(new { error = "Unauthorized" }, statusCode: 401);
    }

    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new { error = "Question cannot be empty" });
    }

    logger.LogInformation(
        "Sending question to MiniBank workflow for {Customer}: {Question}",
        principal.Owner,
        request.Question);

    var authorizedBank = new AuthorizedBank(bank, principal.Owner);
    var workflow = BankingWorkflow.Create(
        new AccountTools(authorizedBank, loggerFactory.CreateLogger<AccountTools>()),
        new CustomerTools(authorizedBank, loggerFactory.CreateLogger<CustomerTools>()),
        new TransactionTools(authorizedBank, loggerFactory.CreateLogger<TransactionTools>()),
        new OperationTools(authorizedBank, loggerFactory.CreateLogger<OperationTools>()),
        ollamaOptions,
        loggerFactory: loggerFactory);

    try
    {
        var result = await workflow.RunDetailedAsync(request.Question, cancellationToken);
        return Results.Ok(new ChatResponse(
            result.Output,
            result.ExecutorIds,
            result.Result.Success,
            result.Result.ErrorCode,
            result.Result.Retryable));
    }
    catch (Exception ex) when (ex is not OperationCanceledException && AccountDenial.TryGet(ex, out var denial))
    {
        logger.LogWarning(denial, "Account not found for question: {Question}", request.Question);
        return Results.Problem(
            title: "Not found",
            detail: denial.Message,
            statusCode: StatusCodes.Status404NotFound);
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
catch (Exception ex)
{
    Log.Fatal(ex, "MiniBank.Api terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task<CustomerPrincipal?> AuthenticateRequestAsync(
    HttpContext context,
    IAuthenticationService authService,
    CancellationToken cancellationToken)
{
    var authHeader = context.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authHeader))
        return null;

    const string bearerPrefix = "Bearer ";
    if (!authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        return null;

    var token = authHeader[bearerPrefix.Length..].Trim();
    if (string.IsNullOrWhiteSpace(token))
        return null;

    return await authService.ValidateTokenAsync(token, cancellationToken);
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

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string Token);

public sealed record ChatRequest(string Question);

public sealed record ChatResponse(
    string Output,
    IReadOnlyList<string> ExecutorIds,
    bool Success,
    string? ErrorCode,
    bool Retryable);

file sealed class NoOpAuditLogger : IAuditLogger
{
    public Task LogAsync(long accountNumber, AccountAction action, decimal amount, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public partial class Program { }
