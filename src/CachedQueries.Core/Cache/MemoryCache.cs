using System.Text.Json;
using System.Text.Json.Serialization;
using CachedQueries.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Core.Cache;

/// <summary>
///     Cache service using IMemoryCache implementation.
/// </summary>
public class MemoryCache : ICacheStore
{
    private readonly IMemoryCache _cache;
    private readonly ILockManager _lockManager;
    private readonly CacheOptions _options;
    private readonly ILogger<MemoryCache> _logger;

    private readonly JsonSerializerOptions _settings = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve
    };

    public MemoryCache(IMemoryCache cache, ILoggerFactory loggerFactory, ILockManager lockManager, CacheOptions options)
    {
        _cache = cache;
        _lockManager = lockManager;
        _options = options;
        _logger = loggerFactory.CreateLogger<MemoryCache>();
    }

    public async Task<T?> GetAsync<T>(string key, bool useLock = true, CancellationToken cancellationToken = default)
    {
        if (useLock)
            await _lockManager.LockAsync(key, _options.LockTimeout);

        var cachedResponse = _cache.Get(key)?.ToString();

        if (useLock)
            await _lockManager.ReleaseLockAsync(key);

        try
        {
            var result = cachedResponse is not null
                ? JsonSerializer.Deserialize<T>(cachedResponse, _settings)
                : default;
            return result;
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
        var serialized = JsonSerializer.Serialize(value, _settings);

        if (useLock)
            await _lockManager.LockAsync(key, _options.LockTimeout);

        _cache.Set(key, serialized, new MemoryCacheEntryOptions { SlidingExpiration = expire });

        if (useLock)
            await _lockManager.ReleaseLockAsync(key);
    }

    public async Task DeleteAsync(string key, bool useLock = true, CancellationToken cancellationToken = default)
    {
        if (useLock)
            await _lockManager.LockAsync(key, _options.LockTimeout);

        _cache.Remove(key);

        if (useLock)
            await _lockManager.ReleaseLockAsync(key);
    }

    public void Log(LogLevel logLevel, string? message, params object?[] args)
    {
        _logger.Log(logLevel, message, args);
    }
}
