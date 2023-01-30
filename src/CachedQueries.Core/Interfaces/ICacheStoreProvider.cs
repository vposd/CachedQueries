using CachedQueries.Core.Enums;

namespace CachedQueries.Core.Interfaces;

public interface ICacheStoreProvider
{
    ICacheStore GetCacheStore(string key, IEnumerable<string> tags, CacheContentType contentType);
}
