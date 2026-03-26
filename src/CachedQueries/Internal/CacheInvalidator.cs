using CachedQueries.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Internal;

/// <summary>
///     Default implementation of cache invalidator.
///     Uses distributed tag-based tracking stored in the cache provider itself,
///     so invalidation works correctly across multiple application instances.
/// </summary>
/// <remarks>
///     Invalidation rule: when an entity type is invalidated, entries matching
///     the current context AND global entries (no context) are removed.
///     Entries belonging to other contexts are left untouched.
/// </remarks>
internal sealed class CacheInvalidator : ICacheInvalidator
{
    private readonly ICacheProvider _defaultProvider;
    private readonly ILogger<CacheInvalidator> _logger;
    private readonly ICacheProviderFactory? _providerFactory;
    private readonly IServiceScopeFactory? _scopeFactory;

    public CacheInvalidator(ICacheProvider cacheProvider, ILogger<CacheInvalidator> logger)
    {
        _defaultProvider = cacheProvider;
        _logger = logger;
    }

    public CacheInvalidator(
        ICacheProvider cacheProvider,
        ICacheProviderFactory providerFactory,
        ILogger<CacheInvalidator> logger)
    {
        _defaultProvider = cacheProvider;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public CacheInvalidator(
        ICacheProvider cacheProvider,
        ICacheProviderFactory providerFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<CacheInvalidator> logger)
    {
        _defaultProvider = cacheProvider;
        _providerFactory = providerFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    ///     Invalidates cache entries for entity types by delegating to the cache provider's
    ///     tag-based invalidation. Builds tag names for both global and current-context entries.
    /// </summary>
    public async Task InvalidateAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default)
    {
        var currentContext = GetCurrentContextKey();
        var tags = TrackingTags.InvalidationTagsForEntityTypes(entityTypes, currentContext);

        if (tags.Count == 0)
        {
            return;
        }

        await InvalidateByProviderTagsAsync(tags, cancellationToken);

        _logger.LogInformation("Invalidated cache for entity types via {TagCount} tags", tags.Count);
    }

    /// <summary>
    ///     Invalidates cache entries for user-defined tags by delegating to the cache provider's
    ///     tag-based invalidation. Builds tag names for both global and current-context entries.
    /// </summary>
    public async Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var currentContext = GetCurrentContextKey();
        var qualifiedTags = TrackingTags.InvalidationTagsForUserTags(tags, currentContext);

        if (qualifiedTags.Count == 0)
        {
            return;
        }

        await InvalidateByProviderTagsAsync(qualifiedTags, cancellationToken);

        _logger.LogInformation("Invalidated cache for {TagCount} tags", qualifiedTags.Count);
    }

    /// <summary>
    ///     No-op. Tracking is now handled by the cache provider through enriched tags in SetAsync.
    ///     Kept for backward compatibility.
    /// </summary>
    public void RegisterCacheEntry(string cacheKey, IEnumerable<Type> entityTypes, string? contextKey = null)
    {
        // Tracking is now handled by the cache provider through enriched tags passed to SetAsync.
        // CacheableQuery builds tracking tags (entity types + user tags + context) and passes them
        // to the provider's SetAsync via CachingOptions.Tags. This ensures tracking data is stored
        // in the distributed cache, not in-memory, so it works across multiple app instances.
    }

    /// <summary>
    ///     No-op. Tracking is now handled by the cache provider through enriched tags in SetAsync.
    ///     Kept for backward compatibility.
    /// </summary>
    public void RegisterCacheEntry(string cacheKey, IEnumerable<string> tags, string? contextKey = null)
    {
        // See comment in the entity types overload above.
    }

    /// <summary>
    ///     Invalidates cache entries by user-supplied keys.
    ///     Automatically expands each key to include :count and :any suffix variants,
    ///     and tries both context-prefixed and global (unprefixed) versions to handle
    ///     entries cached with or without IgnoreContext().
    /// </summary>
    public async Task InvalidateByKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var contextKey = GetCurrentContextKey();
        var prefix = CacheServiceAccessor.CachePrefix;
        var keySet = new HashSet<string>();

        foreach (var key in keys)
        {
            // Global key: {prefix}:{key}
            keySet.Add($"{prefix}:{key}");
            foreach (var suffix in CacheKeySuffixes.All)
            {
                keySet.Add($"{prefix}:{key}{suffix}");
            }

            // Context-scoped key: {prefix}:{context}:{key}
            if (!string.IsNullOrEmpty(contextKey))
            {
                keySet.Add($"{prefix}:{contextKey}:{key}");
                foreach (var suffix in CacheKeySuffixes.All)
                {
                    keySet.Add($"{prefix}:{contextKey}:{key}{suffix}");
                }
            }
        }

        await InvalidateKeysAsync(keySet, cancellationToken);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Clearing all cache entries");

        var providers = GetAllProviders();
        foreach (var provider in providers)
        {
            try
            {
                await provider.ClearAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear cache provider: {ProviderType}", provider.GetType().Name);
            }
        }

        _logger.LogInformation("Cleared all cache entries across {ProviderCount} providers", providers.Count);
    }

    internal string? GetCurrentContextKey()
    {
        if (_scopeFactory is null)
        {
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var contextProvider = scope.ServiceProvider.GetService<ICacheContextProvider>();
        return contextProvider?.GetContextKey();
    }

    private async Task InvalidateByProviderTagsAsync(IReadOnlyList<string> tags, CancellationToken cancellationToken)
    {
        var providers = GetAllProviders();
        foreach (var provider in providers)
        {
            try
            {
                await provider.InvalidateByTagsAsync(tags, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate cache by tags on provider: {ProviderType}",
                    provider.GetType().Name);
            }
        }
    }

    private async Task InvalidateKeysAsync(HashSet<string> keys, CancellationToken cancellationToken)
    {
        var providers = GetAllProviders();

        foreach (var key in keys)
        {
            foreach (var provider in providers)
            {
                try
                {
                    await provider.RemoveAsync(key, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to invalidate cache key: {CacheKey}", key);
                }
            }

            _logger.LogDebug("Invalidated cache key: {CacheKey}", key);
        }

        if (keys.Count > 0)
        {
            _logger.LogInformation("Invalidated {Count} cache entries across {ProviderCount} providers",
                keys.Count, providers.Count);
        }
    }

    private IReadOnlyList<ICacheProvider> GetAllProviders()
    {
        if (_providerFactory is not null)
        {
            return _providerFactory.GetAllProviders().ToList();
        }

        return [_defaultProvider];
    }
}
