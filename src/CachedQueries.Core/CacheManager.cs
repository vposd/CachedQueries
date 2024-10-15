using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;

namespace CachedQueries.Core;

public class CacheManager(
    ICacheEntryStrategy cacheEntryStrategy,
    ICacheCollectionStrategy cacheCollectionStrategy,
    ICacheInvalidator cacheInvalidator,
    ICacheKeyFactory cacheKeyFactory,
    CachingOptions defaultCachingOptions)
    : ICacheManager
{
    public ICacheCollectionStrategy CacheCollectionStrategy { get; } = cacheCollectionStrategy;
    public ICacheEntryStrategy CacheEntryStrategy { get; } = cacheEntryStrategy;
    public ICacheInvalidator CacheInvalidator { get; } = cacheInvalidator;
    public ICacheKeyFactory CacheKeyFactory { get; } = cacheKeyFactory;
    public CachingOptions DefaultCachingOptions { get; } = defaultCachingOptions;
}
