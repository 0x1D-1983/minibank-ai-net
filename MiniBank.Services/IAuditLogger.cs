using System.Threading.Tasks;
using MiniBank.Domain.Models;

namespace Banking.Services;

public interface IAuditLogger
{
    Task LogAsync(long accountNumber, AccountAction action, decimal amount);
}