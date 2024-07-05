using System.Collections.Concurrent;
using CachedQueries.Core.Interfaces;

namespace CachedQueries.Core;

public class DefaultLockManager : ILockManager
{
    private readonly ConcurrentDictionary<object, SemaphoreSlim> _locks = new();

    public async Task CheckLockAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_locks.TryGetValue(key, out var lockItem))
        {
            await lockItem.WaitAsync(cancellationToken);
        }
    }

    public async Task LockAsync(string key, TimeSpan timespan, CancellationToken cancellationToken = default)
    {
        var lockItem = _locks.GetOrAdd(key, k => new SemaphoreSlim(1, 1));
        await lockItem.WaitAsync(cancellationToken);
    }

    public Task ReleaseLockAsync(string key)
    {
        if (_locks.TryRemove(key, out var lockItem))
        {
            lockItem.Release();
        }

        return Task.CompletedTask;
    }
}
