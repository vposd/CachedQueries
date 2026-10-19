namespace CachedQueries.Abstractions;

/// <summary>
///     Abstraction for cache storage operations. Implement it to plug in a custom backend.
/// </summary>
/// <remarks>
///     Contract expected by CachedQueries:
///     <list type="bullet">
///         <item>Implementations must be thread-safe; one instance is shared by all requests (singleton).</item>
///         <item>
///             <see cref="GetAsync{T}" /> returns <c>default</c> on a miss. Scalars are requested as nullable
///             (<c>int?</c>, <c>bool?</c>) so that a cached <c>0</c>/<c>false</c> can be told apart from a miss.
///         </item>
///         <item>
///             <see cref="CachingOptions.Tags" /> passed to <see cref="SetAsync{T}" /> are fully qualified tracking tags
///             (entity types, user tags and context). The provider must associate the entry with each of them so
///             that <see cref="InvalidateByTagsAsync" /> removes it. Tags must be treated as opaque strings.
///         </item>
///         <item>
///             An entry written concurrently with an invalidation of one of its tags must not survive that
///             invalidation untracked (i.e. it must either be removed or remain invalidatable).
///         </item>
///         <item>
///             Distributed implementations should swallow and log transient backend failures in
///             <see cref="GetAsync{T}" />/<see cref="SetAsync{T}" /> (treating them as misses) so that the cache never
///             breaks the query path.
///         </item>
///     </list>
/// </remarks>
public interface ICacheProvider
{
    /// <summary>
    ///     Gets a cached value by key, or <c>default</c> when the key is not cached.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sets a value in cache with the specified expiration and tracking tags.
    /// </summary>
    Task SetAsync<T>(string key, T value, CachingOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes a value from cache by key.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes all cached values associated with any of the specified tags.
    /// </summary>
    Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clears all values cached by this provider.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
