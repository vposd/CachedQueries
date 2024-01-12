using Microsoft.Extensions.Logging;

namespace CachedQueries.Core.Interfaces;

/// <summary>
///     Cache service interface
/// </summary>
public interface ICacheStore
{
    /// <summary>
    ///     Get results from cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="useLock">Use lock when set</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T">TEntity type</typeparam>
    /// <returns></returns>
    Task<T?> GetAsync<T>(string key, bool useLock = true, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Set value to cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value</param>
    /// <param name="useLock">Use lock when set</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T">TEntity type</typeparam>
    /// <returns></returns>
    Task SetAsync<T>(string key, T value, bool useLock = true, TimeSpan? expire = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Remove item from cache
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="useLock">Use lock when set</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteAsync(string key, bool useLock = true, CancellationToken cancellationToken = default);

    void Log(LogLevel logLevel, string? message, params object?[] args);
}
