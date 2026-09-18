using System.Threading.Tasks;
using System.Threading;
using MiniBank.Domain.Models;

namespace Banking.Services;

public interface IAuditLogger
{
    Task LogAsync(long accountNumber, AccountAction action, decimal amount, CancellationToken cancellationToken = default);
}
