using Microsoft.Agents.AI.Workflows;

namespace MiniBank.AI.Workflows;

internal sealed class DeclineExecutor()
    : Executor<ApprovalResult, string>(BankingWorkflow.DeclineExecutorId)
{
    public override ValueTask<string> HandleAsync(
        ApprovalResult message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(message.Reason is { Length: > 0 } reason
            ? $"Declined: {reason}"
            : "Declined: the write was not approved.");
}
