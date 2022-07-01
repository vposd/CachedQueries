using CachedQueries.Core.Interfaces;

namespace CachedQueries.Core;

public class DefaultCacheInvalidator : ICacheInvalidator
{
    private readonly ICache _cache;

    public DefaultCacheInvalidator(ICache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Async remove all cache entries linked to provided invalidation tags
    /// </summary>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    public async Task InvalidateCacheAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var tagsList = tags.ToList();
        var keysToRemove = new List<string>(tagsList);

        var tagsToExpireTasks = tagsList.Distinct().Select(tag => CacheManager.CachePrefix + tag)
            .Select(tagKey => _cache.GetAsync<List<string>>(tagKey, cancellationToken))
            .ToList();

        await Task.WhenAll(tagsToExpireTasks);

        foreach (var list in tagsToExpireTasks.Select(x => x.Result ?? new List<string>()))
            keysToRemove.AddRange(list);

        var tasks = keysToRemove
            .Distinct()
            .Select(item => _cache.DeleteAsync(item, cancellationToken));

        await Task.WhenAll(tasks);
    }
}
