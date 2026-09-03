using System.Threading.Tasks;
using MiniBank.AI.Tests.Support;

namespace MiniBank.AI.Tests;

/// <summary>
/// Questions that could map to more than one tool. Each case pins the intended
/// tool, arguments, and factual answer — not the LLM's phrasing.
/// </summary>
[Collection("Ollama")]
public sealed class BankingAgentAmbiguityTests
{
    [Fact(Timeout = 180_000)]
    public async Task OwnerBalanceWithoutAccountNumber_UsesOwnerTotal_NotGetBalance()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("What's John Smith's balance?");

        AgentAssert.ChoseTool(harness.Chat, "get_owner_total_balance");
        AgentAssert.ReceivedArgument(harness.Chat, "get_owner_total_balance", "owner", "John Smith");
        AgentAssert.DidNotChoose(harness.Chat, "get_balance");
        AgentAssert.LookedUpOwner(harness.Repository, "John Smith");
        Assert.Empty(harness.Repository.FindByIdArgs);
        AgentAssert.AnswerContainsFacts(answer, 2332.42m);
    }

    [Fact(Timeout = 180_000)]
    public async Task HowMuchDoesJohnHaveInTheBank_UsesOwnerTotal_NotBankTotal()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("How much does John Smith have in the bank?");

        AgentAssert.ChoseTool(harness.Chat, "get_owner_total_balance");
        AgentAssert.ReceivedArgument(harness.Chat, "get_owner_total_balance", "owner", "John Smith");
        AgentAssert.DidNotChoose(harness.Chat, "get_total_value");
        Assert.Equal(0, harness.Repository.AllCallCount);
        AgentAssert.AnswerContainsFacts(answer, 2332.42m);
    }

    [Fact(Timeout = 180_000)]
    public async Task TotalValueOfAllAccounts_UsesBankTotal_NotOwnerTotal()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("What's the total across every account in the bank?");

        AgentAssert.ChoseTool(harness.Chat, "get_total_value");
        AgentAssert.DidNotChoose(harness.Chat, "get_owner_total_balance");
        Assert.True(harness.Repository.AllCallCount > 0);
        Assert.Empty(harness.Repository.FindByOwnerArgs);
        AgentAssert.AnswerContainsFacts(answer, 9782.42m);
    }

    [Fact(Timeout = 180_000)]
    public async Task HowManyDepositsHasJohnMade_UsesCount_NotAccountDepositList()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("How many deposits has John Smith made?");

        AgentAssert.ChoseTool(harness.Chat, "count_deposits_by_owner");
        AgentAssert.ReceivedArgument(harness.Chat, "count_deposits_by_owner", "owner", "John Smith");
        AgentAssert.DidNotChoose(harness.Chat, "get_deposits");
        Assert.Empty(harness.Repository.FindByIdArgs);
        AgentAssert.AnswerContainsFacts(answer, 2);
    }

    [Fact(Timeout = 180_000)]
    public async Task DepositsToASpecificAccount_UsesGetDeposits_NotOwnerCountOrFullHistory()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("Show me the deposits made to account 10001.");

        AgentAssert.ChoseTool(harness.Chat, "get_deposits");
        AgentAssert.ReceivedArgument(harness.Chat, "get_deposits", "accountNumber", 10001L);
        AgentAssert.DidNotChoose(harness.Chat, "count_deposits_by_owner", "get_account_history");
        Assert.Contains(10001L, harness.Repository.FindByIdArgs);
        Assert.Empty(harness.Repository.FindByOwnerArgs);
        AgentAssert.AnswerContainsFacts(answer, 10001L, 1532.42m);
    }

    [Fact(Timeout = 180_000)]
    public async Task EverythingThatHappenedOnAccount_UsesFullHistory_NotDepositsOnly()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("Show me everything that happened on account 10001.");

        AgentAssert.ChoseTool(harness.Chat, "get_account_history");
        AgentAssert.ReceivedArgument(harness.Chat, "get_account_history", "accountNumber", 10001L);
        AgentAssert.DidNotChoose(harness.Chat, "get_deposits");
        AgentAssert.AnswerContainsFacts(answer, 10001L, 1532.42m);
    }

    [Fact(Timeout = 180_000)]
    public async Task LargestAccount_UsesHighestBalance_NotBankTotal()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("Which customer account is the largest?");

        AgentAssert.ChoseTool(harness.Chat, "get_highest_balance_account");
        AgentAssert.DidNotChoose(harness.Chat, "get_total_value");
        AgentAssert.AnswerContainsFacts(answer, "Jane Doe", 5000.00m);
    }

    [Fact(Timeout = 180_000)]
    public async Task ListJohnsAccounts_UsesFindAccounts_NotOwnerTotal()
    {
        var harness = await AgentTestHarness.CreateAsync();
        var answer = await harness.AskAsync("Which accounts does John Smith have?");

        AgentAssert.ChoseTool(harness.Chat, "find_accounts_by_owner");
        AgentAssert.ReceivedArgument(harness.Chat, "find_accounts_by_owner", "owner", "John Smith");
        AgentAssert.DidNotChoose(harness.Chat, "get_owner_total_balance");
        AgentAssert.AnswerContainsFacts(answer, 1532.42m, 800.00m);
    }
}
