using CachedQueries.Core.Models;
using CachedQueries.DependencyInjection;

namespace CachedQueries.Linq;

/// <summary>
///     Provides extension methods for querying entities and converting them to a list with caching support.
/// </summary>
public static class ToListExtensions
{
    /// <summary>
    ///     Asynchronously converts an IQueryable sequence to a cached list using the specified caching options.
    /// </summary>
    /// <param name="query">The IQueryable source to convert.</param>
    /// <param name="options">The caching options to apply.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <returns>A collection of elements converted from the sequence.</returns>
    public static async Task<ICollection<T>> ToListCachedAsync<T>(this IQueryable<T> query,
        CachingOptions options,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var result = await cacheManager.CacheCollectionStrategy.ExecuteAsync(query, options, cancellationToken);
        return result;
    }

    /// <summary>
    ///     Asynchronously converts an IQueryable sequence to a cached list using default caching options configured in the
    ///     cache manager.
    /// </summary>
    /// <param name="query">The IQueryable source to convert.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <returns>A collection of elements converted from the sequence.</returns>
    public static async Task<ICollection<T>> ToListCachedAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var defaultOption = new CachingOptions
        {
            CacheDuration = cacheManager.Config.DefaultCacheDuration
        };

        return await query.ToListCachedAsync(defaultOption, cancellationToken);
    }
}
