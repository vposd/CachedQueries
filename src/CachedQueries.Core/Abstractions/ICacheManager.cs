using CachedQueries.Core.Models;

namespace CachedQueries.Core.Abstractions;

public interface ICacheManager
{
    public ICacheCollectionStrategy CacheCollectionStrategy { get; }
    public ICacheEntryStrategy CacheEntryStrategy { get; }
    public ICacheInvalidator CacheInvalidator { get; }
    public ICacheKeyFactory CacheKeyFactory { get; }
    public ICacheContextProvider CacheContextProvider { get; }
    public CachedQueriesConfig Config { get; }
}
