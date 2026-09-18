using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniBank.AI.Agents;
using MiniBank.AI.Models;
using MiniBank.AI.Tools;

namespace MiniBank.AI.Workflows;

/// <summary>
/// MiniBank assistant workflow:
/// User → Intent Agent → READ → Query Executor
///                      → WRITE → Approval Executor → Transfer Executor → Bank
/// </summary>
public sealed class BankingWorkflow
{
    public const string IntentAgentId = "IntentAgent";
    public const string QueryExecutorId = "QueryExecutor";
    public const string ApprovalExecutorId = "ApprovalExecutor";
    public const string TransferExecutorId = "TransferExecutor";
    public const string DeclineExecutorId = "DeclineExecutor";

    private BankingWorkflow(Workflow workflow)
    {
        Workflow = workflow;
    }

    public Workflow Workflow { get; }

    public static BankingWorkflow Create(
        AccountTools accountTools,
        CustomerTools customerTools,
        TransactionTools transactionTools,
        OperationTools operationTools,
        OllamaOptions ollama,
        IChatClient? chatClient = null,
        ILoggerFactory? loggerFactory = null,
        IWriteApprover? approver = null)
    {
        ArgumentNullException.ThrowIfNull(accountTools);
        ArgumentNullException.ThrowIfNull(customerTools);
        ArgumentNullException.ThrowIfNull(transactionTools);
        ArgumentNullException.ThrowIfNull(operationTools);
        ArgumentNullException.ThrowIfNull(ollama);

        loggerFactory ??= NullLoggerFactory.Instance;
        approver ??= new AutoApprover();

        var intent = new IntentExecutor(new IntentAgent(ollama, chatClient, loggerFactory));
        var query = new QueryExecutor(
            new BankingAgent(accountTools, customerTools, transactionTools, ollama, chatClient, loggerFactory).Agent,
            loggerFactory.CreateLogger<QueryExecutor>());
        var approval = new ApprovalExecutor(approver);
        var transfer = new TransferExecutor(
            operationTools,
            loggerFactory.CreateLogger<TransferExecutor>());
        var decline = new DeclineExecutor();

        var workflow = new WorkflowBuilder(intent)
            .WithName("MiniBank")
            .WithDescription("Routes lookup questions to read tools and money movement through approval.")
            .AddSwitch(intent, sw => sw
                .AddCase((BankingIntent? classified) => classified?.Kind == IntentKind.Read, [query])
                .AddCase((BankingIntent? classified) => classified?.Kind == IntentKind.Write, [approval])
                .WithDefault([query]))
            .AddSwitch(approval, sw => sw
                .AddCase((ApprovalResult? result) => result?.Approved == true, [transfer])
                .AddCase((ApprovalResult? result) => result?.Approved == false, [decline]))
            .WithOutputFrom(query, transfer, decline)
            .Build();

        return new BankingWorkflow(workflow);
    }

    public async Task<string> RunAsync(string question, CancellationToken cancellationToken = default)
    {
        var detailed = await RunDetailedAsync(question, cancellationToken);
        return detailed.Output;
    }

    public async Task<WorkflowRunResult> RunDetailedAsync(string question, CancellationToken cancellationToken = default)
    {
        await using var run = await InProcessExecution.RunAsync(Workflow, question, cancellationToken: cancellationToken);
        var events = run.OutgoingEvents.ToList();

        var result = events.OfType<WorkflowOutputEvent>()
            .Select(evt => evt.Is<ToolResult>(out var toolResult) ? toolResult : null)
            .LastOrDefault(toolResult => toolResult is not null)
            ?? new ToolResult(false, string.Empty, "NO_OUTPUT", Retryable: false);

        var executorIds = events
            .OfType<ExecutorCompletedEvent>()
            .Select(evt => evt.ExecutorId)
            .ToList();

        return new WorkflowRunResult(result, executorIds);
    }
}

public sealed record WorkflowRunResult(ToolResult Result, IReadOnlyList<string> ExecutorIds)
{
    public string Output => Result.UserFacingMessage ?? string.Empty;
}
