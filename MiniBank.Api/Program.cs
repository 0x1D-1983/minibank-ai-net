using MiniBank.Domain.Models;
using Banking.Repositories;
using Banking.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
BankingWorkflow workflow;
try
{
    bank = await CreateBankAsync();
    workflow = BankingWorkflow.Create(
        new AccountTools(bank),
        new CustomerTools(bank),
        new TransactionTools(bank),
        new OperationTools(bank),
        loggerFactory: loggerFactory);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Failed to initialise MiniBank");
    await Log.CloseAndFlushAsync();
    throw;
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/chat", async (ChatRequest request, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest(new { error = "Question cannot be empty" });
    }

    logger.LogInformation("Sending question to MiniBank workflow: {Question}", request.Question);

    try
    {
        var result = await workflow.RunDetailedAsync(request.Question, cancellationToken);
        return Results.Ok(new ChatResponse(result.Output, result.ExecutorIds));
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

public sealed record ChatRequest(string Question);

public sealed record ChatResponse(string Output, IReadOnlyList<string> ExecutorIds);

file sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly Dictionary<long, Account> _accounts = new();
    private readonly object _sync = new();

    public Task AddAccountAsync(Account account)
    {
        lock (_sync)
            _accounts[account.AccountNumber] = account;
        return Task.CompletedTask;
    }

    public Task<Account?> FindByIdAsync(long accountNumber)
    {
        lock (_sync)
            return Task.FromResult(_accounts.TryGetValue(accountNumber, out var account) ? account : null);
    }

    public Task<List<Account>> AllAsync()
    {
        lock (_sync)
            return Task.FromResult(_accounts.Values.ToList());
    }

    public Task<List<Account>> FindByOwnerAsync(string owner)
    {
        lock (_sync)
        {
            return Task.FromResult(_accounts.Values
                .Where(account => account.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase))
                .ToList());
        }
    }

    public Task UpdateAccountAsync(Account account)
    {
        lock (_sync)
            _accounts[account.AccountNumber] = account;
        return Task.CompletedTask;
    }
}

file sealed class NoOpAuditLogger : IAuditLogger
{
    public Task LogAsync(long accountNumber, AccountAction action, decimal amount)
        => Task.CompletedTask;
}
