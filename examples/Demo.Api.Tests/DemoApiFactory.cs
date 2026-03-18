using CachedQueries;
using CachedQueries.Extensions;
using CachedQueries.Redis;
using Demo.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Demo.Api.Tests;

public class DemoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string RedisConnectionString => _redis.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureServices(services =>
        {
            // Remove all EF-related registrations
            RemoveService<DbContextOptions<AppDbContext>>(services);
            RemoveService<AppDbContext>(services);

            // Remove in-memory cache provider registrations to replace with Redis
            RemoveService<CachedQueries.Abstractions.ICacheProvider>(services);
            RemoveService<CachedQueries.Providers.MemoryCacheProvider>(services);
            RemoveService<CachedQueries.Abstractions.ICacheProviderFactory>(services);
            RemoveService<CachedQueries.Abstractions.ICacheInvalidator>(services);
            RemoveService<CachedQueries.Abstractions.ICacheKeyGenerator>(services);
            RemoveService<CachedQueriesConfiguration>(services);
            RemoveService<CachedQueries.Interceptors.CacheInvalidationInterceptor>(services);
            RemoveService<CachedQueries.Interceptors.TransactionCacheInvalidationInterceptor>(services);

            // Re-register DbContext with Testcontainers PostgreSQL
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Register CachedQueries with Redis provider
            services.AddCachedQueriesWithRedis(_redis.GetConnectionString(), config =>
            {
                config.DefaultOptions = new CachingOptions(TimeSpan.FromMinutes(30));
                config.AutoInvalidation = true;
                config.UseContextProvider<TenantCacheContextProvider>();
            });

            // Wire up cache invalidation interceptors
            services.AddCacheInvalidation<AppDbContext>();
        });
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in descriptors)
            services.Remove(d);
    }

    public HttpClient CreateClientForTenant(string tenantId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return client;
    }

    /// <summary>
    /// Flush all Redis data AND reset the in-memory invalidator tracking.
    /// Use this before tests that verify cache invalidation to avoid
    /// pollution from previous tests.
    /// </summary>
    public async Task ResetCacheAsync()
    {
        // Flush Redis completely (allowAdmin required for FLUSHDB)
        var redis = ConnectionMultiplexer.Connect($"{_redis.GetConnectionString()},allowAdmin=true");
        var server = redis.GetServer(redis.GetEndPoints()[0]);
        await server.FlushDatabaseAsync();
        await redis.CloseAsync();

        // Also clear the in-memory invalidator via the API
        // (this clears the entity-type → cacheKey mappings)
        var client = CreateClient();
        await client.PostAsync("/api/cache/clear-all", null);
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _redis.StartAsync());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask());
    }
}

/// <summary>
/// Collection definition to share a single factory across all test classes.
/// This ensures all tests share the same PostgreSQL + Redis containers.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<DemoApiFactory>;
