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
/// <remarks>
///     <para>
///         Results served through the cache are loaded with <c>AsNoTracking()</c> (unless the query already uses
///         <c>AsNoTrackingWithIdentityResolution()</c>) because the same instances may be handed to many callers.
///         Treat them as read-only; to modify an entity, load it without <c>.Cacheable()</c>.
///     </para>
///     <para>
///         Concurrent cache misses for the same key are coalesced: only one caller per key queries the
///         database, the others wait for and reuse its result. <c>null</c> results of
///         <c>FirstOrDefaultAsync</c>/<c>SingleOrDefaultAsync</c> are not cached.
///     </para>
///     <para>
///         When CachedQueries is not configured (<c>UseCachedQueries()</c> was not called) or
///         <see cref="CacheOptionsBuilder.SkipIf" /> is true, the query executes normally, with tracking.
///     </para>
/// </remarks>
/// <example>
///     var orders = await _context.Orders
///     .Where(o => o.IsActive)
///     .Cacheable()
///     .ToListAsync();
/// </example>
public sealed class CacheableQuery<T> where T : class
{
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

    private bool BypassCache => _options.SkipCache || !CacheServiceAccessor.IsConfigured;

    /// <summary>
    ///     The query actually executed on a cache miss: cached instances are shared between callers,
    ///     so change tracking is disabled unless the caller chose identity resolution explicitly.
    /// </summary>
    private IQueryable<T> CachedExecutionQuery =>
        UsesIdentityResolution(Query.Expression) ? Query : Query.AsNoTracking();

