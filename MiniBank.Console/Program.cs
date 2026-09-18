using MiniBank.Domain.Models;
using Banking.Repositories;
using Banking.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Auth;
using MiniBank.AI.Agents;
using MiniBank.AI.Telemetry;
using MiniBank.AI.Tools;
using MiniBank.AI.Workflows;
using Serilog;
using System.Threading.Tasks;
using System;
using System.IO;

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

    var identity = new InMemoryCustomerIdentityService();
    var bank = await CreateBankAsync();
    var principal = await PromptForLoginAsync(identity);
    if (principal is null)
    {
        logger.LogWarning("MiniBank console login failed.");
        Console.WriteLine("MiniBank: Login failed.");
        return;
    }

    logger.LogInformation("Authenticated console customer: {Customer}", principal.Owner);

    var customerBank = bank.ForCustomer(principal.Owner);
    var workflow = BankingWorkflow.Create(
        new AccountTools(customerBank),
        new CustomerTools(customerBank),
        new TransactionTools(customerBank),
        new OperationTools(customerBank),
        OllamaOptions.FromConfiguration(builder.Configuration),
        loggerFactory: loggerFactory);

    PrintWelcome(principal.Owner);

    while (true)
    {
        Console.Write("You: ");
        var question = Console.ReadLine();
        if (question is null)
            break;

        question = question.Trim();
        if (question.Length == 0)
            continue;
        if (IsQuit(question))
            break;

        logger.LogInformation(
            "Sending question to MiniBank workflow for {Customer}: {Question}",
            principal.Owner,
            question);

        try
        {
            var result = await workflow.RunDetailedAsync(question);
            if (result.ExecutorIds.Count > 0)
                Console.WriteLine($"[{string.Join(" → ", result.ExecutorIds)}]");
            Console.WriteLine($"MiniBank: {result.Output}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow failed for question: {Question}", question);
            Console.WriteLine($"MiniBank: Sorry, something went wrong. {ex.Message}");
        }

        Console.WriteLine();
    }

    await app.StopAsync();
}
finally
{
    await Log.CloseAndFlushAsync();
}

static void PrintWelcome(string customer)
{
    Console.WriteLine("MiniBank assistant. Type a question, or quit to exit.");
    Console.WriteLine($"Signed in as {customer}. You can only see and debit your own accounts.");
    Console.WriteLine();
}

static async Task<CustomerPrincipal?> PromptForLoginAsync(ICustomerIdentityService identity)
{
    Console.WriteLine("Sign in with a demo MiniBank user.");
    Console.Write("Username: ");
    var username = Console.ReadLine();
    Console.Write("Password: ");
    var password = Console.ReadLine();

    if (username is null || password is null)
        return null;

    var result = await identity.SignInAsync(username, password);
    return result?.Principal;
}

static bool IsQuit(string question)
    => question.Equals("quit", StringComparison.OrdinalIgnoreCase)
        || question.Equals("exit", StringComparison.OrdinalIgnoreCase)
        || question.Equals("q", StringComparison.OrdinalIgnoreCase)
        || question.Equals("bye", StringComparison.OrdinalIgnoreCase);

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

file sealed class NoOpAuditLogger : IAuditLogger
{
    public Task LogAsync(long accountNumber, AccountAction action, decimal amount)
        => Task.CompletedTask;
}
