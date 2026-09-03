using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Telemetry;
using MiniBank.AI.Tools;
using OllamaSharp;

namespace MiniBank.AI.Agents;

/// <summary>
/// A single-agent banking assistant. The LLM decides when to call tools;
/// there is no workflow graph.
/// </summary>
public sealed class BankingAgent
{
    private const string Endpoint = "http://localhost:11434";
    private const string ModelName = "qwen2.5:1.5b-instruct";

    public BankingAgent(
        AccountTools accountTools,
        CustomerTools customerTools,
        TransactionTools transactionTools,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(accountTools);
        ArgumentNullException.ThrowIfNull(customerTools);
        ArgumentNullException.ThrowIfNull(transactionTools);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var toolLogger = loggerFactory.CreateLogger<TracingAIFunction>();

        IChatClient ollama = new OllamaApiClient(new Uri(Endpoint), ModelName);
        IChatClient chatClient = ollama
            .AsBuilder()
            .Use(inner => new TracingChatClient(
                inner,
                loggerFactory.CreateLogger<TracingChatClient>(),
                ModelName))
            .UseLogging(loggerFactory)
            .Build();

        var agent = chatClient.AsAIAgent(
            instructions:
                """
                You are a helpful MiniBank assistant.
                Always use tools to answer banking questions. Never invent balances, totals, or transactions.
                Choose the matching tool:
                - Account balance → get_balance
                - Customer's total money across their accounts → get_owner_total_balance
                - How many deposits a customer has made → count_deposits_by_owner
                - Total value of all accounts in the bank → get_total_value
                - Account with the highest balance → get_highest_balance_account
                - Deposits made to a specific account → get_deposits
                - List a customer's accounts → find_accounts_by_owner
                - Full history of an account → get_account_history
                Format currency in GBP, for example £1,532.42.
                Keep answers brief.
                """,
            name: "BankingAgent",
            description: "Answers MiniBank account questions by calling banking tools.",
            tools:
            [
                Tool(accountTools.GetBalanceAsync, "get_balance", toolLogger),
                Tool(accountTools.FindAccountsByOwnerAsync, "find_accounts_by_owner", toolLogger),
                Tool(accountTools.GetTotalValueAsync, "get_total_value", toolLogger),
                Tool(accountTools.GetHighestBalanceAccountAsync, "get_highest_balance_account", toolLogger),
                Tool(customerTools.GetOwnerTotalBalanceAsync, "get_owner_total_balance", toolLogger),
                Tool(customerTools.CountDepositsByOwnerAsync, "count_deposits_by_owner", toolLogger),
                Tool(transactionTools.GetDepositsAsync, "get_deposits", toolLogger),
                Tool(transactionTools.GetAccountHistoryAsync, "get_account_history", toolLogger)
            ],
            loggerFactory: loggerFactory);

        Agent = new TracingAgent(
            agent.AsBuilder()
                .UseOpenTelemetry(
                    MiniBankActivitySources.Agent.Name,
                    otel => otel.EnableSensitiveData = true)
                .Build(),
            loggerFactory.CreateLogger<TracingAgent>(),
            ModelName);
    }

    public AIAgent Agent { get; }

    private static AIFunction Tool(Delegate method, string name, ILogger logger)
        => new TracingAIFunction(AIFunctionFactory.Create(method, name: name), logger);
}
