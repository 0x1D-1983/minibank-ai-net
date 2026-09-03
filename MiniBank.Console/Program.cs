using Banking.Domain.Models;
using Banking.Repositories;
using Banking.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Agents;
using MiniBank.AI.Telemetry;
using MiniBank.AI.Tools;
using Serilog;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(dispose: false);
builder.Services.AddMiniBankTracing(builder.Configuration, "MiniBank.Console");

try
{
    using var app = builder.Build();
    await app.StartAsync();

    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("MiniBank.Console");

    var bank = await CreateBankAsync();

    AIAgent agent = new BankingAgent(
        new AccountTools(bank),
        new CustomerTools(bank),
        new TransactionTools(bank),
        loggerFactory).Agent;

    AgentSession session = await agent.CreateSessionAsync();

    var question = "What is the balance of account 20001?";
    logger.LogInformation("Sending question to BankingAgent: {Question}", question);

    Console.WriteLine(await agent.RunAsync(question, session));

    await app.StopAsync();
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
