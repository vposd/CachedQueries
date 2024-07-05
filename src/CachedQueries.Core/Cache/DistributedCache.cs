﻿using System.Text.Json;
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
    private readonly ILogger<DistributedCache> _logger;
    private readonly CacheOptions _options;

    private readonly JsonSerializerOptions _settings = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public DistributedCache(IDistributedCache cache, ILoggerFactory loggerFactory, ILockManager lockManager,
        CacheOptions options)
    {
        _cache = cache;
        _lockManager = lockManager;
        _options = options;
        _logger = loggerFactory.CreateLogger<DistributedCache>();
    }

    public async Task DeleteAsync(string key, bool useLock = true, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception exception)
        {
            Log(LogLevel.Error, "Error delete cached data: @{Message}", exception.Message);
        }
    }

    public async Task<T?> GetAsync<T>(string key, bool useLock = true, CancellationToken cancellationToken = default)
    {
        try
        {
            if (useLock)
            {
                await _lockManager.CheckLockAsync(key, cancellationToken);
            }

            var cachedResponse = await _cache.GetAsync(key, cancellationToken);

            return cachedResponse is not null
                ? JsonSerializer.Deserialize<T>(cachedResponse, _settings)
                : default;
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
        var isLockAcquired = false;
        try
        {
            var response = JsonSerializer.SerializeToUtf8Bytes(value, _settings);

            if (useLock)
            {
                await _lockManager.LockAsync(key, _options.LockTimeout, cancellationToken);
                isLockAcquired = true;
            }

            await _cache.SetAsync(
                key,
                response,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expire },
                cancellationToken);
        }
        catch (Exception exception)
        {
            Log(LogLevel.Error, "Error setting cached data: @{Message}", exception.Message);
        }
        finally
        {
            try
            {
                if (useLock && isLockAcquired)
                {
                    _lockManager.ReleaseLockAsync(key).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, "Error releasing lock: @{Message}", ex.Message);
            }
        }
    }

    public void Log(LogLevel logLevel, string? message, params object?[] args)
    {
        _logger.Log(logLevel, message, args);
    }
}
