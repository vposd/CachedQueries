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
    private readonly ILogger<MemoryCache> _logger;
    private readonly CacheOptions _options;

    private readonly JsonSerializerOptions _settings = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
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
        try
        {
            if (useLock)
                await _lockManager.CheckLockAsync(key, cancellationToken);

            _cache.TryGetValue(key, out var value);
            var cachedResponse = value?.ToString();

            var result = cachedResponse is not null
                ? JsonSerializer.Deserialize<T>(cachedResponse, _settings)
                : default;
            return result;
        }
        catch (Exception exception)
        {
            Log(LogLevel.Error, "Error loading cached data: @{Message}", exception.Message);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, bool useLock = true, TimeSpan? expire = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value, _settings);

            if (useLock)
                await _lockManager.LockAsync(key, _options.LockTimeout, cancellationToken);

            _cache.Set(key, serialized, new MemoryCacheEntryOptions { SlidingExpiration = expire });

            if (useLock)
                await _lockManager.ReleaseLockAsync(key);
        }
        catch (Exception exception)
        {
            if (useLock)
                await _lockManager.ReleaseLockAsync(key);

            Log(LogLevel.Error, "Error setting cached data: @{Message}", exception.Message);
        }
    }

    public async Task DeleteAsync(string key, bool useLock = true, CancellationToken cancellationToken = default)
    {
        try
        {
            _cache.Remove(key);
        }
        catch (Exception exception)
        {
            Log(LogLevel.Error, "Error delete cached data: @{Message}", exception.Message);
        }
    }

    public void Log(LogLevel logLevel, string? message, params object?[] args)
    {
        _logger.Log(logLevel, message, args);
    }
}