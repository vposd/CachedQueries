namespace CachedQueries.Core.Interfaces;

public interface ILockManager
{
    Task CheckLockAsync(string key, CancellationToken cancellationToken = default);
    Task LockAsync(string key, TimeSpan timespan);
    Task ReleaseLockAsync(string key);
}
