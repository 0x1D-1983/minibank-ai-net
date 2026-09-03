using System;
using MiniBank.Domain.Models;

namespace MiniBank.Domain.Concurrency;

public sealed class AccountLockHandle : IDisposable
{
    private readonly IDisposable _releaser;
    private bool _disposed;

    public Account Account { get; }

    internal AccountLockHandle(Account account, IDisposable releaser)
    {
        Account = account;
        _releaser = releaser;
    }

    /// <summary>
    /// Reads balance synchronously. Safe because holding this handle already
    /// proves exclusive access — no need to touch the lock again.
    /// </summary>
    public decimal Balance => Account.PeekBalance();

    public void Deposit(decimal amount) => Account.ApplyDeposit(amount);
    public void Withdraw(decimal amount) => Account.ApplyWithdraw(amount);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _releaser.Dispose();
    }
}