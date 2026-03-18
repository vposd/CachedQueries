using CachedQueries.Abstractions;

namespace Demo.Api;

/// <summary>
/// Provides tenant isolation for cached queries.
/// Cache keys are prefixed with the tenant ID so that
/// each tenant has its own isolated cache namespace.
/// </summary>
public class TenantCacheContextProvider : ICacheContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantCacheContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetContextKey()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
    }
}
