using Microsoft.Agents.AI.Workflows;

namespace MiniBank.AI.Workflows;

internal sealed class ApprovalExecutor(IWriteApprover approver)
    : Executor<BankingIntent, ApprovalResult>(BankingWorkflow.ApprovalExecutorId)
{
    public override async ValueTask<ApprovalResult> HandleAsync(
        BankingIntent message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var structural = Validate(message);
        if (!structural.Approved)
            return new ApprovalResult(message, false, structural.Reason);

        var decision = await approver.ReviewAsync(message, cancellationToken);
        return new ApprovalResult(message, decision.Approved, decision.Reason);
    }

    private static ApprovalDecision Validate(BankingIntent intent)
    {
        if (intent.Kind != IntentKind.Write || intent.Operation is null)
            return new ApprovalDecision(false, "Not a write operation.");

        if (intent.Amount is null or <= 0)
            return new ApprovalDecision(false, "A positive amount is required.");

        return intent.Operation switch
        {
            WriteOperation.Transfer when intent.FromAccountNumber is null or 0
                || intent.ToAccountNumber is null or 0
                => new ApprovalDecision(false, "A transfer needs a source and destination account."),
            WriteOperation.Transfer when intent.FromAccountNumber == intent.ToAccountNumber
                => new ApprovalDecision(false, "Cannot transfer to the same account."),
            WriteOperation.Deposit or WriteOperation.Withdraw when intent.AccountNumber is null or 0
                => new ApprovalDecision(false, "An account number is required."),
            _ => new ApprovalDecision(true)
        };
    }
}
