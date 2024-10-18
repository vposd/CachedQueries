using CachedQueries.Core;
using CachedQueries.Core.Abstractions;

namespace CachedQueries.Test.Linq.Helpers;

public class EmptyKeyCacheFactory(ICacheContextProvider cacheContext) : DefaultCacheKeyFactory(cacheContext)
{
    public override string GetCacheKey<T>(IQueryable<T> query, string[] tags)
    {
        return string.Empty;
    }
}
