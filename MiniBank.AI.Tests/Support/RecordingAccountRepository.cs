using Banking.Domain.Models;
using Banking.Repositories;

namespace MiniBank.AI.Tests.Support;

internal sealed class RecordingAccountRepository : IAccountRepository
{
    private readonly Dictionary<long, Account> _accounts = new();

    public List<long> FindByIdArgs { get; } = [];
    public List<string> FindByOwnerArgs { get; } = [];
    public int AllCallCount { get; private set; }

    public Task AddAccountAsync(Account account)
    {
        _accounts[account.AccountNumber] = account;
        return Task.CompletedTask;
    }

    public Task<Account?> FindByIdAsync(long accountNumber)
    {
        FindByIdArgs.Add(accountNumber);
        return Task.FromResult(_accounts.TryGetValue(accountNumber, out var account) ? account : null);
    }

    public Task<List<Account>> AllAsync()
    {
        AllCallCount++;
        return Task.FromResult(_accounts.Values.ToList());
    }

    public Task<List<Account>> FindByOwnerAsync(string owner)
    {
        FindByOwnerArgs.Add(owner);
        return Task.FromResult(_accounts.Values
            .Where(account => account.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase))
            .ToList());
    }

    public Task UpdateAccountAsync(Account account)
    {
        _accounts[account.AccountNumber] = account;
        return Task.CompletedTask;
    }

    public void ClearRecordings()
    {
        FindByIdArgs.Clear();
        FindByOwnerArgs.Clear();
        AllCallCount = 0;
    }
}
