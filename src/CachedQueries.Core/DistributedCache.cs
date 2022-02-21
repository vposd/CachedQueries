using System.Text.Json;
using CachedQueries.Core.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace CachedQueries.Core;

/// <summary>
/// Cache service using IDistributedCache implementation.
/// </summary>
public class DistributedCache : ICache
{
    private readonly IDistributedCache _cache;

    public DistributedCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task DeleteAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var cachedResponse = await _cache.GetStringAsync(key);
        try
        {
            return cachedResponse is not null
                ? JsonSerializer.Deserialize<T>(cachedResponse)
                : default;
        }
        catch (Exception)
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expire = null)
    {
            var response = JsonSerializer.Serialize(value);
            await _cache.SetStringAsync(
                key,
                response,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expire });
    }
}