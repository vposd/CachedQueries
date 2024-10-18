using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;

namespace CachedQueries.Core.Strategies;

public class DefaultCacheCollectionStrategy(
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
            return query.ToList();
        }

        var cached = await cacheStore.GetAsync<IEnumerable<T>>(key, cancellationToken);
        if (cached is not null)
        {
            return cached.ToList();
        }

        var value = query.ToList();
        await cacheStore.SetAsync(key, value, options.CacheDuration, cancellationToken);
        await cacheInvalidator.LinkTagsAsync(key, options.Tags, cancellationToken);

        return value;
    }
}
