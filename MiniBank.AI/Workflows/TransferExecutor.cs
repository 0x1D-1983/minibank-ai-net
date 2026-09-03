using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using MiniBank.AI.Tools;

namespace MiniBank.AI.Workflows;

internal sealed class TransferExecutor(OperationTools operations)
    : Executor<ApprovalResult, string>(BankingWorkflow.TransferExecutorId)
{
    public override async ValueTask<string> HandleAsync(
        ApprovalResult message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var intent = message.Intent;

        try
        {
            return intent.Operation switch
            {
                WriteOperation.Deposit => await operations.DepositAsync(
                    intent.AccountNumber!.Value, intent.Amount!.Value),
                WriteOperation.Withdraw => await operations.WithdrawAsync(
                    intent.AccountNumber!.Value, intent.Amount!.Value),
                WriteOperation.Transfer => await operations.TransferAsync(
                    intent.FromAccountNumber!.Value,
                    intent.ToAccountNumber!.Value,
                    intent.Amount!.Value),
                _ => $"Cannot execute '{intent.Operation}'."
            };
        }
        catch (Exception ex)
        {
            return $"The bank rejected the operation: {ex.Message}";
        }
    }
}