    /// <summary>
    ///     Executes the query and caches the result as a list.
    /// </summary>
    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (BypassCache)
        {
            return await Query.ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        var call = Prepare(CacheTarget.Collection, generator => generator.GenerateKey(Query));
        var query = CachedExecutionQuery;

        return (await GetOrLoadAsync(call, async () => await query.ToListAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false))!;
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
    ///     A <c>null</c> result is not cached.
    /// </summary>
    public async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>>? predicate,
        CancellationToken cancellationToken = default)
    {
        if (BypassCache)
        {
            return await FirstOrDefault(Query, predicate, cancellationToken).ConfigureAwait(false);
        }

        var call = Prepare(CacheTarget.Single, generator => generator.GenerateKey(Query, predicate));
        var query = CachedExecutionQuery;

        return await GetOrLoadAsync(call, () => FirstOrDefault(query, predicate, cancellationToken),
            cancellationToken).ConfigureAwait(false);
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
    ///     A <c>null</c> result is not cached.
    /// </summary>
    public async Task<T?> SingleOrDefaultAsync(
        Expression<Func<T, bool>>? predicate,
        CancellationToken cancellationToken = default)
    {
        if (BypassCache)
        {
            return await SingleOrDefault(Query, predicate, cancellationToken).ConfigureAwait(false);
        }

        var call = Prepare(CacheTarget.Single, generator => generator.GenerateKey(Query, predicate));
        var query = CachedExecutionQuery;

        return await GetOrLoadAsync(call, () => SingleOrDefault(query, predicate, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Executes the query and caches the count result.
    /// </summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        if (BypassCache)
        {
            return await Query.CountAsync(cancellationToken).ConfigureAwait(false);
        }

        var call = Prepare(CacheTarget.Scalar, generator => generator.GenerateKey(Query), CacheKeySuffixes.Count);

        return await GetOrLoadScalarAsync(call, () => Query.CountAsync(cancellationToken), cancellationToken)
            .ConfigureAwait(false);
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
        if (BypassCache)
        {
            return await Any(Query, predicate, cancellationToken).ConfigureAwait(false);
        }

        var call = Prepare(CacheTarget.Scalar, generator => generator.GenerateKey(Query, predicate),
            CacheKeySuffixes.Any);

        return await GetOrLoadScalarAsync(call, () => Any(Query, predicate, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private static Task<T?> FirstOrDefault(IQueryable<T> query, Expression<Func<T, bool>>? predicate,
        CancellationToken cancellationToken)
    {
        return predicate is null
            ? query.FirstOrDefaultAsync(cancellationToken)
            : query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    private static Task<T?> SingleOrDefault(IQueryable<T> query, Expression<Func<T, bool>>? predicate,
        CancellationToken cancellationToken)
    {
        return predicate is null
            ? query.SingleOrDefaultAsync(cancellationToken)
            : query.SingleOrDefaultAsync(predicate, cancellationToken);
    }

    private static Task<bool> Any(IQueryable<T> query, Expression<Func<T, bool>>? predicate,
        CancellationToken cancellationToken)
    {
        return predicate is null
            ? query.AnyAsync(cancellationToken)
            : query.AnyAsync(predicate, cancellationToken);
    }

    /// <summary>
    ///     Gets a value from cache or loads it with stampede protection: only one concurrent caller per
    ///     cache key executes the query; others wait and then read the freshly cached value.
    ///     <c>null</c> results are returned but not cached.
    /// </summary>
    private async Task<TResult?> GetOrLoadAsync<TResult>(
        CacheCall call,
        Func<Task<TResult?>> loadFromDb,
        CancellationToken cancellationToken) where TResult : class
    {
        // Fast path: check cache without lock
        var cached = await call.Provider.GetAsync<TResult>(call.Key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        using (await KeyedLock.AcquireAsync(call.Key, cancellationToken).ConfigureAwait(false))
        {
            // Double-check: another caller may have loaded the value while we were waiting
            cached = await call.Provider.GetAsync<TResult>(call.Key, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }

            var result = await loadFromDb().ConfigureAwait(false);
            if (result is not null)
            {
                await call.Provider.SetAsync(call.Key, result, BuildOptionsWithTracking(call.ContextKey),
                    cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
    }

    /// <summary>
    ///     Scalar variant of <see cref="GetOrLoadAsync{TResult}" />. Uses a nullable wrapper to distinguish a
    ///     cache miss from a cached default value (0 / false).
    /// </summary>
    private async Task<TResult> GetOrLoadScalarAsync<TResult>(
        CacheCall call,
        Func<Task<TResult>> loadFromDb,
        CancellationToken cancellationToken) where TResult : struct
    {
        var cached = await call.Provider.GetAsync<TResult?>(call.Key, cancellationToken).ConfigureAwait(false);
        if (cached.HasValue)
        {
            return cached.Value;
        }

        using (await KeyedLock.AcquireAsync(call.Key, cancellationToken).ConfigureAwait(false))
        {
            cached = await call.Provider.GetAsync<TResult?>(call.Key, cancellationToken).ConfigureAwait(false);
            if (cached.HasValue)
            {
                return cached.Value;
            }

            var result = await loadFromDb().ConfigureAwait(false);
            await call.Provider.SetAsync(call.Key, result, BuildOptionsWithTracking(call.ContextKey),
                cancellationToken).ConfigureAwait(false);
            return result;
        }
    }

    /// <summary>
    ///     Resolves provider, context and the full cache key ({CachePrefix}:{contextKey}:{hash}{suffix}).
    /// </summary>
    private CacheCall Prepare(CacheTarget autoTarget, Func<ICacheKeyGenerator, string> generateKey,
        string suffix = "")
    {
        var contextKey = _options.IgnoreContext ? null : CacheServiceAccessor.GetContextKey();

        var target = _options.Target == CacheTarget.Auto ? autoTarget : _options.Target;
        var provider = CacheServiceAccessor.ProviderFactory?.GetProvider(target) ?? CacheServiceAccessor.CacheProvider!;

        var hash = (_options.CacheKey ?? generateKey(CacheServiceAccessor.KeyGenerator!)) + suffix;
        var prefix = CacheServiceAccessor.CachePrefix;
        var key = string.IsNullOrEmpty(contextKey) ? $"{prefix}:{hash}" : $"{prefix}:{contextKey}:{hash}";

        return new CacheCall(provider, key, contextKey);
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

    /// <summary>
    ///     True when the query chain already contains <c>AsNoTrackingWithIdentityResolution()</c>.
    /// </summary>
    private static bool UsesIdentityResolution(Expression expression)
    {
        while (expression is MethodCallExpression { Arguments.Count: > 0 } call)
        {
            if (call.Method.Name == nameof(EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution))
            {
                return true;
            }

            expression = call.Arguments[0];
        }

        return false;
    }

    private readonly record struct CacheCall(ICacheProvider Provider, string Key, string? ContextKey);
}
