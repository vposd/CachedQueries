using System.Collections.Concurrent;
using CachedQueries.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Internal;

/// <summary>
/// Default implementation of cache invalidator.
/// Tracks cache entries with context awareness for multi-tenant invalidation.
/// </summary>
/// <remarks>
/// Invalidation rule: when an entity type is invalidated, entries matching
/// the current context AND global entries (no context) are removed.
/// Entries belonging to other contexts are left untouched.
/// </remarks>
internal sealed class CacheInvalidator : ICacheInvalidator
{
    private readonly ICacheProvider _defaultProvider;
    private readonly ICacheProviderFactory? _providerFactory;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<CacheInvalidator> _logger;

    // Maps entity type → { cacheKey → contextKey? }
    // contextKey is null for global (context-independent) entries
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, string?>> _entityTypeToKeys = new();

    // Maps tag → { cacheKey → contextKey? }
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string?>> _tagToKeys = new();

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
        IServiceProvider serviceProvider,
        ILogger<CacheInvalidator> logger)
    {
        _defaultProvider = cacheProvider;
        _providerFactory = providerFactory;
        _scopeFactory = serviceProvider.GetService<IServiceScopeFactory>();
        _logger = logger;
    }

    /// <summary>
    /// Invalidates cache entries for entity types.
    /// Removes entries matching the current context + global entries (contextKey == null).
    /// </summary>
    public async Task InvalidateAsync(IEnumerable<Type> entityTypes, CancellationToken cancellationToken = default)
    {
        var currentContext = GetCurrentContextKey();
        var keysToInvalidate = new HashSet<string>();

        foreach (var entityType in entityTypes)
        {
            if (!_entityTypeToKeys.TryGetValue(entityType, out var entries))
                continue;

            var toRemove = new List<string>();
            foreach (var (cacheKey, ctxKey) in entries)
            {
                // Invalidate global entries (ctxKey == null) and current context entries
                if (ctxKey is null || ctxKey == currentContext)
                {
                    keysToInvalidate.Add(cacheKey);
                    toRemove.Add(cacheKey);
                }
            }

            foreach (var key in toRemove)
                entries.TryRemove(key, out _);

            if (entries.IsEmpty)
                _entityTypeToKeys.TryRemove(entityType, out _);
        }

        await InvalidateKeysAsync(keysToInvalidate, cancellationToken);
    }

    /// <summary>
    /// Invalidates cache entries for tags.
    /// Removes entries matching the current context + global entries (contextKey == null).
    /// </summary>
    public async Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var currentContext = GetCurrentContextKey();
        var keysToInvalidate = new HashSet<string>();
        var tagsList = tags.ToList();

        foreach (var tag in tagsList)
        {
            if (!_tagToKeys.TryGetValue(tag, out var entries))
                continue;

            var toRemove = new List<string>();
            foreach (var (cacheKey, ctxKey) in entries)
            {
                if (ctxKey is null || ctxKey == currentContext)
                {
                    keysToInvalidate.Add(cacheKey);
                    toRemove.Add(cacheKey);
                }
            }

            foreach (var key in toRemove)
                entries.TryRemove(key, out _);

            if (entries.IsEmpty)
                _tagToKeys.TryRemove(tag, out _);
        }

        await InvalidateKeysAsync(keysToInvalidate, cancellationToken);

        // Also delegate to all cache providers for distributed tag invalidation
        var providers = GetAllProviders();
        foreach (var provider in providers)
        {
            await provider.InvalidateByTagsAsync(tagsList, cancellationToken);
        }
    }

    public void RegisterCacheEntry(string cacheKey, IEnumerable<Type> entityTypes, string? contextKey = null)
    {
        foreach (var entityType in entityTypes)
        {
            var entries = _entityTypeToKeys.GetOrAdd(entityType, _ => new());
            entries[cacheKey] = contextKey;
        }
    }

    public void RegisterCacheEntry(string cacheKey, IEnumerable<string> tags, string? contextKey = null)
    {
        foreach (var tag in tags)
        {
            var entries = _tagToKeys.GetOrAdd(tag, _ => new());
            entries[cacheKey] = contextKey;
        }
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Clearing all cache entries");

        _entityTypeToKeys.Clear();
        _tagToKeys.Clear();

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

    public async Task ClearContextAsync(CancellationToken cancellationToken = default)
    {
        var contextKey = GetCurrentContextKey();

        if (string.IsNullOrEmpty(contextKey))
        {
            _logger.LogWarning("No cache context available. Use ClearAllAsync to clear all entries.");
            return;
        }

        _logger.LogInformation("Clearing cache for context: {ContextKey}", contextKey);

        var keysToInvalidate = CollectKeysForContext(contextKey);
        await InvalidateKeysAsync(keysToInvalidate, cancellationToken);
    }

    private HashSet<string> CollectKeysForContext(string contextKey)
    {
        var keys = new HashSet<string>();

        foreach (var (_, entries) in _entityTypeToKeys)
        {
            var toRemove = entries
                .Where(e => e.Value == contextKey)
                .Select(e => e.Key)
                .ToList();

            foreach (var k in toRemove)
            {
                keys.Add(k);
                entries.TryRemove(k, out _);
            }
        }

        foreach (var (_, entries) in _tagToKeys)
        {
            var toRemove = entries
                .Where(e => e.Value == contextKey)
                .Select(e => e.Key)
                .ToList();

            foreach (var k in toRemove)
            {
                keys.Add(k);
                entries.TryRemove(k, out _);
            }
        }

        return keys;
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

    private IReadOnlyList<ICacheProvider> GetAllProviders()
    {
        if (_providerFactory is not null)
            return _providerFactory.GetAllProviders().ToList();

        return [_defaultProvider];
    }
}
