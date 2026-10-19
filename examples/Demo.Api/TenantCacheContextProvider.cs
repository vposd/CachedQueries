using CachedQueries.Abstractions;

namespace Demo.Api;

/// <summary>
///     Provides tenant isolation for cached queries.
///     Cache keys and tags are prefixed with the tenant ID so that each tenant has its own cache namespace.
/// </summary>
/// <remarks>
///     CachedQueries resolves this provider from a fresh DI scope, so it reads the tenant from ambient state
///     (<see cref="IHttpContextAccessor" />) rather than from a scoped service populated by middleware.
///     Outside a request (background services) it returns null: see <c>Jobs/PriceAdjustmentWorker</c> for how to
///     invalidate a tenant's entries explicitly there.
/// </remarks>
public class TenantCacheContextProvider(IHttpContextAccessor httpContextAccessor) : ICacheContextProvider
{
    public string? GetContextKey()
    {
        return Tenancy.FromRequest(httpContextAccessor.HttpContext);
    }
}
