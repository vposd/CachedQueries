using System.Collections.Concurrent;
using CachedQueries.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace CachedQueries.Providers;

/// <summary>
///     In-process cache provider backed by <see cref="IMemoryCache" />.
/// </summary>
/// <remarks>
///     <para>
///         Tag invalidation uses one <see cref="CancellationTokenSource" /> per tag, attached to every entry carrying
///         that tag as an expiration token. Invalidating a tag cancels its token, which expires all of its entries
///         atomically. An entry stored with an already-cancelled token is never added, so a write racing with an
///         invalidation of one of its tags cannot leave an untracked entry behind.
///     </para>
///     <para>
///         Cached values are returned by reference: every caller receives the same instance. Treat results
///         obtained through the cache as read-only.
///     </para>
///     <para>
///         The provider is only suitable for a single application instance. Use a distributed provider
///         (e.g. <c>CachedQueries.Redis</c>) when several instances must observe the same invalidations.
///     </para>
/// </remarks>
public sealed class MemoryCacheProvider(IMemoryCache cache, ILogger<MemoryCacheProvider> logger) : ICacheProvider
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tagTokens = new();

    // Attached to every entry so ClearAsync can expire all entries written by this provider.
    private CancellationTokenSource _clearToken = new();

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = cache.TryGetValue<T>(key, out var value) ? value : default;

        if (result is not null)
        {
            logger.LogDebug("Cache hit for key: {CacheKey}", key);
        }
        else
        {
            logger.LogDebug("Cache miss for key: {CacheKey}", key);
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, CachingOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entryOptions = new MemoryCacheEntryOptions();

        if (options.UseSlidingExpiration)
        {
            entryOptions.SlidingExpiration = options.Expiration;
        }
        else
        {
            entryOptions.AbsoluteExpirationRelativeToNow = options.Expiration;
        }

        entryOptions.AddExpirationToken(new CancellationChangeToken(Volatile.Read(ref _clearToken).Token));
        foreach (var tag in options.Tags)
        {
            var tagToken = _tagTokens.GetOrAdd(tag, _ => new CancellationTokenSource());
            entryOptions.AddExpirationToken(new CancellationChangeToken(tagToken.Token));
        }

        cache.Set(key, value, entryOptions);

        logger.LogDebug("Cached value for key: {CacheKey}, Expiration: {Expiration}", key, options.Expiration);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        cache.Remove(key);

        logger.LogDebug("Removed cache key: {CacheKey}", key);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var invalidatedCount = 0;

        foreach (var tag in tags)
        {
            if (_tagTokens.TryRemove(tag, out var tagToken))
            {
                // Not disposed on purpose: a concurrent SetAsync may still read its Token.
                tagToken.Cancel();
                invalidatedCount++;
            }
        }

        if (invalidatedCount > 0)
        {
            logger.LogInformation("Invalidated cache entries for {Count} tags", invalidatedCount);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Exchange(ref _clearToken, new CancellationTokenSource()).Cancel();

        foreach (var tag in _tagTokens.Keys)
        {
            if (_tagTokens.TryRemove(tag, out var tagToken))
            {
                tagToken.Cancel();
            }
        }

        logger.LogInformation("Cleared all cache entries");

        return Task.CompletedTask;
    }
}
