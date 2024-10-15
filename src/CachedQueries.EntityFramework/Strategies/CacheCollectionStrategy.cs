using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries.EntityFramework.Strategies;

public class CacheCollectionStrategy(
    ICacheKeyFactory cacheKeyFactory,
    ICacheInvalidator cacheInvalidator,
    ICacheStore cacheStore) : ICacheCollectionStrategy
{
    public async Task<ICollection<T>> ExecuteAsync<T>(IQueryable<T> query, CachingOptions options,
        CancellationToken cancellationToken = default)
    {
        var key = cacheKeyFactory.GetCacheKey(query, options.Tags);
        if (string.IsNullOrEmpty(key))
        {
            return await query.ToListAsync(cancellationToken);
        }

        var cached = await cacheStore.GetAsync<IEnumerable<T>>(key, cancellationToken);
        if (cached is not null)
        {
            return cached.ToList();
        }

        var value = await query.ToListAsync(cancellationToken);
        await cacheStore.SetAsync(key, value, options.CacheDuration, cancellationToken);
        await cacheInvalidator.LinkTagsAsync(key, options.Tags, cancellationToken);

        return value;
    }
}
