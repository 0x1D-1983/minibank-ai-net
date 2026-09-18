using System;
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
        OllamaOptions ollama,
        IChatClient? chatClient = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(accountTools);
        ArgumentNullException.ThrowIfNull(customerTools);
        ArgumentNullException.ThrowIfNull(transactionTools);
        ArgumentNullException.ThrowIfNull(ollama);

        loggerFactory ??= NullLoggerFactory.Instance;
        var toolLogger = loggerFactory.CreateLogger<TracingAIFunction>();
        chatClient = MiniBankChat.Create(chatClient, loggerFactory, ollama);

        var tools = QueryTools.Create(accountTools, customerTools, transactionTools, toolLogger);
        var currentOwner = customerTools.CurrentOwner;

        var agent = chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = "QueryAgent",
                Description = "Answers MiniBank lookup questions using read-only tools.",
                ChatOptions = new ChatOptions
                {
                    Instructions =
                        $"""
                        You are a helpful MiniBank assistant for lookup questions.
                        You are assisting {currentOwner}. Every tool returns only this customer's accounts.
                        Always use tools to answer questions about {currentOwner}'s accounts. Never invent balances, totals, transactions, or account numbers.

                        Choose the matching tool:
                        - get_balance: ONLY when the user supplied a specific account number. Never guess or invent one.
                        - get_owner_total_balance: {currentOwner}'s total, when they ask for their balance without an account number (including "my" or the name {currentOwner}).
                        - count_deposits_by_owner: how many deposits {currentOwner} has made.
                        - get_deposits: ONLY deposits on a numbered account. Do not use this for full history.
                        - find_accounts_by_owner: list {currentOwner}'s accounts.
                        - get_account_history: every transaction on a numbered account. Use this for history or "everything that happened".

                        If the user asks about a different named customer, do not call tools and do not use {currentOwner}'s figures as the answer.
                        Tell them you can only see {currentOwner}'s accounts and cannot look up other customers.

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

        Agent = MiniBankChat.Instrument(agent, loggerFactory, ollama);
    }

    public AIAgent Agent { get; }
}
