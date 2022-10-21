using System.Collections.Concurrent;
using CachedQueries.Core.Interfaces;

namespace CachedQueries.Core;

public class DefaultLockManager : ILockManager
{
    private readonly ConcurrentDictionary<object, SemaphoreSlim> _locks = new();

    public Task CheckLockAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task LockAsync(string key, TimeSpan timespan)
    {
        var lockItem = _locks.GetOrAdd(key, k => new SemaphoreSlim(1, 1));
        await lockItem.WaitAsync();
    }

    public Task ReleaseLockAsync(string key)
    {
        _locks.TryGetValue(key, out var lockItem);
        lockItem?.Release();

        return Task.CompletedTask;
    }

}
