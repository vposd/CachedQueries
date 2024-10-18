using AutoFixture;
using CachedQueries.Core.Cache;
using CachedQueries.Core.Models;
using CachedQueries.DependencyInjection;
using CachedQueries.EntityFramework.Extensions;
using CachedQueries.Linq;
using CachedQueries.Test.Linq.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;
using MemoryCache = CachedQueries.Core.Cache.MemoryCache;

namespace CachedQueries.Test.Linq;

public class ToListCachedTest
{
    private readonly Mock<Func<TestDbContext>> _contextFactoryMock;
    private readonly Fixture _fixture;

    public ToListCachedTest()
    {
        _fixture = new Fixture();
        _contextFactoryMock = new Mock<Func<TestDbContext>>();
        _contextFactoryMock.Setup(x => x()).Returns(() =>
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(_fixture.Create<string>())
                .Options;
            var context = new TestDbContext(options);
            return context;
        });

        var services = new ServiceCollection();

        services.AddMemoryCache();
    }

    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToListCachedAsync_Should_Cache_List_Results(Type cacheStore)
    {
        // Given
        CacheManagerTestBed.InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Orders
            .Include(x => x.Products)
            .ThenInclude(x => x.Attributes)
            .Where(x => x.Id > 0)
            .ToListCachedAsync(CancellationToken.None);

        context.Orders.Add(_fixture.Create<Order>());
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Orders.ToListAsync();
        var entitiesFromCache = await context.Orders
            .Include(x => x.Products)
            .ThenInclude(x => x.Attributes)
            .Where(x => x.Id > 0)
            .ToListCachedAsync(CancellationToken.None);

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToListCachedAsync_Should_Cache_List_Results_With_Explicit_Tags(Type cacheStore)
    {
        // Given
        CacheManagerTestBed.InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Orders
            .Include(x => x.Products)
            .ThenInclude(x => x.Attributes)
            .Where(x => x.Id > 0)
            .ToListCachedAsync(new CachingOptions(TimeSpan.FromHours(1), ["blogs"]), CancellationToken.None);

        context.Orders.Add(_fixture.Create<Order>());
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Orders.ToListAsync();
        var entitiesFromCache = await context.Orders
            .Include(x => x.Products)
            .ThenInclude(x => x.Attributes)
            .Where(x => x.Id > 0)
            .ToListCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToListCachedAsync_Should_Cache_List_Results_With_Lifetime(Type cacheStore)
    {
        // Given
        CacheManagerTestBed.InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Orders
            .Include(x => x.Products)
            .ThenInclude(x => x.Attributes)
            .Where(x => x.Id > 0)
            .ToListCachedAsync(CancellationToken.None);

        context.Orders.Add(_fixture.Create<Order>());
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Orders.ToListAsync();
        var entitiesFromCache = await context.Orders
            .Include(x => x.Products)
            .ThenInclude(x => x.Attributes)
            .Where(x => x.Id > 0)
            .ToListCachedAsync(new CachingOptions(TimeSpan.FromHours(1)), CancellationToken.None);

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToListCachedAsync_Should_Update_Cache_List_Results_After_Expiration_With_Explicit_Tags(
        Type cacheStore)
    {
        // Given
        CacheManagerTestBed.InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var cacheManager = CacheManagerContainer.Resolve();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Orders.ToListCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

        context.Orders.Add(_fixture.Create<Order>());
        await context.SaveChangesAsync();
        await cacheManager.CacheInvalidator.InvalidateCacheAsync(["blogs"], CancellationToken.None);

        var entitiesFromDb = await context.Orders.ToListAsync();
        var entitiesFromCache =
            await context.Orders.ToListCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("Get", typeof(DistributedCache))]
    [InlineData("Remove", typeof(DistributedCache))]
    [InlineData("Set", typeof(DistributedCache))]
    [InlineData("Get", typeof(MemoryCache))]
    [InlineData("Remove", typeof(MemoryCache))]
    [InlineData("Set", typeof(MemoryCache))]
    public async Task ToListCachedAsync_Should_Not_Throw_Error_When_Set_Cache_Error(string method, Type cacheType)
    {
        // Given
        var services = CacheManagerTestBed.InitCacheManager(cacheType);
        if (cacheType == typeof(DistributedCache))
        {
            var cache = CacheManagerTestBed.ConfigureFailingDistributedCache(method);
            services.AddSingleton<IDistributedCache>(_ => cache.Object);

            var serviceProvider = services.BuildServiceProvider();
            CacheManagerContainer.Initialize(serviceProvider);
        }

        if (cacheType == typeof(MemoryCache))
        {
            var cache = CacheManagerTestBed.ConfigureFailingMemoryCache(method);
            services.AddSingleton<IMemoryCache>(_ => cache.Object);

            var serviceProvider = services.BuildServiceProvider();
            CacheManagerContainer.Initialize(serviceProvider);
        }

        await using var context = _contextFactoryMock.Object();
        var cacheManager = CacheManagerContainer.Resolve();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Orders.ToListCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

        context.Orders.Add(_fixture.Create<Order>());
        await context.SaveChangesAsync();
        await cacheManager.CacheInvalidator.InvalidateCacheAsync(["blogs"], CancellationToken.None);

        var entitiesFromDb = await context.Orders.ToListAsync();
        var entitiesFromCache =
            await context.Orders.ToListCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToListCachedAsync_Should_Update_Cache_List_Results_After_Expiration(Type cacheStore)
    {
        // Given
        CacheManagerTestBed.InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Orders.ToListCachedAsync();

        context.Orders.Add(_fixture.Create<Order>());
        await context.ChangeTracker.ExpireEntitiesCacheAsync(CancellationToken.None);
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Orders.ToListAsync();
        var entitiesFromCache = await context.Orders.ToListCachedAsync();

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToListCachedAsync_Should_Not_Check_Cache_If_Key_Is_Empty_With_Explicit_Tags(Type cacheStore)
    {
        // Given
        CacheManagerTestBed.InitCacheManager(cacheStore, true);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Orders
            .Include(x => x.Customer)
            .Include(x => x.Products).ThenInclude(x => x.Attributes)
            .ToListCachedAsync();

        context.Orders.Add(_fixture.Create<Order>());

        await context.ChangeTracker.ExpireEntitiesCacheAsync(CancellationToken.None);
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Orders.ToListAsync();
        var entitiesFromCache = await context.Orders.ToListCachedAsync();

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(3);
    }
}
