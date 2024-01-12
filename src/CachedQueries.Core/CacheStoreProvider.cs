using CachedQueries.Core.Enums;
using CachedQueries.Core.Interfaces;

namespace CachedQueries.Core;

public class CacheStoreProvider : ICacheStoreProvider
{
    private readonly ICacheStore _cacheStore;

    public CacheStoreProvider(ICacheStore cacheStore)
    {
        _cacheStore = cacheStore;
    }

    public ICacheStore GetCacheStore(string key, IEnumerable<string> tags, CacheContentType contentType)
    {
        return _cacheStore;
    }
}
