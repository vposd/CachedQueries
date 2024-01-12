using CachedQueries.Core.Interfaces;

namespace CachedQueries.Core;

/// <summary>
///     Cache manager
///     Contains ICache, ICacheKeyFactory implementation and responsible for link/unlink invalidation tags.
/// </summary>
public class CacheManager : ICacheManager
{
    public CacheManager(ILockManager lockManager, ICacheStoreProvider cacheStoreProvider,
        ICacheInvalidator cacheInvalidator, ICacheKeyFactory cacheKeyFactory, CacheOptions options)
    {
        LockManager = lockManager;
        CacheStoreProvider = cacheStoreProvider;
        CacheInvalidator = cacheInvalidator;
        CacheKeyFactory = cacheKeyFactory;
        CacheOptions = options;
    }

    public CacheOptions CacheOptions { get; }
    public ILockManager LockManager { get; }
    public ICacheStoreProvider CacheStoreProvider { get; }
    public ICacheInvalidator CacheInvalidator { get; }
    public ICacheKeyFactory CacheKeyFactory { get; }
}
