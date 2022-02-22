﻿using System.Text.Json;
using System.Text.Json.Serialization;
using CachedQueries.Core.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace CachedQueries.Core;

/// <summary>
/// Cache service using IDistributedCache implementation.
/// </summary>
public class DistributedCache : ICache
{
    private readonly JsonSerializerOptions _settings = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve
    };
    
    private readonly IDistributedCache _cache;

    public DistributedCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(key, cancellationToken);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        var cachedResponse = await _cache.GetStringAsync(key, cancellationToken);
        try
        {
            return cachedResponse is not null
                ? JsonSerializer.Deserialize<T>(cachedResponse, _settings)
                : default;
        }
        catch (Exception)
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expire = null, CancellationToken cancellationToken = default)
    {
        var response = JsonSerializer.Serialize(value, _settings);
            await _cache.SetStringAsync(
                key,
                response,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expire },
                cancellationToken);
    }
}