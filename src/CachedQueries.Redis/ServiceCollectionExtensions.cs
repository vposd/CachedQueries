using System.Diagnostics.CodeAnalysis;
using CachedQueries.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CachedQueries.Redis;

/// <summary>
///     Extension methods for configuring CachedQueries with Redis.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds CachedQueries services with Redis distributed cache.
    ///     Requires AddStackExchangeRedisCache to be configured first.
    /// </summary>
    /// <example>
    ///     services.AddStackExchangeRedisCache(options => options.Configuration = "localhost:6379");
    ///     services.AddCachedQueriesWithRedis();
    /// </example>
    public static IServiceCollection AddCachedQueriesWithRedis(
        this IServiceCollection services,
        Action<CachedQueriesConfiguration>? configure = null)
    {
        // Register core services first (this adds a plain AddSingleton<RedisCacheProvider>)
        services.AddCachedQueries<RedisCacheProvider>(configure);

        // Replace with our factory that properly injects IConnectionMultiplexer when available.
        // Must come after AddCachedQueries so our factory is the last registration and wins in DI.
        services.AddSingleton<RedisCacheProvider>(sp =>
        {
            var cache = sp.GetRequiredService<IDistributedCache>();
            var logger = sp.GetRequiredService<ILogger<RedisCacheProvider>>();
            var config = sp.GetRequiredService<CachedQueriesConfiguration>();

            // Try to get IConnectionMultiplexer for atomic operations
            var redis = sp.GetService<IConnectionMultiplexer>();

            // Read InstanceName from RedisCacheOptions (set by AddStackExchangeRedisCache).
            // IDistributedCache auto-prepends InstanceName, but IDatabase does not —
            // we pass it to RedisCacheProvider so all access paths use the same prefix.
            var redisCacheOptions = sp.GetService<IOptions<RedisCacheOptions>>();
            var keyPrefix = redisCacheOptions?.Value.InstanceName ?? "";

            return new RedisCacheProvider(cache, redis, logger, keyPrefix, config.CachePrefix);
        });

        return services;
    }

    /// <summary>
    ///     Adds CachedQueries services with Redis using connection string.
    ///     Automatically configures IConnectionMultiplexer for atomic tag operations.
    /// </summary>
    /// <example>
    ///     services.AddCachedQueriesWithRedis("localhost:6379");
    /// </example>
    [ExcludeFromCodeCoverage]
    public static IServiceCollection AddCachedQueriesWithRedis(
        this IServiceCollection services,
        string connectionString,
        Action<CachedQueriesConfiguration>? configure = null)
    {
        // Register ConnectionMultiplexer for atomic operations
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(connectionString));

        services.AddStackExchangeRedisCache(options => { options.Configuration = connectionString; });

        return services.AddCachedQueriesWithRedis(configure);
    }
}
