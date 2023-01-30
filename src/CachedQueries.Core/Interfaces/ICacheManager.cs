namespace CachedQueries.Core.Interfaces;

public interface ICacheManager {
    ILockManager LockManager { get; }
    ICacheStoreProvider CacheStoreProvider { get; }
    ICacheInvalidator CacheInvalidator { get; }
    ICacheKeyFactory CacheKeyFactory { get; }
    CacheOptions CacheOptions { get; }
}
