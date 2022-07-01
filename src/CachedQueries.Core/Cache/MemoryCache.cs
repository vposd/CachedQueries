using System.Text.Json;
using System.Text.Json.Serialization;
using CachedQueries.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Core.Cache;

/// <summary>
///     Cache service using IMemoryCache implementation.
/// </summary>
public class MemoryCache : ICache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCache> _logger;

    private readonly JsonSerializerOptions _settings = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve
    };

    public MemoryCache(IMemoryCache cache, ILoggerFactory loggerFactory)
    {
        _cache = cache;
        _logger = loggerFactory.CreateLogger<MemoryCache>();
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var cachedResponse = _cache.Get(key)?.ToString();
        try
        {
            var result = cachedResponse is not null
                ? JsonSerializer.Deserialize<T>(cachedResponse, _settings)
                : default;
            return Task.FromResult(result);
        }
        catch (Exception exception)
        {
            _logger.LogError("Error loading cached data: @{Message}", exception.Message);
            return Task.FromResult<T?>(default);
        }
    }

    public async Task SetAsync<T>(string key, T value, bool useLock = true, TimeSpan? expire = null,
        CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(value, _settings);

        if (useLock)
            await CacheManager.LockManager.LockAsync(key, CacheManager.LockTimeout);

        _cache.Set(key, serialized, new MemoryCacheEntryOptions { SlidingExpiration = expire });

        if (useLock)
            await CacheManager.LockManager.ReleaseLockAsync(key);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await CacheManager.LockManager.LockAsync(key, CacheManager.LockTimeout);
        _cache.Remove(key);
        await CacheManager.LockManager.ReleaseLockAsync(key);
    }

    public void Log(LogLevel logLevel, string? message, params object?[] args)
    {
        _logger.Log(logLevel, message, args);
    }
}
