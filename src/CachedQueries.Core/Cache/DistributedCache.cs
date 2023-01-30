using System.Text.Json;
using System.Text.Json.Serialization;
using CachedQueries.Core.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Core.Cache;

/// <summary>
///     Cache service using IDistributedCache implementation.
/// </summary>
public class DistributedCache : ICacheStore
{
    private readonly IDistributedCache _cache;
    private readonly ILockManager _lockManager;
    private readonly CacheOptions _options;
    private readonly ILogger<DistributedCache> _logger;

    private readonly JsonSerializerOptions _settings = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve
    };

    public DistributedCache(IDistributedCache cache, ILoggerFactory loggerFactory, ILockManager lockManager, CacheOptions options)
    {
        _cache = cache;
        _lockManager = lockManager;
        _options = options;
        _logger = loggerFactory.CreateLogger<DistributedCache>();
    }

    public async Task DeleteAsync(string key, bool useLock = true, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(key, cancellationToken);
    }

    public async Task<T?> GetAsync<T>(string key, bool useLock = true, CancellationToken cancellationToken = default)
    {
        if (useLock)
            await _lockManager.CheckLockAsync(key, cancellationToken);

        var cachedResponse = await _cache.GetStringAsync(key, cancellationToken);

        try
        {
            return cachedResponse is not null
                ? JsonSerializer.Deserialize<T>(cachedResponse, _settings)
                : default;
        }
        catch (Exception exception)
        {
            _logger.LogError("Error loading cached data: @{Message}", exception.Message);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, bool useLock = true, TimeSpan? expire = null,
        CancellationToken cancellationToken = default)
    {
        var response = JsonSerializer.Serialize(value, _settings);

        if (useLock)
            await _lockManager.LockAsync(key, _options.LockTimeout);

        await _cache.SetStringAsync(
            key,
            response,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expire },
            cancellationToken);

        if (useLock)
            await _lockManager.ReleaseLockAsync(key);
    }

    public void Log(LogLevel logLevel, string? message, params object?[] args)
    {
        _logger.Log(logLevel, message, args);
    }
}
