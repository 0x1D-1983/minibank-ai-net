using MiniBank.Domain.Models;
using Banking.Services;
using System.Threading.Tasks;

namespace MiniBank.AI.Tests.Support;

internal sealed class NoOpAuditLogger : IAuditLogger
{
    public Task LogAsync(long accountNumber, AccountAction action, decimal amount)
        => Task.CompletedTask;
}
