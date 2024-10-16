using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;
using CachedQueries.EntityFramework.Extensions;
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
        var tags = options.RetrieveTagsFromQuery ? query.RetrieveRawInvalidationTagsFromQuery() : options.Tags;
        var key = cacheKeyFactory.GetCacheKey(query, tags);
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
        await cacheInvalidator.LinkTagsAsync(key, tags, cancellationToken);

        return value;
    }
}
