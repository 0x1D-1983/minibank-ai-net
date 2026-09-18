using MiniBank.Domain.Models;
using Banking.Repositories;
using Banking.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Agents;
using MiniBank.AI.Auth;
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

    var authService = new InMemoryAuthenticationService();
    var bank = await CreateBankAsync();

    PrintWelcome();

    var principal = await LoginAsync(authService, (Microsoft.Extensions.Logging.ILogger)logger);
    if (principal is null)
    {
        Console.WriteLine("Login failed. Exiting.");
        await app.StopAsync();
        return;
    }

    logger.LogInformation("Customer authenticated: {Customer}", principal.Owner);
    Console.WriteLine($"Welcome, {principal.Owner}! You can now ask questions about your accounts.");
    Console.WriteLine();

    var authorizedBank = new AuthorizedBank(bank, principal.Owner);
    var workflow = BankingWorkflow.Create(
        new AccountTools(authorizedBank),
        new CustomerTools(authorizedBank),
        new TransactionTools(authorizedBank),
        new OperationTools(authorizedBank),
        OllamaOptions.FromConfiguration(builder.Configuration),
        loggerFactory: loggerFactory);

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

        logger.LogInformation("Sending question to MiniBank workflow: {Question}", question);

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

static void PrintWelcome()
{
    Console.WriteLine("MiniBank assistant.");
    Console.WriteLine("Demo users: alice, john, jane (password: password)");
    Console.WriteLine();
}

static async Task<CustomerPrincipal?> LoginAsync(IAuthenticationService authService, Microsoft.Extensions.Logging.ILogger logger)
{
    const int maxAttempts = 3;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        Console.Write("Username: ");
        var username = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(username))
        {
            Console.WriteLine("Username cannot be empty.");
            continue;
        }

        Console.Write("Password: ");
        var password = ReadPassword();
        Console.WriteLine();

        var token = await authService.AuthenticateAsync(username, password);
        if (token is null)
        {
            logger.LogWarning("Failed login attempt for username: {Username}", username);
            Console.WriteLine($"Invalid credentials. {maxAttempts - attempt} attempts remaining.");
            continue;
        }

        return await authService.ValidateTokenAsync(token);
    }

    return null;
}

static string ReadPassword()
{
    var password = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
            break;
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password.Length--;
            Console.Write("\b \b");
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write('*');
        }
    }
    return password.ToString();
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
