namespace CachedQueries.Abstractions;

/// <summary>
/// Factory for resolving cache providers based on cache target type.
/// Allows different storage backends for different types of cached data.
/// </summary>
public interface ICacheProviderFactory
{
    /// <summary>
    /// Gets the appropriate cache provider for the specified target.
    /// </summary>
    /// <param name="target">The cache target type.</param>
    /// <returns>The cache provider to use.</returns>
    ICacheProvider GetProvider(CacheTarget target);

    /// <summary>
    /// Gets all registered cache providers.
    /// Used for invalidation across all providers.
    /// </summary>
    IEnumerable<ICacheProvider> GetAllProviders();
}
