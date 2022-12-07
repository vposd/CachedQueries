using CachedQueries.Core.Interfaces;

namespace CachedQueries.Core;

/// <summary>
///     Static cache manager.
///     Contains ICache, ICacheKeyFactory implementation and responsible for link/unlink invalidation tags.
/// </summary>
public static class CacheManager
{
    private static ICache? _cache;
    private static ILockManager _lockManager = new DefaultLockManager();
    private static ICacheInvalidator? _cacheInvalidator;

    /// <summary>
    ///     ICacheKeyFactory implementation.
    ///     Contains base CacheKeyFactory by default.
    /// </summary>
    public static ICacheKeyFactory CacheKeyFactory { get; set; } = new CacheKeyFactory();

    /// <summary>
    ///     Lock Timeout
    /// </summary>
    public static TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     Default Lifetime
    /// </summary>
    public static TimeSpan DefaultLifetime { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    ///     ILockManager implementation
    /// </summary>
    /// <exception cref="ArgumentException">Throws when lock manager is not defined</exception>
    public static ILockManager LockManager
    {
        get
        {
            if (_lockManager is null)
                throw new ArgumentException("LockManager is not defined");

            return _lockManager;
        }
        set => _lockManager = value;
    }

    /// <summary>
    ///     ICacheInvalidator implementation
    /// </summary>
    /// <exception cref="ArgumentException">Throws when cache invalidator is not defined</exception>
    public static ICacheInvalidator CacheInvalidator
    {
        get
        {
            if (_cacheInvalidator is null)
                throw new ArgumentException("CacheInvalidator is not defined");

            return _cacheInvalidator;
        }
        set => _cacheInvalidator = value;
    }

    /// <summary>
    ///     ICache implementation
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
    ///     Custom cache prefix. Just for lulz.
    /// </summary>
    public static string CachePrefix { get; set; } = "lore_";

    /// <summary>
    ///     Link invalidation tags to cache key
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="tags">Invalidation tags</param>
    public static void LinkTags(string key, IEnumerable<string> tags)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var tagsToLink = tags.Distinct().Select(tag => CachePrefix + tag).ToList();
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
    ///     Async link invalidation tags to cache key
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    public static async Task LinkTagsAsync(string key, IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        var tagsToLink = tags.Distinct().Select(tag => CachePrefix + tag).ToList();
        foreach (var tag in tagsToLink)
        {
            var list = await Cache.GetAsync<List<string>>(tag, useLock: false, cancellationToken)
                       ?? new List<string>();

            if (!list.Contains(key))
                list.Add(key);

            await Cache.SetAsync(tag, list.Distinct(), useLock: false, expire: null, cancellationToken);
        }
    }

    /// <summary>
    ///     Async remove all cache entries linked to provided invalidation tags
    /// </summary>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    public static async Task InvalidateCacheAsync(IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        await CacheInvalidator.InvalidateCacheAsync(tags, cancellationToken);
    }
}
