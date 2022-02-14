using Microsoft.EntityFrameworkCore;

namespace Lore.QueryCache.EntityFramework;

public class QueryCacheKeyFactory : CacheKeyFactory
{
    public override string GetCacheKey<T>(IQueryable<T> query, IEnumerable<string> tags) where T : class
    {
        var command = query.ToQueryString() + string.Join('_', tags.ToList());
        return GetStringSha256Hash(command);
    }
}