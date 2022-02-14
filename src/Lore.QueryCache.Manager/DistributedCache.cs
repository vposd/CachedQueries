using System.Text.Json;
using Lore.QueryCache.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace Lore.QueryCache;

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
        return cachedResponse is not null
            ? JsonSerializer.Deserialize<T>(cachedResponse)
            : default;
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