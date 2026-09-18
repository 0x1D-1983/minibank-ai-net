using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Models;

namespace MiniBank.AI.Workflows;

internal sealed class QueryExecutor(
    AIAgent queryAgent,
    ILogger<QueryExecutor> logger)
    : Executor<BankingIntent, ToolResult>(BankingWorkflow.QueryExecutorId)
{
    public override async ValueTask<ToolResult> HandleAsync(
        BankingIntent message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await queryAgent.RunAsync(message.Question, cancellationToken: cancellationToken);
            return new ToolResult(true, response.Text, "SUCCESS", Retryable: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected failure handling query: {Question}", message.Question);
            return new ToolResult(false, "The request could not be completed due to an internal error.", "INTERNAL_ERROR", false);
        }
    }
}
