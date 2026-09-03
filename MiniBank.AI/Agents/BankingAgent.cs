using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniBank.AI.Telemetry;
using MiniBank.AI.Tools;

namespace MiniBank.AI.Agents;

/// <summary>
/// Query specialist used by the workflow's Query Executor. It only has READ tools;
/// deposits, withdrawals, and transfers are never invoked from here.
/// </summary>
public sealed class BankingAgent
{
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
        var toolLogger = loggerFactory.CreateLogger<TracingAIFunction>();
        chatClient = MiniBankChat.Create(chatClient, loggerFactory);

        var tools = QueryTools.Create(accountTools, customerTools, transactionTools, toolLogger);

        var agent = chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "QueryAgent",
                Description = "Answers MiniBank lookup questions using read-only tools.",
                ChatOptions = new ChatOptions
                {
                    Instructions =
                        """
                        You are a helpful MiniBank assistant for lookup questions.
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

                        You cannot move money. If the user asks to deposit, withdraw, or transfer, say that must go through approval.

                        Include account numbers from tool results in your answer.
                        Format currency in GBP, for example £1,532.42.
                        Keep answers brief.
                        """,
                    Temperature = 0f,
                    Tools = tools
                }
            },
            loggerFactory: loggerFactory);

        Agent = MiniBankChat.Instrument(agent, loggerFactory);
    }

    public AIAgent Agent { get; }
}
