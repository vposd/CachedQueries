using System;
using System.Threading.Tasks;

namespace CachedQueries.Core.Interfaces;

/// <summary>
/// Cache service interface
/// </summary>
public interface ICache
{
    /// <summary>
    /// Get results from cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <typeparam name="T">TEntity type</typeparam>
    /// <returns></returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set value to cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value</param>
    /// <param name="expire">Expiration timespan</param>
    /// <typeparam name="T">TEntity type</typeparam>
    /// <returns></returns>
    Task SetAsync<T>(string key, T value, TimeSpan? expire = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove item from cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <returns></returns>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}