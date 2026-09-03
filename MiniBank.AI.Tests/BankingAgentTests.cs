using System.Threading.Tasks;
using MiniBank.AI.Tests.Support;

namespace MiniBank.AI.Tests;

[Collection("Ollama")]
public sealed class BankingAgentTests
{
    [Fact(Timeout = 180_000)]
    public async Task BalanceQuestion_UsesGetBalance_WithAccountNumber()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("What's the balance of account 10001?");

        AgentAssert.ChoseTool(harness.Chat, "get_balance");
        AgentAssert.ReceivedArgument(harness.Chat, "get_balance", "accountNumber", 10001L);
        Assert.Contains(10001L, harness.Repository.FindByIdArgs);
        AgentAssert.AnswerContainsFacts(answer, 1532.42m);
    }

    [Fact(Timeout = 180_000)]
    public async Task OwnerTotalQuestion_UsesOwnerTotalBalance()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync(
            "How much money does John Smith have across his accounts?");

        AgentAssert.ChoseTool(harness.Chat, "get_owner_total_balance");
        AgentAssert.ReceivedArgument(harness.Chat, "get_owner_total_balance", "owner", "John Smith");
        AgentAssert.LookedUpOwner(harness.Repository, "John Smith");
        AgentAssert.AnswerContainsFacts(answer, 2332.42m);
    }

    [Fact(Timeout = 180_000)]
    public async Task DepositCountQuestion_UsesCountDepositsByOwner()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("How many deposits has John Smith made?");

        AgentAssert.ChoseTool(harness.Chat, "count_deposits_by_owner");
        AgentAssert.ReceivedArgument(harness.Chat, "count_deposits_by_owner", "owner", "John Smith");
        AgentAssert.LookedUpOwner(harness.Repository, "John Smith");
        AgentAssert.AnswerContainsFacts(answer, 2);
    }

    [Fact(Timeout = 180_000)]
    public async Task BankTotalQuestion_UsesGetTotalValue()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("What's the total value of all accounts?");

        AgentAssert.ChoseTool(harness.Chat, "get_total_value");
        Assert.True(harness.Repository.AllCallCount > 0);
        AgentAssert.AnswerContainsFacts(answer, 9782.42m);
    }

    [Fact(Timeout = 180_000)]
    public async Task HighestBalanceQuestion_UsesGetHighestBalanceAccount()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("Which account has the highest balance?");

        AgentAssert.ChoseTool(harness.Chat, "get_highest_balance_account");
        Assert.True(harness.Repository.AllCallCount > 0);
        AgentAssert.AnswerContainsFacts(answer, "Jane Doe", 5000.00m);
    }

    [Fact(Timeout = 180_000)]
    public async Task AccountDepositsQuestion_UsesGetDeposits()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("Show me the deposits made to account 10001.");

        AgentAssert.ChoseTool(harness.Chat, "get_deposits");
        AgentAssert.ReceivedArgument(harness.Chat, "get_deposits", "accountNumber", 10001L);
        Assert.Contains(10001L, harness.Repository.FindByIdArgs);
        AgentAssert.AnswerContainsFacts(answer, 10001L, 1532.42m);
    }

    [Fact(Timeout = 180_000)]
    public async Task ListOwnerAccountsQuestion_UsesFindAccountsByOwner()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("List the accounts owned by John Smith.");

        AgentAssert.ChoseTool(harness.Chat, "find_accounts_by_owner");
        AgentAssert.ReceivedArgument(harness.Chat, "find_accounts_by_owner", "owner", "John Smith");
        AgentAssert.LookedUpOwner(harness.Repository, "John Smith");
        AgentAssert.AnswerContainsFacts(answer, 1532.42m, 800.00m);
    }

    [Fact(Timeout = 180_000)]
    public async Task AccountHistoryQuestion_UsesGetAccountHistory()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("Show the full transaction history of account 10001.");

        AgentAssert.ChoseTool(harness.Chat, "get_account_history");
        AgentAssert.ReceivedArgument(harness.Chat, "get_account_history", "accountNumber", 10001L);
        Assert.Contains(10001L, harness.Repository.FindByIdArgs);
        AgentAssert.AnswerContainsFacts(answer, 10001L, "Deposit", 1532.42m);
    }
}
