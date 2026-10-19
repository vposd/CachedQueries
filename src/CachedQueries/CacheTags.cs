namespace CachedQueries;

/// <summary>
///     Tag names used by CachedQueries for automatic, entity-based invalidation.
/// </summary>
/// <remarks>
///     Every cached query is tracked under one tag per entity type it depends on. That tag has the same name as
///     a user tag equal to <see cref="ForEntity{TEntity}" />, so it can be combined with explicit tags or used for
///     manual invalidation:
///     <code>
///     // keep SaveChanges-driven invalidation for Order while also tagging the entry as "reports"
///     .Cacheable(o => o.WithTags("reports", CacheTags.ForEntity&lt;Order&gt;()))
///
///     // equivalent to Cache.InvalidateAsync&lt;Order&gt;()
///     await Cache.InvalidateByTagAsync(CacheTags.ForEntity&lt;Order&gt;());
///     </code>
///     Context scoping (e.g. per tenant) is applied by the library on top of this name; do not add the
///     context key yourself.
/// </remarks>
public static class CacheTags
{
    /// <summary>
    ///     Returns the tag under which queries depending on <typeparamref name="TEntity" /> are tracked.
    /// </summary>
    public static string ForEntity<TEntity>()
    {
        return ForEntity(typeof(TEntity));
    }

    /// <summary>
    ///     Returns the tag under which queries depending on <paramref name="entityType" /> are tracked.
    /// </summary>
    public static string ForEntity(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return entityType.FullName ?? entityType.Name;
    }
}
