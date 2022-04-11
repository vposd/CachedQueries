using System.Text.Json;
using System.Text.Json.Serialization;
using CachedQueries.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Core;

/// <summary>
/// Cache service using IMemoryCache implementation.
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

    public Task SetAsync<T>(string key, T value, TimeSpan? expire = null, CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(value, _settings);
        _cache.Set(key, serialized, new MemoryCacheEntryOptions { SlidingExpiration = expire });
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
    
    public void Log(LogLevel logLevel, string? message, params object?[] args)
    {
        _logger.Log(logLevel, message, args);
    }
}