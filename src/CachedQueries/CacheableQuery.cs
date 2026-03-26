using System.Collections.Concurrent;
using System.Linq.Expressions;
using CachedQueries.Abstractions;
using CachedQueries.Internal;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries;

/// <summary>
///     A query wrapper that enables caching. Created by calling
///     <see cref="Extensions.CacheableExtensions.Cacheable{T}(IQueryable{T})" /> on an IQueryable.
///     Call terminal methods like <see cref="ToListAsync" />, <see cref="FirstOrDefaultAsync(CancellationToken)" />,
///     <see cref="CountAsync" />, or <see cref="AnyAsync(CancellationToken)" /> to execute with caching.
/// </summary>
/// <example>
///     var orders = await _context.Orders
///     .Where(o => o.IsActive)
///     .Cacheable()
///     .ToListAsync();
/// </example>
public sealed class CacheableQuery<T> where T : class
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks = new();

    private readonly CachingOptions _options;

    internal CacheableQuery(IQueryable<T> source, CachingOptions options)
    {
        Query = source;
        _options = options;
    }

    /// <summary>
    ///     Gets the underlying IQueryable, useful for operations not covered by the cached terminal methods.
    /// </summary>
    public IQueryable<T> Query { get; }

    /// <summary>
    ///     Executes the query and caches the result as a list.
    /// </summary>
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_options.SkipCache || !CacheServiceAccessor.IsConfigured)
        {
            return await Query.ToListAsync(cancellationToken);
        }

        var (cacheProvider, keyGenerator, contextKey) = ResolveServices();
        var provider = GetProvider(ResolveTarget(CacheTarget.Collection), cacheProvider);
        var cacheKey = BuildCacheKey(keyGenerator, contextKey);

        return await GetOrLoadAsync(
            cacheKey,
            provider,
            () => Query.ToListAsync(cancellationToken),
            BuildOptionsWithTracking(contextKey),
            cancellationToken);
    }

    /// <summary>
    ///     Executes the query and caches the first result or default.
    /// </summary>
    public Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return FirstOrDefaultAsync(null, cancellationToken);
    }

    /// <summary>
    ///     Executes the query with predicate and caches the first result or default.
    /// </summary>
    public async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>>? predicate,
        CancellationToken cancellationToken = default)
    {
        if (_options.SkipCache || !CacheServiceAccessor.IsConfigured)
        {
            return predicate is null
                ? await Query.FirstOrDefaultAsync(cancellationToken)
                : await Query.FirstOrDefaultAsync(predicate, cancellationToken);
        }

        var (cacheProvider, keyGenerator, contextKey) = ResolveServices();
        var provider = GetProvider(ResolveTarget(CacheTarget.Single), cacheProvider);

        var baseCacheKey = _options.CacheKey ?? keyGenerator.GenerateKey(Query, predicate);
        var cacheKey = ComposeKey(baseCacheKey, contextKey);
        var cached = await provider.GetAsync<T>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var result = predicate is null
            ? await Query.FirstOrDefaultAsync(cancellationToken)
            : await Query.FirstOrDefaultAsync(predicate, cancellationToken);

        if (result is null)
        {
            return result;
        }

        await provider.SetAsync(cacheKey, result, BuildOptionsWithTracking(contextKey), cancellationToken);

        return result;
    }

    /// <summary>
    ///     Executes the query and caches the single result or default.
    /// </summary>
    public Task<T?> SingleOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return SingleOrDefaultAsync(null, cancellationToken);
    }

    /// <summary>
    ///     Executes the query with predicate and caches the single result or default.
    /// </summary>
    public async Task<T?> SingleOrDefaultAsync(
        Expression<Func<T, bool>>? predicate,
        CancellationToken cancellationToken = default)
    {
        if (_options.SkipCache || !CacheServiceAccessor.IsConfigured)
        {
            return predicate is null
                ? await Query.SingleOrDefaultAsync(cancellationToken)
                : await Query.SingleOrDefaultAsync(predicate, cancellationToken);
        }

        var (cacheProvider, keyGenerator, contextKey) = ResolveServices();
        var provider = GetProvider(ResolveTarget(CacheTarget.Single), cacheProvider);

        var baseCacheKey = _options.CacheKey ?? keyGenerator.GenerateKey(Query, predicate);
        var cacheKey = ComposeKey(baseCacheKey, contextKey);
        var cached = await provider.GetAsync<T>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var result = predicate is null
            ? await Query.SingleOrDefaultAsync(cancellationToken)
            : await Query.SingleOrDefaultAsync(predicate, cancellationToken);

        if (result is not null)
        {
            await provider.SetAsync(cacheKey, result, BuildOptionsWithTracking(contextKey), cancellationToken);
        }

        return result;
    }

    /// <summary>
    ///     Executes the query and caches the count result.
    /// </summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_options.SkipCache || !CacheServiceAccessor.IsConfigured)
        {
            return await Query.CountAsync(cancellationToken);
        }

        var (cacheProvider, keyGenerator, contextKey) = ResolveServices();
        var provider = GetProvider(ResolveTarget(CacheTarget.Scalar), cacheProvider);

        var baseCacheKey = (_options.CacheKey ?? keyGenerator.GenerateKey(Query)) + CacheKeySuffixes.Count;
        var cacheKey = ComposeKey(baseCacheKey, contextKey);

        return await GetOrLoadScalarAsync(
            cacheKey,
            provider,
            () => Query.CountAsync(cancellationToken),
            BuildOptionsWithTracking(contextKey),
            cancellationToken);
    }

    /// <summary>
    ///     Executes the query and caches whether any results exist.
    /// </summary>
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return AnyAsync(null, cancellationToken);
    }

    /// <summary>
    ///     Executes the query with predicate and caches whether any results exist.
    /// </summary>
    public async Task<bool> AnyAsync(
        Expression<Func<T, bool>>? predicate,
        CancellationToken cancellationToken = default)
    {
        if (_options.SkipCache || !CacheServiceAccessor.IsConfigured)
        {
            return predicate is null
                ? await Query.AnyAsync(cancellationToken)
                : await Query.AnyAsync(predicate, cancellationToken);
        }

        var (cacheProvider, keyGenerator, contextKey) = ResolveServices();
        var provider = GetProvider(ResolveTarget(CacheTarget.Scalar), cacheProvider);

        var baseCacheKey = (_options.CacheKey ?? keyGenerator.GenerateKey(Query, predicate)) + CacheKeySuffixes.Any;
        var cacheKey = ComposeKey(baseCacheKey, contextKey);

        return await GetOrLoadScalarAsync(
            cacheKey,
            provider,
            async () => predicate is null
                ? await Query.AnyAsync(cancellationToken)
                : await Query.AnyAsync(predicate, cancellationToken),
            BuildOptionsWithTracking(contextKey),
            cancellationToken);
    }

    /// <summary>
    ///     Gets a value from cache or loads it from the database with stampede protection.
    ///     Only one concurrent caller per cache key executes the query; others wait for the result.
    /// </summary>
    private static async Task<TResult> GetOrLoadAsync<TResult>(
        string cacheKey,
        ICacheProvider provider,
        Func<Task<TResult>> loadFromDb,
        CachingOptions options,
        CancellationToken cancellationToken) where TResult : class
    {
        // Fast path: check cache without lock
        var cached = await provider.GetAsync<TResult>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        // Slow path: acquire per-key semaphore to prevent thundering herd
        var semaphore = KeyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring lock
            cached = await provider.GetAsync<TResult>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return cached;
            }

            var result = await loadFromDb();
            await provider.SetAsync(cacheKey, result, options, cancellationToken);
            return result;
        }
        finally
        {
            semaphore.Release();
            // Clean up if no other waiters are pending (only removes if same instance)
            KeyLocks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(cacheKey, semaphore));
        }
    }

    /// <summary>
    ///     Gets a scalar value from cache or loads it with stampede protection.
    ///     Uses nullable wrapper to distinguish cache miss from cached value.
    /// </summary>
    private static async Task<TResult> GetOrLoadScalarAsync<TResult>(
        string cacheKey,
        ICacheProvider provider,
        Func<Task<TResult>> loadFromDb,
        CachingOptions options,
        CancellationToken cancellationToken) where TResult : struct
    {
        // Fast path: check cache without lock
        var cached = await provider.GetAsync<TResult?>(cacheKey, cancellationToken);
        if (cached.HasValue)
        {
            return cached.Value;
        }

        // Slow path: acquire per-key semaphore to prevent thundering herd
        var semaphore = KeyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring lock
            cached = await provider.GetAsync<TResult?>(cacheKey, cancellationToken);
            if (cached.HasValue)
            {
                return cached.Value;
            }

            var result = await loadFromDb();
            await provider.SetAsync(cacheKey, result, options, cancellationToken);
            return result;
        }
        finally
        {
            semaphore.Release();
            KeyLocks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(cacheKey, semaphore));
        }
    }

    private string BuildCacheKey(ICacheKeyGenerator keyGenerator, string? contextKey)
    {
        var hash = _options.CacheKey ?? keyGenerator.GenerateKey(Query);
        return ComposeKey(hash, contextKey);
    }

    /// <summary>
    ///     Composes the full cache key: {CachePrefix}:{contextKey}:{hash} or {CachePrefix}:{hash}.
    /// </summary>
    private static string ComposeKey(string hash, string? contextKey)
    {
        var prefix = CacheServiceAccessor.CachePrefix;
        return string.IsNullOrEmpty(contextKey)
            ? $"{prefix}:{hash}"
            : $"{prefix}:{contextKey}:{hash}";
    }

    private CacheTarget ResolveTarget(CacheTarget autoTarget)
    {
        return _options.Target == CacheTarget.Auto ? autoTarget : _options.Target;
    }

    private static ICacheProvider GetProvider(CacheTarget target, ICacheProvider defaultProvider)
    {
        var factory = CacheServiceAccessor.ProviderFactory;
        return factory?.GetProvider(target) ?? defaultProvider;
    }

    private (ICacheProvider, ICacheKeyGenerator, string?) ResolveServices()
    {
        return (
            CacheServiceAccessor.CacheProvider!,
            CacheServiceAccessor.KeyGenerator!,
            _options.IgnoreContext ? null : CacheServiceAccessor.GetContextKey()
        );
    }

    private CachingOptions BuildOptionsWithTracking(string? contextKey)
    {
        // When explicit key is set, caller manages cache manually — skip auto entity type tags.
        IEnumerable<Type> entityTypes = _options.CacheKey is not null
            ? Array.Empty<Type>()
            : EntityTypeExtractor.ExtractEntityTypes(Query);
        var trackingTags = TrackingTags.BuildTrackingTags(entityTypes, _options.Tags, contextKey);
        return _options.WithTrackingTags(trackingTags);
    }
}
