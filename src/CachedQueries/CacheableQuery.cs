using System.Linq.Expressions;
using CachedQueries.Abstractions;
using CachedQueries.Internal;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries;

/// <summary>
/// A query wrapper that enables caching. Created by calling
/// <see cref="Extensions.CacheableExtensions.Cacheable{T}(IQueryable{T})"/> on an IQueryable.
/// Call terminal methods like <see cref="ToListAsync"/>, <see cref="FirstOrDefaultAsync(CancellationToken)"/>,
/// <see cref="CountAsync"/>, or <see cref="AnyAsync(CancellationToken)"/> to execute with caching.
/// </summary>
/// <example>
/// var orders = await _context.Orders
///     .Where(o => o.IsActive)
///     .Cacheable()
///     .ToListAsync();
/// </example>
public sealed class CacheableQuery<T> where T : class
{
    private readonly IQueryable<T> _source;
    private readonly CachingOptions _options;

    internal CacheableQuery(IQueryable<T> source, CachingOptions options)
    {
        _source = source;
        _options = options;
    }

    /// <summary>
    /// Executes the query and caches the result as a list.
    /// </summary>
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_options.SkipCache || !CacheServiceAccessor.IsConfigured)
            return await _source.ToListAsync(cancellationToken);

        var (cacheProvider, keyGenerator, invalidator, contextKey) = ResolveServices();
        var provider = GetProvider(ResolveTarget(CacheTarget.Collection), cacheProvider);

        var cacheKey = BuildCacheKey(keyGenerator, contextKey);
        var cached = await provider.GetAsync<List<T>>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var result = await _source.ToListAsync(cancellationToken);
        await provider.SetAsync(cacheKey, result, _options, cancellationToken);
        RegisterEntry(invalidator, cacheKey, contextKey);

        return result;
    }

    /// <summary>
    /// Executes the query and caches the first result or default.
    /// </summary>
    public Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
        => FirstOrDefaultAsync(null, cancellationToken);

    /// <summary>
    /// Executes the query with predicate and caches the first result or default.
    /// </summary>
    public async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>>? predicate,
        CancellationToken cancellationToken = default)
    {
        if (_options.SkipCache || !CacheServiceAccessor.IsConfigured)
            return predicate is null
                ? await _source.FirstOrDefaultAsync(cancellationToken)
                : await _source.FirstOrDefaultAsync(predicate, cancellationToken);

        var (cacheProvider, keyGenerator, invalidator, contextKey) = ResolveServices();
        var provider = GetProvider(ResolveTarget(CacheTarget.Single), cacheProvider);

        var baseCacheKey = _options.CacheKey ?? keyGenerator.GenerateKey(_source, predicate);
        var cacheKey = ApplyContextPrefix(baseCacheKey, contextKey);
        var cached = await provider.GetAsync<T>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var result = predicate is null
            ? await _source.FirstOrDefaultAsync(cancellationToken)
            : await _source.FirstOrDefaultAsync(predicate, cancellationToken);

        if (result is null) return result;

        await provider.SetAsync(cacheKey, result, _options, cancellationToken);
        RegisterEntry(invalidator, cacheKey, contextKey);

        return result;
    }

    /// <summary>
    /// Executes the query and caches the single result or default.
    /// </summary>
    public Task<T?> SingleOrDefaultAsync(CancellationToken cancellationToken = default)
        => SingleOrDefaultAsync(null, cancellationToken);

    /// <summary>
    /// Executes the query with predicate and caches the single result or default.
    /// </summary>
    public async Task<T?> SingleOrDefaultAsync(
        Expression<Func<T, bool>>? predicate,
        CancellationToken cancellationToken = default)
    {
        if (_options.SkipCache || !CacheServiceAccessor.IsConfigured)
            return predicate is null
                ? await _source.SingleOrDefaultAsync(cancellationToken)
                : await _source.SingleOrDefaultAsync(predicate, cancellationToken);

        var (cacheProvider, keyGenerator, invalidator, contextKey) = ResolveServices();
        var provider = GetProvider(ResolveTarget(CacheTarget.Single), cacheProvider);

        var baseCacheKey = _options.CacheKey ?? keyGenerator.GenerateKey(_source, predicate);
        var cacheKey = ApplyContextPrefix(baseCacheKey, contextKey);
        var cached = await provider.GetAsync<T>(cacheKey, cancellationToken);
        if (cached is not null) return cached;

        var result = predicate is null
            ? await _source.SingleOrDefaultAsync(cancellationToken)
            : await _source.SingleOrDefaultAsync(predicate, cancellationToken);

        if (result is not null)
        {
            await provider.SetAsync(cacheKey, result, _options, cancellationToken);
            RegisterEntry(invalidator, cacheKey, contextKey);
        }

        return result;
    }

    /// <summary>
    /// Executes the query and caches the count result.
    /// </summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_options.SkipCache || !CacheServiceAccessor.IsConfigured)
            return await _source.CountAsync(cancellationToken);

        var (cacheProvider, keyGenerator, invalidator, contextKey) = ResolveServices();
        var provider = GetProvider(ResolveTarget(CacheTarget.Scalar), cacheProvider);

        var baseCacheKey = (_options.CacheKey ?? keyGenerator.GenerateKey(_source)) + ":count";
        var cacheKey = ApplyContextPrefix(baseCacheKey, contextKey);
        var cached = await provider.GetAsync<int?>(cacheKey, cancellationToken);
        if (cached.HasValue) return cached.Value;

        var result = await _source.CountAsync(cancellationToken);
        await provider.SetAsync(cacheKey, result, _options, cancellationToken);
        RegisterEntry(invalidator, cacheKey, contextKey);

        return result;
    }

    /// <summary>
    /// Executes the query and caches whether any results exist.
    /// </summary>
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => AnyAsync(null, cancellationToken);

    /// <summary>
    /// Executes the query with predicate and caches whether any results exist.
    /// </summary>
    public async Task<bool> AnyAsync(
        Expression<Func<T, bool>>? predicate,
        CancellationToken cancellationToken = default)
    {
        if (_options.SkipCache || !CacheServiceAccessor.IsConfigured)
            return predicate is null
                ? await _source.AnyAsync(cancellationToken)
                : await _source.AnyAsync(predicate, cancellationToken);

        var (cacheProvider, keyGenerator, invalidator, contextKey) = ResolveServices();
        var provider = GetProvider(ResolveTarget(CacheTarget.Scalar), cacheProvider);

        var baseCacheKey = (_options.CacheKey ?? keyGenerator.GenerateKey(_source, predicate)) + ":any";
        var cacheKey = ApplyContextPrefix(baseCacheKey, contextKey);
        var cached = await provider.GetAsync<bool?>(cacheKey, cancellationToken);
        if (cached.HasValue) return cached.Value;

        var result = predicate is null
            ? await _source.AnyAsync(cancellationToken)
            : await _source.AnyAsync(predicate, cancellationToken);

        await provider.SetAsync(cacheKey, result, _options, cancellationToken);
        RegisterEntry(invalidator, cacheKey, contextKey);

        return result;
    }

    private string BuildCacheKey(ICacheKeyGenerator keyGenerator, string? contextKey)
    {
        var baseCacheKey = _options.CacheKey ?? keyGenerator.GenerateKey(_source);
        return ApplyContextPrefix(baseCacheKey, contextKey);
    }

    private static string ApplyContextPrefix(string cacheKey, string? contextKey)
        => string.IsNullOrEmpty(contextKey) ? cacheKey : $"{contextKey}:{cacheKey}";

    private CacheTarget ResolveTarget(CacheTarget autoTarget)
        => _options.Target == CacheTarget.Auto ? autoTarget : _options.Target;

    private static ICacheProvider GetProvider(CacheTarget target, ICacheProvider defaultProvider)
    {
        var factory = CacheServiceAccessor.ProviderFactory;
        return factory?.GetProvider(target) ?? defaultProvider;
    }

    private (ICacheProvider, ICacheKeyGenerator, ICacheInvalidator, string?) ResolveServices()
    {
        return (
            CacheServiceAccessor.CacheProvider!,
            CacheServiceAccessor.KeyGenerator!,
            CacheServiceAccessor.Invalidator!,
            _options.IgnoreContext ? null : CacheServiceAccessor.GetContextKey()
        );
    }

    private void RegisterEntry(ICacheInvalidator invalidator, string cacheKey, string? contextKey)
    {
        var entityTypes = EntityTypeExtractor.ExtractEntityTypes(_source);
        invalidator.RegisterCacheEntry(cacheKey, entityTypes, contextKey);

        if (_options.Tags.Count > 0)
            invalidator.RegisterCacheEntry(cacheKey, _options.Tags, contextKey);
    }
}
