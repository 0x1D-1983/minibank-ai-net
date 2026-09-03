using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        IChatClient? chatClient = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(accountTools);
        ArgumentNullException.ThrowIfNull(customerTools);
        ArgumentNullException.ThrowIfNull(transactionTools);

        loggerFactory ??= NullLoggerFactory.Instance;
        chatClient ??= new OllamaApiClient(new Uri(Endpoint), ModelName);

        var toolLogger = loggerFactory.CreateLogger<TracingAIFunction>();

        chatClient = chatClient
            .AsBuilder()
            .Use(inner => new TracingChatClient(
                inner,
                loggerFactory.CreateLogger<TracingChatClient>(),
                ModelName))
            .UseLogging(loggerFactory)
            .Build();

        var tools = new List<AITool>
        {
            Tool(accountTools.GetBalanceAsync, "get_balance", toolLogger),
            Tool(accountTools.FindAccountsByOwnerAsync, "find_accounts_by_owner", toolLogger),
            Tool(accountTools.GetTotalValueAsync, "get_total_value", toolLogger),
            Tool(accountTools.GetHighestBalanceAccountAsync, "get_highest_balance_account", toolLogger),
            Tool(customerTools.GetOwnerTotalBalanceAsync, "get_owner_total_balance", toolLogger),
            Tool(customerTools.CountDepositsByOwnerAsync, "count_deposits_by_owner", toolLogger),
            Tool(transactionTools.GetDepositsAsync, "get_deposits", toolLogger),
            Tool(transactionTools.GetAccountHistoryAsync, "get_account_history", toolLogger)
        };

        var agent = chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "BankingAgent",
                Description = "Answers MiniBank account questions by calling banking tools.",
                ChatOptions = new ChatOptions
                {
                    Instructions =
                        """
                        You are a helpful MiniBank assistant.
                        Always use tools to answer banking questions. Never invent balances, totals, transactions, or account numbers.

                        Choose the matching tool:
                        - get_balance: ONLY when the user supplied a specific account number. Never guess or invent one.
                        - get_owner_total_balance: how much a named customer has, when no account number was given.
                        - count_deposits_by_owner: how many deposits a named customer has made.
                        - get_total_value: total of every account in the bank.
                        - get_highest_balance_account: which account has the highest balance.
                        - get_deposits: ONLY deposits on a numbered account. Do not use this for full history.
                        - find_accounts_by_owner: list a customer's accounts.
                        - get_account_history: every transaction on a numbered account. Use this for history or "everything that happened".

                        Include account numbers from tool results in your answer.
                        Format currency in GBP, for example £1,532.42.
                        Keep answers brief.
                        """,
                    Temperature = 0f,
                    Tools = tools
                }
            },
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
