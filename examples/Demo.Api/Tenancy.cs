namespace Demo.Api;

/// <summary>
///     Resolves the current tenant.
/// </summary>
/// <remarks>
///     DEMO ONLY: the tenant comes from a client-supplied <c>X-Tenant-Id</c> header so the demo can be driven with
///     curl. A real application must derive it from authenticated data (e.g. a "tenant_id" claim): the value is used
///     both to filter data and to isolate cache entries.
/// </remarks>
public static class Tenancy
{
    public const string HeaderName = "X-Tenant-Id";
    public const string DefaultTenant = "default";

    /// <summary>
    ///     Tenant from the request, or null when there is no request or no header (e.g. background services).
    /// </summary>
    public static string? FromRequest(HttpContext? http)
    {
        return http?.Request.Headers[HeaderName].FirstOrDefault();
    }

    /// <summary>
    ///     Tenant used to filter data in endpoints.
    /// </summary>
    public static string GetTenantId(this HttpContext http)
    {
        return FromRequest(http) ?? DefaultTenant;
    }
}
