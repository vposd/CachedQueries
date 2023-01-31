using CachedQueries.Core.Interfaces;

namespace CachedQueries.Core;

public class DefaultCacheInvalidator : ICacheInvalidator
{
    private readonly ICacheStore _cache;

    public DefaultCacheInvalidator(ICacheStore cache)
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

        var tagsToExpireTasks = tagsList.Distinct()
            .Select(tagKey => _cache.GetAsync<List<string>>(tagKey, false, cancellationToken))
            .ToList();

        await Task.WhenAll(tagsToExpireTasks);

        foreach (var list in tagsToExpireTasks.Select(x => x.Result ?? new List<string>()))
            keysToRemove.AddRange(list);

        var tasks = keysToRemove
            .Distinct()
            .Select(item => _cache.DeleteAsync(item, false, cancellationToken));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    ///     Async link invalidation tags to cache key
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    public async Task LinkTagsAsync(string key, IEnumerable<string> tags,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var tagsToLink = tags.Distinct().ToList();

        async Task LinkTagAsync(string tag)
        {
            var list = await _cache.GetAsync<List<string>>(tag, false, cancellationToken) ?? new List<string>();

            if (!list.Contains(key))
                list.Add(key);

            await _cache.SetAsync(tag, list.Distinct(), false, null, cancellationToken);
        }

        await Task.WhenAll(tagsToLink.Select(LinkTagAsync));
    }
}