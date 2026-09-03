using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Banking.Repositories;
using Banking.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Telemetry;
using MiniBank.AI.Tools;
using MiniBank.AI.Workflows;
using MiniBank.Domain.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(dispose: false);
builder.Services.AddMiniBankTracing(builder.Configuration, "MiniBank.Api");

try
{
    var app = builder.Build();

    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("MiniBank.Api");
    var bank = await CreateBankAsync();
    var workflow = BankingWorkflow.Create(
        new AccountTools(bank),
        new CustomerTools(bank),
        new TransactionTools(bank),
        new OperationTools(bank),
        loggerFactory: loggerFactory);

    app.MapGet("/health", () => Results.Ok());

    app.MapPost("/chat", async (ChatRequest request, CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Results.BadRequest(new ErrorResponse("Question must not be empty."));

        var question = request.Question.Trim();
        logger.LogInformation("Sending question to MiniBank workflow: {Question}", question);

        try
        {
            var result = await workflow.RunDetailedAsync(question, cancellationToken);
            return Results.Ok(new ChatResponse(result.Output, result.ExecutorIds));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Workflow failed for question: {Question}", question);
            return Results.Problem(
                title: "MiniBank workflow failed.",
                detail: "The request could not be completed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    });

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

file sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly Dictionary<long, Account> _accounts = new();

    public Task AddAccountAsync(Account account)
    {
        _accounts[account.AccountNumber] = account;
        return Task.CompletedTask;
    }

    public Task<Account?> FindByIdAsync(long accountNumber)
        => Task.FromResult(_accounts.TryGetValue(accountNumber, out var account) ? account : null);

    public Task<List<Account>> AllAsync()
        => Task.FromResult(_accounts.Values.ToList());

    public Task<List<Account>> FindByOwnerAsync(string owner)
        => Task.FromResult(_accounts.Values
            .Where(account => account.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase))
            .ToList());

    public Task UpdateAccountAsync(Account account)
    {
        _accounts[account.AccountNumber] = account;
        return Task.CompletedTask;
    }
}

file sealed class NoOpAuditLogger : IAuditLogger
{
    public Task LogAsync(long accountNumber, AccountAction action, decimal amount)
        => Task.CompletedTask;
}

file sealed record ChatRequest(string? Question);

file sealed record ChatResponse(string Output, IReadOnlyList<string> ExecutorIds);

file sealed record ErrorResponse(string Error);
