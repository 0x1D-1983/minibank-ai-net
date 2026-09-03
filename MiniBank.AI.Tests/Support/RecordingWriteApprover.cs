using MiniBank.AI.Workflows;

namespace MiniBank.AI.Tests.Support;

internal sealed class RecordingWriteApprover(bool approve = true) : IWriteApprover
{
    public List<BankingIntent> Seen { get; } = [];
    public bool Approve { get; set; } = approve;
    public string? Reason { get; set; } = "Transfers are paused.";

    public ValueTask<ApprovalDecision> ReviewAsync(BankingIntent intent, CancellationToken cancellationToken)
    {
        Seen.Add(intent);
        return ValueTask.FromResult(Approve
            ? new ApprovalDecision(true)
            : new ApprovalDecision(false, Reason));
    }
}
