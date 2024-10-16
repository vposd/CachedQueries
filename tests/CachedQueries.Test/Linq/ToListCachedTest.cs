using AutoFixture;
using CachedQueries.Core;
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
using Microsoft.Extensions.Logging.Abstractions;
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
        InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
            .Where(x => x.Id > 0)
            .ToListCachedAsync(CancellationToken.None);

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
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
        InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
            .Where(x => x.Id > 0)
            .ToListCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
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
        InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
            .Where(x => x.Id > 0)
            .ToListCachedAsync(CancellationToken.None);

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
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
        InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var cacheManager = CacheManagerContainer.Resolve();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs.ToListCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();
        await cacheManager.CacheInvalidator.InvalidateCacheAsync(["blogs"], CancellationToken.None);

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs.ToListCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

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
        var services = InitCacheManager(cacheType);
        if (cacheType == typeof(DistributedCache))
        {
            var cache = ConfigureFailingDistributedCache(method);
            services.AddSingleton<IDistributedCache>(_ => cache.Object);

            var serviceProvider = services.BuildServiceProvider();
            CacheManagerContainer.Initialize(serviceProvider);
        }

        if (cacheType == typeof(MemoryCache))
        {
            var cache = ConfigureFailingMemoryCache(method);
            services.AddSingleton<IMemoryCache>(_ => cache.Object);

            var serviceProvider = services.BuildServiceProvider();
            CacheManagerContainer.Initialize(serviceProvider);
        }

        await using var context = _contextFactoryMock.Object();
        var cacheManager = CacheManagerContainer.Resolve();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs.ToListCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();
        await cacheManager.CacheInvalidator.InvalidateCacheAsync(["blogs"], CancellationToken.None);

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs.ToListCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

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
        InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs.ToListCachedAsync();

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.ChangeTracker.ExpireEntitiesCacheAsync(CancellationToken.None);
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs.ToListCachedAsync();

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
        InitCacheManager(cacheStore, true);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs
            .Include(x => x.Author)
            .Include(x => x.Posts).ThenInclude(x => x.Comments)
            .ToListCachedAsync();

        context.Blogs.Add(_fixture.Create<Blog>());

        await context.ChangeTracker.ExpireEntitiesCacheAsync(CancellationToken.None);
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs.ToListCachedAsync();

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(3);
    }
    
    #region Arrange

    private static IServiceCollection InitCacheManager(Type? cacheStoreType, bool initEmptyCacheKeyFactory = false)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        services.AddCachedQueries(options =>
        {
            if (cacheStoreType == typeof(MemoryCache))
            {
                options.UseCacheStore<MemoryCache>();
            }

            if (cacheStoreType == typeof(DistributedCache))
            {
                options.UseCacheStore<DistributedCache>();
            }

            if (initEmptyCacheKeyFactory)
            {
                options.UseCacheKeyFactory<EmptyKeyCacheFactory>();
            }
            else
            {
                options.UseEntityFramework();
            }
        });

        var serviceProvider = services.BuildServiceProvider();
        CacheManagerContainer.Initialize(serviceProvider);

        return services;
    }

    private static Mock<IDistributedCache> ConfigureFailingDistributedCache(string method)
    {
        var cache = new Mock<IDistributedCache>();

        switch (method)
        {
            case "Get":
                cache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception(""));
                break;
            case "Remove":
                cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception(""));
                break;
            case "Set":
                cache.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(),
                        It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception(""));
                break;
        }

        return cache;
    }

    private static Mock<IMemoryCache> ConfigureFailingMemoryCache(string method)
    {
        var cache = new Mock<IMemoryCache>();
        object expectedValue;
        switch (method)
        {
            case "Get":
                cache.Setup(x => x.TryGetValue(It.IsAny<object>(), out expectedValue))
                    .Throws(new Exception(""));
                break;
            case "Remove":
                cache.Setup(x => x.Remove(It.IsAny<object>()))
                    .Throws(new Exception(""));
                break;
            case "Set":
                cache.Setup(x => x.CreateEntry(It.IsAny<object>()))
                    .Throws(new Exception(""));
                break;
        }

        return cache;
    }

    #endregion
}
