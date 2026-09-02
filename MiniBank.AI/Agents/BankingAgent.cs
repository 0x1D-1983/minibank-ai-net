using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
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
        TransactionTools transactionTools)
    {
        ArgumentNullException.ThrowIfNull(accountTools);
        ArgumentNullException.ThrowIfNull(customerTools);
        ArgumentNullException.ThrowIfNull(transactionTools);

        IChatClient chatClient = new OllamaApiClient(new Uri(Endpoint), ModelName);

        Agent = chatClient.AsAIAgent(
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
                AIFunctionFactory.Create(accountTools.GetBalanceAsync, name: "get_balance"),
                AIFunctionFactory.Create(accountTools.FindAccountsByOwnerAsync, name: "find_accounts_by_owner"),
                AIFunctionFactory.Create(accountTools.GetTotalValueAsync, name: "get_total_value"),
                AIFunctionFactory.Create(accountTools.GetHighestBalanceAccountAsync, name: "get_highest_balance_account"),
                AIFunctionFactory.Create(customerTools.GetOwnerTotalBalanceAsync, name: "get_owner_total_balance"),
                AIFunctionFactory.Create(customerTools.CountDepositsByOwnerAsync, name: "count_deposits_by_owner"),
                AIFunctionFactory.Create(transactionTools.GetDepositsAsync, name: "get_deposits"),
                AIFunctionFactory.Create(transactionTools.GetAccountHistoryAsync, name: "get_account_history")
            ]);
    }

    public AIAgent Agent { get; }
}
