using Banking.Domain.Models;
using Banking.Services;

namespace MiniBank.AI.Tests.Support;

internal sealed class NoOpAuditLogger : IAuditLogger
{
    public Task LogAsync(long accountNumber, AccountAction action, decimal amount)
        => Task.CompletedTask;
}
