using CachedQueries.Core;

namespace CachedQueries.Test.Linq.Helpers;

public class EmptyKeyCacheFactory : DefaultCacheKeyFactory
{
    public override string GetCacheKey<T>(IQueryable<T> query, string[] tags)
    {
        return string.Empty;
    }
}
