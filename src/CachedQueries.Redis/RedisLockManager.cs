using CachedQueries.Core;
using CachedQueries.Core.Interfaces;
using StackExchange.Redis;

namespace CachedQueries.Redis;

public class RedisLockManager : ILockManager
{
    private readonly CacheOptions _cacheOptions;
    private readonly IDatabase _database;

    public RedisLockManager(IConnectionMultiplexer multiplexer, CacheOptions cacheOptions)
    {
        _cacheOptions = cacheOptions;
        _database = multiplexer.GetDatabase();
    }

    public async Task LockAsync(string key, TimeSpan timespan, CancellationToken cancellationToken = default)
    {
        var lockAchieved = false;
        var totalTime = TimeSpan.Zero;
        var maxTime = _cacheOptions.LockTimeout;
        var expiration = _cacheOptions.LockTimeout;
        var sleepTime = TimeSpan.FromMilliseconds(50);

        while (!lockAchieved && totalTime < maxTime)
        {
            lockAchieved = _database.LockTake(key, GetLockValue(key), expiration);
            if (lockAchieved)
            {
                continue;
            }

            await Task.Delay(sleepTime, cancellationToken);
            totalTime += sleepTime;
        }
    }

    public async Task ReleaseLockAsync(string key)
    {
        await _database.LockReleaseAsync(key, GetLockValue(key));
    }

    public async Task CheckLockAsync(string key, CancellationToken cancellationToken = default)
    {
        var totalTime = TimeSpan.Zero;
        var maxTime = _cacheOptions.LockTimeout;
        var sleepTime = TimeSpan.FromMilliseconds(50);
        var lockAchieved = false;

        while (!lockAchieved && totalTime < maxTime)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var lockValue = _database.LockQuery(key);
            lockAchieved = !lockValue.HasValue;

            if (lockAchieved)
            {
                continue;
            }

            await Task.Delay(sleepTime, cancellationToken);
            totalTime += sleepTime;
        }
    }

    private static string GetLockValue(string key)
    {
        return key + "_lock";
    }
}
