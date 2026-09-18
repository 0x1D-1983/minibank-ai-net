using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Banking.Services;
using Microsoft.Extensions.AI;
using MiniBank.AI.Agents;
using MiniBank.AI.Tests.Support;
using MiniBank.AI.Tools;
using MiniBank.AI.Workflows;
using MiniBank.Domain.Models;

namespace MiniBank.AI.Tests;

/// <summary>
/// Unauthorized account access through the workflow, without Ollama.
/// Query denials become a normal not-found answer; writes are rejected by TransferExecutor.
/// </summary>
public sealed class BankingWorkflowAuthorizationTests
{
    private static readonly OllamaOptions UnusedOllama = new()
    {
        Endpoint = "http://127.0.0.1:9",
        Model = "unused"
    };

    [Fact]
    public async Task UnauthorizedAccountQuery_ReturnsAccountNotFoundAnswer()
    {
        var workflow = await CreateWorkflowAsync(options =>
        {
            if (ScriptedChatClient.HasTool(options, "classify_query"))
                return ScriptedChatClient.FunctionCall("classify_query");

            if (ScriptedChatClient.HasTool(options, "get_balance"))
            {
                return ScriptedChatClient.FunctionCall(
                    "get_balance",
                    new Dictionary<string, object?> { ["accountNumber"] = 20001L });
            }

            return ScriptedChatClient.Text("Account 20001 doesn't exist.");
        });

        var result = await workflow.RunDetailedAsync("What's the balance of account 20001?");

        Assert.Contains("doesn't exist", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20001", result.Output);
        Assert.Contains(BankingWorkflow.QueryExecutorId, result.ExecutorIds);
        Assert.DoesNotContain(BankingWorkflow.TransferExecutorId, result.ExecutorIds);
    }

    [Fact]
    public async Task UnauthorizedWithdraw_ReturnsBankRejectionAnswer()
    {
        var workflow = await CreateWorkflowAsync(options =>
        {
            if (ScriptedChatClient.HasTool(options, "classify_withdraw"))
            {
                return ScriptedChatClient.FunctionCall(
                    "classify_withdraw",
                    new Dictionary<string, object?>
                    {
                        ["accountNumber"] = 20001L,
                        ["amount"] = 10m
                    });
            }

            return ScriptedChatClient.Text("unexpected query turn");
        });

        var result = await workflow.RunDetailedAsync("Withdraw 10 from account 20001.");

        Assert.Contains("doesn't exist", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20001", result.Output);
        Assert.Contains(BankingWorkflow.TransferExecutorId, result.ExecutorIds);
        Assert.DoesNotContain(BankingWorkflow.QueryExecutorId, result.ExecutorIds);
    }

    private static async Task<BankingWorkflow> CreateWorkflowAsync(Func<ChatOptions?, ChatResponse> respond)
    {
        var bank = new Bank(new RecordingAccountRepository(), new NoOpAuditLogger());
        await bank.AddAccountAsync(new CurrentAccount("John Smith", 10001, overdraftLimit: 500m));
        await bank.DepositAsync(10001, 1_532.42m);
        await bank.AddAccountAsync(new CurrentAccount("Jane Doe", 20001, overdraftLimit: 0m));
        await bank.DepositAsync(20001, 5_000.00m);

        var authorizedBank = new AuthorizedBank(bank, "John Smith");
        var chat = new ScriptedChatClient(respond);

        return BankingWorkflow.Create(
            new AccountTools(authorizedBank),
            new CustomerTools(authorizedBank),
            new TransactionTools(authorizedBank),
            new OperationTools(authorizedBank),
            UnusedOllama,
            chatClient: chat);
    }
}
