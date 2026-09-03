namespace MiniBank.AI.Workflows;

public enum IntentKind
{
    Read,
    Write
}

public enum WriteOperation
{
    Deposit,
    Withdraw,
    Transfer
}

public sealed record BankingIntent(
    IntentKind Kind,
    string Question,
    WriteOperation? Operation = null,
    long? AccountNumber = null,
    long? FromAccountNumber = null,
    long? ToAccountNumber = null,
    decimal? Amount = null)
{
    public static BankingIntent Read(string question)
        => new(IntentKind.Read, question);

    public static BankingIntent Deposit(string question, long accountNumber, decimal amount)
        => new(IntentKind.Write, question, WriteOperation.Deposit, AccountNumber: accountNumber, Amount: amount);

    public static BankingIntent Withdraw(string question, long accountNumber, decimal amount)
        => new(IntentKind.Write, question, WriteOperation.Withdraw, AccountNumber: accountNumber, Amount: amount);

    public static BankingIntent Transfer(string question, long fromAccountNumber, long toAccountNumber, decimal amount)
        => new(IntentKind.Write, question, WriteOperation.Transfer,
            FromAccountNumber: fromAccountNumber, ToAccountNumber: toAccountNumber, Amount: amount);
}

public sealed record ApprovalResult(BankingIntent Intent, bool Approved, string? Reason = null);

public interface IWriteApprover
{
    ValueTask<ApprovalDecision> ReviewAsync(BankingIntent intent, CancellationToken cancellationToken);
}

public sealed record ApprovalDecision(bool Approved, string? Reason = null);

/// <summary>
/// Approves every structurally valid write. Console and tests can swap this
/// for a human or policy check without changing the workflow graph.
/// </summary>
public sealed class AutoApprover : IWriteApprover
{
    public ValueTask<ApprovalDecision> ReviewAsync(BankingIntent intent, CancellationToken cancellationToken)
        => ValueTask.FromResult(new ApprovalDecision(true));
}
