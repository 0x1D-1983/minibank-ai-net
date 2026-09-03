using System;
using System.Collections.Generic;
using System.Linq;
using MiniBank.Domain.Models;

namespace MiniBank.Domain.Concurrency;

/// <summary>
/// Holds locks on several accounts at once (acquired in a fixed order by
/// <see cref="Account.LockAllAsync"/>). Index by account to get its handle.
/// </summary>
public sealed class MultiAccountLock : IDisposable
{
    private readonly List<AccountLockHandle> _handles;
    private bool _disposed;

    internal MultiAccountLock(List<AccountLockHandle> handles) => _handles = handles;

    public AccountLockHandle this[Account account] =>
        _handles.First(h => h.Account == account);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = _handles.Count - 1; i >= 0; i--)
            _handles[i].Dispose();
    }
}