using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MiniBank.Domain.Concurrency;
using MiniBank.Domain.Exceptions;

namespace MiniBank.Domain.Models;

public abstract class Account 
{
    private readonly AsyncFriendlyLock _lock = new();

    protected decimal _balance;

    public string Owner { get; }
    public long AccountNumber { get; }
    public List<string> History { get; } = new List<string>();

    protected Account(string owner, long accountNumber)
    {
        Owner = owner;
        AccountNumber = accountNumber;
        _balance = 0m;
    }

    public async Task<decimal> GetBalanceAsync()
    {
        using var releaser = await _lock.AcquireAsync();
        return _balance;
    }

    internal decimal PeekBalance() => _balance;

    /// <summary>
    /// Acquires this account's lock and returns a handle proving it's held.
    /// Dispose the handle to release. This is the ONLY way to get access
    /// to the mutation methods on <see cref="AccountLockHandle"/>.
    /// </summary>
    public async Task<AccountLockHandle> AcquireLockAsync()
    {
        var releaser = await _lock.AcquireAsync();
        return new AccountLockHandle(this, releaser);
    }

    /// <summary>
    /// Acquires locks on several accounts at once, always in AccountNumber
    /// order, so callers never have to think about deadlock ordering.
    /// </summary>
    public static async Task<MultiAccountLock> LockAllAsync(params Account[] accounts)
    {
        var ordered = accounts.Distinct().OrderBy(a => a.AccountNumber).ToList();
        var handles = new List<AccountLockHandle>(ordered.Count);
        try
        {
            foreach (var account in ordered)
                handles.Add(await account.AcquireLockAsync());
        }
        catch
        {
            foreach (var h in handles) h.Dispose();
            throw;
        }
        return new MultiAccountLock(handles);
    }

    public async Task DepositAsync(decimal amount)
    {
        using var handle = await AcquireLockAsync();
        handle.Deposit(amount);
    }

    public async Task WithdrawAsync(decimal amount)
    {
        using var handle = await AcquireLockAsync();
        handle.Withdraw(amount);
    }

    // --- Mutation primitives. internal: only Account itself and
    // AccountLockHandle (same assembly) can reach these, and the handle
    // is unobtainable without holding the lock. ---

    internal void ApplyDeposit(decimal amount)
    {
        if (amount <= 0)
            throw new InvalidAmountException($"Deposit amount must be positive, got {amount}");
        _balance += amount;
        History.Add($"{AccountAction.Deposit}: +{amount:F2}");
    }

    internal abstract void ApplyWithdraw(decimal amount);

    public virtual async Task<string> ToStringAsync()
    {
        var balance = await GetBalanceAsync();
        return $"Account(owner=\"{Owner}\", accountNumber={AccountNumber}, balance={balance})";
    }

    // public void Dispose() => _lock.Dispose();

    /// <summary>
    /// Sets the balance directly when reconstructing an account from storage.
    /// Bypasses deposit/withdraw validation — for repository/hydration use only.
    /// </summary>
    internal void HydrateBalance(decimal balance) => _balance = balance;

    protected static void ValidateWithdrawAmount(decimal amount)
    {
        if (amount <= 0)
            throw new InvalidAmountException($"Withdrawal amount must be positive, got {amount}");
    }
}