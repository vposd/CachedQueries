namespace CachedQueries.Core.Interfaces;

public interface ILockManager
{
    Task LockAsync(string key, TimeSpan timespan);
    Task ReleaseLockAsync(string key);
}
