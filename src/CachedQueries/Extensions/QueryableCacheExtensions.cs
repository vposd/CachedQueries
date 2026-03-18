using System.Linq.Expressions;

namespace CachedQueries.Extensions;

/// <summary>
/// Extension methods for caching IQueryable results.
/// These are convenience wrappers around the <see cref="CacheableQuery{T}"/> API.
/// Prefer using <c>.Cacheable().ToListAsync()</c> for new code.
/// </summary>
public static class QueryableCacheExtensions
{
    /// <summary>
    /// Executes the query and caches the result as a list.
    /// </summary>
    public static Task<List<T>> ToListCachedAsync<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable().ToListAsync(cancellationToken);

    /// <summary>
    /// Executes the query and caches the result as a list with custom options.
    /// </summary>
    public static Task<List<T>> ToListCachedAsync<T>(
        this IQueryable<T> query,
        CachingOptions options,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable(options).ToListAsync(cancellationToken);

    /// <summary>
    /// Executes the query and caches the first result or default.
    /// </summary>
    public static Task<T?> FirstOrDefaultCachedAsync<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable().FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Executes the query with predicate and caches the first result or default.
    /// </summary>
    public static Task<T?> FirstOrDefaultCachedAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable().FirstOrDefaultAsync(predicate, cancellationToken);

    /// <summary>
    /// Executes the query with predicate and caches the first result or default with custom options.
    /// </summary>
    public static Task<T?> FirstOrDefaultCachedAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>>? predicate,
        CachingOptions options,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable(options).FirstOrDefaultAsync(predicate, cancellationToken);

    /// <summary>
    /// Executes the query and caches the single result or default.
    /// </summary>
    public static Task<T?> SingleOrDefaultCachedAsync<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable().SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Executes the query with predicate and caches the single result or default.
    /// </summary>
    public static Task<T?> SingleOrDefaultCachedAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable().SingleOrDefaultAsync(predicate, cancellationToken);

    /// <summary>
    /// Executes the query with predicate and caches the single result or default with custom options.
    /// </summary>
    public static Task<T?> SingleOrDefaultCachedAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>>? predicate,
        CachingOptions options,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable(options).SingleOrDefaultAsync(predicate, cancellationToken);

    /// <summary>
    /// Executes the query and caches the count result.
    /// </summary>
    public static Task<int> CountCachedAsync<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable().CountAsync(cancellationToken);

    /// <summary>
    /// Executes the query and caches the count result with custom options.
    /// </summary>
    public static Task<int> CountCachedAsync<T>(
        this IQueryable<T> query,
        CachingOptions options,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable(options).CountAsync(cancellationToken);

    /// <summary>
    /// Executes the query and caches whether any results exist.
    /// </summary>
    public static Task<bool> AnyCachedAsync<T>(
        this IQueryable<T> query,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable().AnyAsync(cancellationToken);

    /// <summary>
    /// Executes the query with predicate and caches whether any results exist.
    /// </summary>
    public static Task<bool> AnyCachedAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable().AnyAsync(predicate, cancellationToken);

    /// <summary>
    /// Executes the query with predicate and caches whether any results exist with custom options.
    /// </summary>
    public static Task<bool> AnyCachedAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>>? predicate,
        CachingOptions options,
        CancellationToken cancellationToken = default)
        where T : class
        => query.Cacheable(options).AnyAsync(predicate, cancellationToken);
}
