namespace CachedQueries.Abstractions;

/// <summary>
///     Provides cache context for multi-tenant or user-scoped caching.
///     Implement this interface to isolate cache entries by tenant, user, or other scope.
/// </summary>
/// <example>
///     public class TenantCacheContextProvider : ICacheContextProvider
///     {
///     private readonly ITenantAccessor _tenantAccessor;
///     public TenantCacheContextProvider(ITenantAccessor tenantAccessor)
///     {
///     _tenantAccessor = tenantAccessor;
///     }
///     public string? GetContextKey() => _tenantAccessor.CurrentTenantId;
///     }
/// </example>
public interface ICacheContextProvider
{
    /// <summary>
    ///     Gets the current cache context key (e.g., tenant ID, user ID).
    ///     Returns null if no context isolation is needed.
    /// </summary>
    /// <returns>Context key to prefix cache entries, or null for global cache.</returns>
    string? GetContextKey();
}
