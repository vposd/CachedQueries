using CachedQueries.Core.Interfaces;

namespace CachedQueries.Core;

public class NullLockManager : ILockManager
{
    public Task CheckLockAsync(string key, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task LockAsync(string key, TimeSpan timespan, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ReleaseLockAsync(string key)
    {
        return Task.CompletedTask;
    }
}
