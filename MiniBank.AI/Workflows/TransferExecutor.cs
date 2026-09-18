using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using MiniBank.AI.Models;
using MiniBank.AI.Tools;

namespace MiniBank.AI.Workflows;

internal sealed class TransferExecutor(
    OperationTools operations,
    ILogger<TransferExecutor> logger)
    : Executor<ApprovalResult, ToolResult>(BankingWorkflow.TransferExecutorId)
{
    private const string UnsupportedOperationCode = "UNSUPPORTED_OPERATION";

    public override async ValueTask<ToolResult> HandleAsync(
        ApprovalResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var intent = message.Intent;

        if (!TryValidate(intent, out var validationError))
            return validationError!;

        return intent.Operation switch
        {
            WriteOperation.Deposit => await operations.DepositAsync(
                intent.AccountNumber!.Value, intent.Amount!.Value, cancellationToken),
            WriteOperation.Withdraw => await operations.WithdrawAsync(
                intent.AccountNumber!.Value, intent.Amount!.Value, cancellationToken),
            WriteOperation.Transfer => await operations.TransferAsync(
                intent.FromAccountNumber!.Value,
                intent.ToAccountNumber!.Value,
                intent.Amount!.Value,
                cancellationToken),
            _ => UnsupportedOperation(intent)
        };
    }

    private ToolResult UnsupportedOperation(BankingIntent intent)
    {
        logger.LogError(
            "Unsupported write operation {Operation} for question {Question}. ErrorCode {ErrorCode}",
            intent.Operation,
            intent.Question,
            UnsupportedOperationCode);

        return new ToolResult(
            false,
            $"Cannot execute '{intent.Operation}'.",
            UnsupportedOperationCode,
            Retryable: false);
    }

    private static bool TryValidate(BankingIntent intent, out ToolResult? error)
    {
        var missing = intent.Operation switch
        {
            WriteOperation.Deposit => intent.AccountNumber is null || intent.Amount is null,
            WriteOperation.Withdraw => intent.AccountNumber is null || intent.Amount is null,
            WriteOperation.Transfer => intent.FromAccountNumber is null || intent.ToAccountNumber is null || intent.Amount is null,
            _ => false
        };
        error = missing
            ? new ToolResult(false, "The request was missing required details.", "INVALID_REQUEST", false)
            : null;
        return !missing;
    }
}
