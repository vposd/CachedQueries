namespace CachedQueries;

/// <summary>
///     Specifies the type of cache target to determine storage strategy.
/// </summary>
public enum CacheTarget
{
    /// <summary>
    ///     Automatically determined based on query type (default).
    /// </summary>
    Auto,

    /// <summary>
    ///     Single item (FirstOrDefault, SingleOrDefault).
    /// </summary>
    Single,

    /// <summary>
    ///     Collection of items (ToList).
    /// </summary>
    Collection,

    /// <summary>
    ///     Scalar value (Count, Any).
    /// </summary>
    Scalar
}
