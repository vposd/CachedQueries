using Lore.QueryCache.Interfaces;

namespace Lore.QueryCache;

public static class CacheManager
{
    public static ICacheKeyFactory CacheKeyFactory { get; set; } = new CacheKeyFactory();

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

    public static string CachePrefix
    {
        set => _cachePrefix = value;
    }

    private static string _cachePrefix = "lore_";
    private static ICache? _cache;

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

    public static void ExpireTags(IEnumerable<string> tags)
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

    public static async Task ExpireTagsAsync(IEnumerable<string> tags)
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