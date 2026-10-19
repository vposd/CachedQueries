namespace CachedQueries.Abstractions;

/// <summary>
///     Provides cache context for multi-tenant or user-scoped caching.
///     Implement this interface to isolate cache entries by tenant, user, or other scope.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             The provider is registered as scoped but resolved from a <b>new</b> DI scope on every cache operation,
///             not from the current request scope. Read ambient state (e.g. <c>IHttpContextAccessor</c>, claims, an
///             <c>AsyncLocal</c>) instead of values that middleware stores in other scoped services.
///         </item>
///         <item>
///             The returned key becomes part of cache keys and tag names. Derive it from authenticated data
///             (e.g. a tenant claim), not from unvalidated client input.
///         </item>
///         <item>
///             Code without an ambient context (background services, message handlers) sees <c>null</c>: automatic
///             invalidation then only reaches global entries. Use the <see cref="ICacheInvalidator" /> overloads
///             that take an explicit context key in such code.
///         </item>
///     </list>
/// </remarks>
/// <example>
///     public class TenantCacheContextProvider(IHttpContextAccessor http) : ICacheContextProvider
///     {
///         public string? GetContextKey() => http.HttpContext?.User.FindFirst("tenant_id")?.Value;
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
