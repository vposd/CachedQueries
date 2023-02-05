namespace CachedQueries.Core.Interfaces;

public interface ILockManager
{
    Task CheckLockAsync(string key, CancellationToken cancellationToken = default);
    Task LockAsync(string key, TimeSpan timespan, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(string key);
}
