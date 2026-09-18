using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace MiniBank.AI.Workflows;

internal sealed class QueryExecutor(AIAgent queryAgent)
    : Executor<BankingIntent, string>(BankingWorkflow.QueryExecutorId)
{
    public override async ValueTask<string> HandleAsync(
        BankingIntent message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await queryAgent.RunAsync(message.Question, cancellationToken: cancellationToken);
            return response.Text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (AccountDenial.TryGet(ex, out var denial))
                return denial.Message;

            throw;
        }
    }
}
