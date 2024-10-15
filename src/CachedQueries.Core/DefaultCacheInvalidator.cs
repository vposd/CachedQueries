using CachedQueries.Core.Abstractions;

namespace CachedQueries.Core;

public class DefaultCacheInvalidator(ICacheStore cache) : ICacheInvalidator
{
    /// <summary>
    ///     Async remove all cache entries linked to provided invalidation tags
    /// </summary>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    public async Task InvalidateCacheAsync(string[] tags, CancellationToken cancellationToken = default)
    {
        var tagsList = tags.ToList();
        var keysToRemove = new List<string>(tagsList);

        var tagsToExpireTasks = tagsList.Distinct()
            .Select(tagKey => cache.GetAsync<List<string>>(tagKey, cancellationToken))
            .ToList();

        await Task.WhenAll(tagsToExpireTasks);

        foreach (var list in tagsToExpireTasks.Select(x => x.Result ?? new List<string>()))
        {
            keysToRemove.AddRange(list);
        }

        var tasks = keysToRemove
            .Distinct()
            .Select(item => cache.DeleteAsync(item, cancellationToken));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    ///     Async link invalidation tags to cache key
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    public async Task LinkTagsAsync(string key, string[] tags,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var tagsToLink = tags.Distinct().ToList();
        await Task.WhenAll(tagsToLink.Select(tag => LinkTagAsync(key, tag, cancellationToken)));
    }

    private async Task LinkTagAsync(string key, string tag, CancellationToken cancellationToken)
    {
        var list = await cache.GetAsync<List<string>>(tag, cancellationToken) ?? [];

        if (!list.Contains(key))
        {
            list.Add(key);
        }

        await cache.SetAsync(tag, list.Distinct(), null, cancellationToken);
    }
}
