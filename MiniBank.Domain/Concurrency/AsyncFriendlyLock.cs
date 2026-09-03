using System;
using System.Threading;
using System.Threading.Tasks;

namespace MiniBank.Domain.Concurrency;

/// <summary>
/// An async-safe mutex that detects reentrant acquisition within the same
/// logical async flow and throws immediately instead of deadlocking.
/// Unlike Monitor/lock, thread identity is meaningless across await points —
/// this uses AsyncLocal, which flows with the logical call context instead.
/// </summary>
public sealed class AsyncFriendlyLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly AsyncLocal<bool> _heldInThisFlow = new();

    public async Task<IDisposable> AcquireAsync()
    {
        if (_heldInThisFlow.Value)
        {
            throw new InvalidOperationException(
                "Reentrant lock acquisition detected: this async flow already " +
                "holds this lock. SemaphoreSlim is not reentrant — this would " +
                "otherwise deadlock silently.");
        }

        await _semaphore.WaitAsync();
        _heldInThisFlow.Value = true;
        return new Releaser(this);
    }

    private void Release()
    {
        _heldInThisFlow.Value = false;
        _semaphore.Release();
    }

    private sealed class Releaser : IDisposable
    {
        private readonly AsyncFriendlyLock _owner;
        private bool _disposed;

        public Releaser(AsyncFriendlyLock owner) => _owner = owner;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner.Release();
        }
    }
}