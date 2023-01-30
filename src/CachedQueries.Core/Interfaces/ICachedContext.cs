namespace CachedQueries.Core.Interfaces;

public interface ICachedContext
{
    ICacheManager CacheManager { get; }
}
