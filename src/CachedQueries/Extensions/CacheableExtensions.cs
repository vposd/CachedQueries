namespace CachedQueries.Extensions;

/// <summary>
///     Extension methods for enabling query caching via the fluent <see cref="CacheableQuery{T}" /> API.
/// </summary>
public static class CacheableExtensions
{
    /// <summary>
    ///     Enables caching for the query with default options (30 min absolute expiration).
    ///     Call a terminal method like <c>ToListAsync()</c> to execute.
    /// </summary>
    /// <example>
    ///     var orders = await _context.Orders
    ///     .Where(o => o.IsActive)
    ///     .Cacheable()
    ///     .ToListAsync();
    /// </example>
    public static CacheableQuery<T> Cacheable<T>(this IQueryable<T> query) where T : class
    {
        return new CacheableQuery<T>(query, CachingOptions.Default);
    }

    /// <summary>
    ///     Enables caching for the query with a fluent options builder.
    /// </summary>
    /// <example>
    ///     var orders = await _context.Orders
    ///     .Cacheable(o => o
    ///     .Expire(TimeSpan.FromMinutes(5))
    ///     .WithTags("orders", "reports"))
    ///     .ToListAsync();
    /// </example>
    public static CacheableQuery<T> Cacheable<T>(
        this IQueryable<T> query,
        Action<CacheOptionsBuilder> configure) where T : class
    {
        var builder = new CacheOptionsBuilder();
        configure(builder);
        return new CacheableQuery<T>(query, builder.Build());
    }

    /// <summary>
    ///     Enables caching for the query with explicit options.
    /// </summary>
    /// <example>
    ///     var options = new CachingOptions(TimeSpan.FromHours(1));
    ///     var orders = await _context.Orders
    ///     .Cacheable(options)
    ///     .ToListAsync();
    /// </example>
    public static CacheableQuery<T> Cacheable<T>(
        this IQueryable<T> query,
        CachingOptions options) where T : class
    {
        return new CacheableQuery<T>(query, options);
    }
}
