namespace Banking.Repositories;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MiniBank.Domain.Models;

public interface IAccountRepository
{
    Task AddAccountAsync(Account account, CancellationToken cancellationToken = default);
    Task<Account?> FindByIdAsync(long accountNumber, CancellationToken cancellationToken = default);
    Task<List<Account>> AllAsync(CancellationToken cancellationToken = default);
    Task<List<Account>> FindByOwnerAsync(string owner, CancellationToken cancellationToken = default);
    Task UpdateAccountAsync(Account account, CancellationToken cancellationToken = default);
}
