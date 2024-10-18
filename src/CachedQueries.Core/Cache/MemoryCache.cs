using System.Text.Json;
using System.Text.Json.Serialization;
using CachedQueries.Core.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Core.Cache;

/// <summary>
///     Cache service using IMemoryCache implementation.
/// </summary>
public class MemoryCache(IMemoryCache cache, ILoggerFactory loggerFactory)
    : ICacheStore
{
    private readonly ILogger<MemoryCache> _logger = loggerFactory.CreateLogger<MemoryCache>();

    private readonly JsonSerializerOptions _settings = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            cache.TryGetValue(key, out var value);
            var cachedResponse = value?.ToString();

            var result = cachedResponse is not null
                ? JsonSerializer.Deserialize<T>(cachedResponse, _settings)
                : default;
            return Task.FromResult(result);
        }
        catch (Exception exception)
        {
            _logger.LogError("Error loading cached data: @{Message}", exception.Message);
            return Task.FromResult(default(T));
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expire = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value, _settings);
            cache.Set(key, serialized, new MemoryCacheEntryOptions { SlidingExpiration = expire });
        }
        catch (Exception exception)
        {
            _logger.LogError("Error setting cached data: @{Message}", exception.Message);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            cache.Remove(key);
        }
        catch (Exception exception)
        {
            _logger.LogError("Error delete cached data: @{Message}", exception.Message);
        }

        return Task.CompletedTask;
    }
}
