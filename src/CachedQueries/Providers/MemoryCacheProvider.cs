using System.Collections.Concurrent;
using CachedQueries.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Providers;

/// <summary>
/// In-memory cache provider using IMemoryCache.
/// </summary>
public sealed class MemoryCacheProvider(IMemoryCache cache, ILogger<MemoryCacheProvider> logger) : ICacheProvider
{
    // Track keys by tag for invalidation (ConcurrentDictionary for thread-safe removal)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tagToKeys = new();

    // Track all keys for clear operation (ConcurrentDictionary for thread-safe removal)
    private readonly ConcurrentDictionary<string, byte> _allKeys = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = cache.TryGetValue<T>(key, out var value) ? value : default;

        if (result is not null)
            logger.LogDebug("Cache hit for key: {CacheKey}", key);
        else
            logger.LogDebug("Cache miss for key: {CacheKey}", key);

        return Task.FromResult(result);
    }

    public Task SetAsync<T>(string key, T value, CachingOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entryOptions = new MemoryCacheEntryOptions();

        if (options.UseSlidingExpiration)
            entryOptions.SlidingExpiration = options.Expiration;
        else
            entryOptions.AbsoluteExpirationRelativeToNow = options.Expiration;

        // Register callback to clean up tracking when entry is evicted
        entryOptions.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            RemoveKeyFromTracking(evictedKey.ToString()!);
        });

        cache.Set(key, value, entryOptions);
        _allKeys[key] = 0;

        // Track tags
        foreach (var tag in options.Tags)
        {
            var keys = _tagToKeys.GetOrAdd(tag, _ => new());
            keys[key] = 0;
        }

        logger.LogDebug("Cached value for key: {CacheKey}, Expiration: {Expiration}", key, options.Expiration);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        cache.Remove(key);
        RemoveKeyFromTracking(key);

        logger.LogDebug("Removed cache key: {CacheKey}", key);

        return Task.CompletedTask;
    }

    public Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var invalidatedCount = 0;

        foreach (var tag in tags)
        {
            if (_tagToKeys.TryRemove(tag, out var keys))
            {
                foreach (var key in keys.Keys)
                {
                    cache.Remove(key);
                    RemoveKeyFromTracking(key);
                    invalidatedCount++;
                }
            }
        }

        if (invalidatedCount > 0)
            logger.LogInformation("Invalidated {Count} cache entries by tags", invalidatedCount);

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var key in _allKeys.Keys)
        {
            cache.Remove(key);
        }

        _allKeys.Clear();
        _tagToKeys.Clear();

        logger.LogInformation("Cleared all cache entries");

        return Task.CompletedTask;
    }

    private void RemoveKeyFromTracking(string key)
    {
        _allKeys.TryRemove(key, out _);

        foreach (var tagEntry in _tagToKeys)
        {
            tagEntry.Value.TryRemove(key, out _);
        }
    }
}
