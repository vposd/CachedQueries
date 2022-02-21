using System.Text.Json;
using CachedQueries.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CachedQueries.Core;

/// <summary>
/// Cache service using IMemoryCache implementation.
/// </summary>
public class MemoryCache : ICache
{
    private readonly IMemoryCache _cache;

    public MemoryCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        var cachedResponse = _cache.Get(key)?.ToString();
        try
        {
            var result = cachedResponse is not null
                ? JsonSerializer.Deserialize<T>(cachedResponse)
                : default;
            return Task.FromResult(result);
        }
        catch (Exception)
        {
            return Task.FromResult<T?>(default);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expire = null)
    {
        var serialized = JsonSerializer.Serialize(value);
        _cache.Set(key, serialized, new MemoryCacheEntryOptions { SlidingExpiration = expire });
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}