using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using MiniBank.AI.Models;

namespace MiniBank.AI.Workflows;

internal sealed class DeclineExecutor()
    : Executor<ApprovalResult, ToolResult>(BankingWorkflow.DeclineExecutorId)
{
    public override ValueTask<ToolResult> HandleAsync(
        ApprovalResult message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var text = message.Reason is { Length: > 0 } reason
            ? $"Declined: {reason}"
            : "Declined: the write was not approved.";

        return ValueTask.FromResult(new ToolResult(false, text, "DECLINED", Retryable: false));
    }
}
