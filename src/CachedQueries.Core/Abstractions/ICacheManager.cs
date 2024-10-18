using CachedQueries.Core.Models;

namespace CachedQueries.Core.Abstractions;

/// <summary>
///     Defines the contract for managing caching operations, strategies, and configuration in an application.
/// </summary>
public interface ICacheManager
{
    /// <summary>
    ///     Gets the strategy used for handling cache collections, such as managing cache entries in batches or groups.
    /// </summary>
    ICacheCollectionStrategy CacheCollectionStrategy { get; }

    /// <summary>
    ///     Gets the strategy used for handling individual cache entries, including rules for adding, retrieving, and expiring
    ///     cached items.
    /// </summary>
    ICacheEntryStrategy CacheEntryStrategy { get; }

    /// <summary>
    ///     Gets the service responsible for invalidating cache entries based on specific conditions, such as time-based or
    ///     event-based expiration.
    /// </summary>
    ICacheInvalidator CacheInvalidator { get; }

    /// <summary>
    ///     Gets the service responsible for generating cache keys, which are used to identify and retrieve cached items.
    /// </summary>
    ICacheKeyFactory CacheKeyFactory { get; }

    /// <summary>
    ///     Gets the provider responsible for handling the context in which cache operations occur, such as managing scoped or
    ///     shared cache contexts.
    /// </summary>
    ICacheContextProvider CacheContextProvider { get; }

    /// <summary>
    ///     Gets the configuration settings for caching, including default expiration times and other cache-specific options.
    /// </summary>
    CachedQueriesConfig Config { get; }
}
