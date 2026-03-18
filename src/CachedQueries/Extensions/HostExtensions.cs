using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CachedQueries.Extensions;

/// <summary>
/// Extension methods for configuring CachedQueries with IHost.
/// </summary>
public static class HostExtensions
{
    /// <summary>
    /// Initializes CachedQueries for the host.
    /// Call this in your Program.cs after builder.Build().
    /// </summary>
    public static IHost UseCachedQueries(this IHost host)
    {
        CacheServiceAccessor.Configure(host.Services);
        return host;
    }
}



