namespace Lore.QueryCache.Interfaces;

public interface ICacheKeyFactory
{
    string GetCacheKey<T>(IQueryable<T> query, IEnumerable<string> tags) where T : class;
}