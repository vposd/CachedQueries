using System.Linq.Expressions;
using CachedQueries.Core.Models;
using CachedQueries.DependencyInjection;

namespace CachedQueries.Linq;

/// <summary>
///     Provides extension methods for querying entities with caching support using the FirstOrDefault pattern.
/// </summary>
public static class FirstOrDefaultExtensions
{
    /// <summary>
    ///     Asynchronously retrieves the first element of a sequence that satisfies a specified condition,
    ///     using caching options for improved performance.
    /// </summary>
    /// <param name="query">The IQueryable source to query.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="options">The caching options to apply.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <returns>The first element that matches the specified condition, or null if no such element is found.</returns>
    public static async Task<T?> FirstOrDefaultCachedAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CachingOptions options,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var result =
            await cacheManager.CacheEntryStrategy.ExecuteAsync(query.Where(predicate), options, cancellationToken);
        return result;
    }

    /// <summary>
    ///     Asynchronously retrieves the first element of a sequence that satisfies a specified condition,
    ///     using default caching options configured in the cache manager.
    /// </summary>
    /// <param name="query">The IQueryable source to query.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <returns>The first element that matches the specified condition, or null if no such element is found.</returns>
    public static async Task<T?> FirstOrDefaultCachedAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var defaultOptions = new CachingOptions
        {
            CacheDuration = cacheManager.Config.DefaultCacheDuration
        };

        return await query.FirstOrDefaultCachedAsync(predicate, defaultOptions, cancellationToken);
    }

    /// <summary>
    ///     Asynchronously retrieves the first element of a sequence using the specified caching options.
    /// </summary>
    /// <param name="query">The IQueryable source to query.</param>
    /// <param name="options">The caching options to apply.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <returns>The first element in the sequence, or null if the sequence is empty.</returns>
    public static async Task<T?> FirstOrDefaultCachedAsync<T>(this IQueryable<T> query,
        CachingOptions options,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var result = await cacheManager.CacheEntryStrategy.ExecuteAsync(query, options, cancellationToken);
        return result;
    }

    /// <summary>
    ///     Asynchronously retrieves the first element of a sequence using default caching options configured in the cache
    ///     manager.
    /// </summary>
    /// <param name="query">The IQueryable source to query.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <returns>The first element in the sequence, or null if the sequence is empty.</returns>
    public static async Task<T?> FirstOrDefaultCachedAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken = default)
    {
        var cacheManager = CacheManagerContainer.Resolve();
        var defaultOptions = new CachingOptions
        {
            CacheDuration = cacheManager.Config.DefaultCacheDuration
        };

        return await query.FirstOrDefaultCachedAsync(defaultOptions, cancellationToken);
    }
}
