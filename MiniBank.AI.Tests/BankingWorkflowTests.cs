using System;
using System.Threading.Tasks;
using MiniBank.AI.Tests.Support;
using MiniBank.AI.Workflows;

namespace MiniBank.AI.Tests;

[Collection("Ollama")]
public sealed class BankingWorkflowTests
{
    [Fact(Timeout = 180_000)]
    public async Task BalanceQuestion_GoesToQueryExecutor_NotTransfer()
    {
        var harness = await AgentTestHarness.CreateWorkflowAsync();
        var result = await harness.AskDetailedAsync("What's the balance of account 10001?");

        Assert.Contains(BankingWorkflow.IntentAgentId, result.ExecutorIds);
        Assert.Contains(BankingWorkflow.QueryExecutorId, result.ExecutorIds);
        Assert.DoesNotContain(BankingWorkflow.ApprovalExecutorId, result.ExecutorIds);
        Assert.DoesNotContain(BankingWorkflow.TransferExecutorId, result.ExecutorIds);
        Assert.Empty(harness.Approver!.Seen);
        Assert.Equal(0, harness.Repository.UpdateCallCount);
        AgentAssert.AnswerContainsFacts(result.Output, 1532.42m);
    }

    [Fact(Timeout = 180_000)]
    public async Task Transfer_GoesThroughApprovalAndMovesMoney()
    {
        var harness = await AgentTestHarness.CreateWorkflowAsync();
        var result = await harness.AskDetailedAsync(
            "Transfer 50 pounds from account 10001 to account 20001.");

        Assert.Contains(BankingWorkflow.IntentAgentId, result.ExecutorIds);
        Assert.Contains(BankingWorkflow.ApprovalExecutorId, result.ExecutorIds);
        Assert.Contains(BankingWorkflow.TransferExecutorId, result.ExecutorIds);
        Assert.DoesNotContain(BankingWorkflow.QueryExecutorId, result.ExecutorIds);
        Assert.DoesNotContain(BankingWorkflow.DeclineExecutorId, result.ExecutorIds);

        var intent = Assert.Single(harness.Approver!.Seen);
        Assert.Equal(IntentKind.Write, intent.Kind);
        Assert.Equal(WriteOperation.Transfer, intent.Operation);
        Assert.Equal(10001L, intent.FromAccountNumber);
        Assert.Equal(20001L, intent.ToAccountNumber);
        Assert.Equal(50m, intent.Amount);

        Assert.Equal(2, harness.Repository.UpdateCallCount);
        Assert.Equal(1482.42m, await (await harness.Bank.FindAccountAsync(10001))!.GetBalanceAsync());
        Assert.Equal(5050.00m, await (await harness.Bank.FindAccountAsync(20001))!.GetBalanceAsync());
        AgentAssert.AnswerContainsFacts(result.Output, 50m, 10001L, 20001L);
    }

    [Fact(Timeout = 180_000)]
    public async Task Transfer_RejectedByApprover_DoesNotMoveMoney()
    {
        var harness = await AgentTestHarness.CreateWorkflowAsync(approveWrites: false);
        var result = await harness.AskDetailedAsync(
            "Transfer 50 pounds from account 10001 to account 20001.");

        Assert.Contains(BankingWorkflow.ApprovalExecutorId, result.ExecutorIds);
        Assert.Contains(BankingWorkflow.DeclineExecutorId, result.ExecutorIds);
        Assert.DoesNotContain(BankingWorkflow.TransferExecutorId, result.ExecutorIds);
        Assert.Single(harness.Approver!.Seen);
        Assert.Equal(0, harness.Repository.UpdateCallCount);
        Assert.Equal(1532.42m, await (await harness.Bank.FindAccountAsync(10001))!.GetBalanceAsync());
        Assert.Equal(5000.00m, await (await harness.Bank.FindAccountAsync(20001))!.GetBalanceAsync());
        Assert.Contains("Declined", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
