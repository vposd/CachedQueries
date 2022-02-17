using CachedQueries.Core.Interfaces;

namespace CachedQueries.Core;

/// <summary>
/// Static cache manager.
/// Contains ICache, ICacheKeyFactory implementation and responsible for link/unlink invalidation tags.
/// </summary>
public static class CacheManager
{
    /// <summary>
    /// ICacheKeyFactory implementation.
    /// Contains base CacheKeyFactory by default.
    /// </summary>
    public static ICacheKeyFactory CacheKeyFactory { get; set; } = new CacheKeyFactory();

    /// <summary>
    /// ICache implemntation
    /// </summary>
    /// <exception cref="ArgumentException">Throws when cache is not defined</exception>
    public static ICache Cache
    {
        get
        {
            if (_cache is null)
                throw new ArgumentException("Cache is not defined");

            return _cache;
        }
        set => _cache = value;
    }

    /// <summary>
    /// Custom cache prefix. Just for lulz.
    /// </summary>
    public static string CachePrefix
    {
        set => _cachePrefix = value;
    }

    private static string _cachePrefix = "lore_";
    private static ICache? _cache;

    /// <summary>
    /// Link invalidation tags to cache key
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="tags">Invalidation tags</param>
    public static void LinkTags(string key, IEnumerable<string> tags)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var tagsToLink = tags.Distinct().Select(tag => _cachePrefix + tag).ToList();
        foreach (var tag in tagsToLink)
        {
            var list = Cache.GetAsync<List<string>>(tag)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult() ?? new List<string>();

            if (!list.Contains(key))
                list.Add(key);

            Cache.SetAsync(tag, list.Distinct()).Wait();
        }
    }

    /// <summary>
    /// Async link invalidation tags to cache key
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="tags">Invalidation tags</param>
    public static async Task LinkTagsAsync(string key, IEnumerable<string> tags)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var tagsToLink = tags.Distinct().Select(tag => _cachePrefix + tag).ToList();
        foreach (var tag in tagsToLink)
        {
            var list = await Cache.GetAsync<List<string>>(tag)
                       ?? new List<string>();

            if (!list.Contains(key))
                list.Add(key);

            await Cache.SetAsync(tag, list.Distinct());
        }
    }

    /// <summary>
    /// Remove all cache entries linked to provided invalidation tags
    /// </summary>
    /// <param name="tags">Invalidation tags</param>
    public static void InvalidateCache(IEnumerable<string> tags)
    {
        var keysToRemove = new List<string>();
        var tagsToExpire = tags.Distinct().Select(tag => _cachePrefix + tag).ToList();

        foreach (var tag in tagsToExpire)
        {
            var list = Cache.GetAsync<List<string>>(tag).ConfigureAwait(false).GetAwaiter()
                .GetResult() ?? new List<string>();

            keysToRemove.AddRange(list);
            keysToRemove.Add(tag);
        }

        foreach (var item in keysToRemove.Distinct().ToList())
            Cache.DeleteAsync(item).Wait();
    }

    /// <summary>
    /// Async remove all cache entries linked to provided invalidation tags
    /// </summary>
    /// <param name="tags">Invalidation tags</param>
    public static async Task InvalidateCacheAsync(IEnumerable<string> tags)
    {
        var keysToRemove = new List<string>();
        var tagsToExpire = tags.Distinct().Select(tag => _cachePrefix + tag).ToList();

        foreach (var tagKey in tagsToExpire)
        {
            var list = await Cache.GetAsync<List<string>>(tagKey) ?? new List<string>();

            keysToRemove.AddRange(list);
            keysToRemove.Add(tagKey);
        }

        foreach (var item in keysToRemove.Distinct().ToList())
            await Cache.DeleteAsync(item);
    }
}