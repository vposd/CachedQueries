using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;

namespace CachedQueries.Core.Strategies;

public class DefaultCacheEntryStrategy(
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
            return query.FirstOrDefault();
        }

        var cached = await cacheStore.GetAsync<T>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var value = query.FirstOrDefault();
        if (value is null)
        {
            return default;
        }

        await cacheStore.SetAsync(key, value, options.CacheDuration, cancellationToken);
        await cacheInvalidator.LinkTagsAsync(key, options.Tags, cancellationToken);

        return value;
    }
}
