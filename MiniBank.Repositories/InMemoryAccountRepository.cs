namespace Banking.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MiniBank.Domain.Models;

public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly Dictionary<long, Account> _accounts = new();
    private readonly object _sync = new();

    public Task AddAccountAsync(Account account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            _accounts[account.AccountNumber] = account;
        return Task.CompletedTask;
    }

    public Task<Account?> FindByIdAsync(long accountNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            return Task.FromResult(_accounts.TryGetValue(accountNumber, out var account) ? account : null);
    }

    public Task<List<Account>> AllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            return Task.FromResult(_accounts.Values.ToList());
    }

    public Task<List<Account>> FindByOwnerAsync(string owner, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(_accounts.Values
                .Where(account => account.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase))
                .ToList());
        }
    }

    public Task UpdateAccountAsync(Account account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            _accounts[account.AccountNumber] = account;
        return Task.CompletedTask;
    }
}
