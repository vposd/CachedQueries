using CachedQueries.Core.Enums;

namespace CachedQueries.Core.Interfaces;

public interface ICacheStoreProvider
{
    /// <summary>
    /// Returns target cache store by content type to split implementations for collections and single objects
    /// </summary>
    /// <param name="key"></param>
    /// <param name="tags"></param>
    /// <param name="contentType"></param>
    /// <returns></returns>
    ICacheStore GetCacheStore(string key, IEnumerable<string> tags, CacheContentType contentType);
}