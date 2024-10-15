using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries.EntityFramework.Strategies;

public class CacheEntryStrategy(
    ICacheKeyFactory cacheKeyFactory,
    ICacheInvalidator cacheInvalidator,
    ICacheStore cacheStore) : ICacheEntryStrategy
{
    public async Task<T?> ExecuteAsync<T>(IQueryable<T> query, CachingOptions options,
        CancellationToken cancellationToken = default)
    {
        var key = cacheKeyFactory.GetCacheKey(query, options.Tags);
        if (string.IsNullOrEmpty(key))
        {
            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        var cached = await cacheStore.GetAsync<T>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var value = await query.FirstOrDefaultAsync(cancellationToken);
        await cacheStore.SetAsync(key, value, options.CacheDuration, cancellationToken);
        await cacheInvalidator.LinkTagsAsync(key, options.Tags, cancellationToken);

        return value;
    }
}
