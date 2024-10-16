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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using MemoryCache = CachedQueries.Core.Cache.MemoryCache;

namespace CachedQueries.Test.Linq;

public class FirstOrDefaultCachedTest
{
    private readonly Mock<Func<TestDbContext>> _contextFactoryMock;
    private readonly Fixture _fixture;

    public FirstOrDefaultCachedTest()
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
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task FirstOrDefaultCachedAsync_Should_Cache_Single_Result_With_Explicit_Tags(Type cacheStore,
        bool usePredicate)
    {
        // Given
        InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        var firstEntityName = entities[0].Name;
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        if (usePredicate)
        {
            await context.Blogs
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id, new CachingOptions(["blogs"]));
        }
        else
        {
            await context.Blogs.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);
        }

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id, new CachingOptions(["blogs"]))
            : await context.Blogs.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be(firstEntityName);
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task FirstOrDefaultCachedAsync_Should_Cache_Single_Result(Type cacheStore, bool usePredicate)
    {
        // Given
        InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        var firstEntityName = entities[0].Name;
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        if (usePredicate)
        {
            await context.Blogs
                .Include(x => x.Author)
                .Include(x => x.Posts).ThenInclude(x => x.Comments)
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id, CancellationToken.None);
        }
        else
        {
            await context.Blogs
                .Include(x => x.Author)
                .Include(x => x.Posts).ThenInclude(x => x.Comments)
                .Where(x => x.Id == entities[0].Id).FirstOrDefaultCachedAsync(CancellationToken.None);
        }

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs
                .Include(x => x.Author)
                .Include(x => x.Posts).ThenInclude(x => x.Comments)
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id)
            : await context.Blogs
                .Include(x => x.Author)
                .Include(x => x.Posts).ThenInclude(x => x.Comments)
                .Where(x => x.Id == entities[0].Id).FirstOrDefaultCachedAsync();

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be(firstEntityName);
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task FirstOrDefaultCachedAsync_Should_Update_Cache_Single_Result_After_Expiration_With_Explicit_Tags(
        Type cacheStore, bool usePredicate)
    {
        // Given
        InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();
        var cacheManager = CacheManagerContainer.Resolve();

        // When
        if (usePredicate)
        {
            await context.Blogs.FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id,
                new CachingOptions(["blogs"]), CancellationToken.None);
        }
        else
        {
            await context.Blogs.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync( new CachingOptions(["blogs"]), CancellationToken.None);
        }

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.SaveChangesAsync();
        await cacheManager.CacheInvalidator.InvalidateCacheAsync(["blogs"], CancellationToken.None);

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id,  new CachingOptions(["blogs"]), CancellationToken.None)
            : await context.Blogs.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync( new CachingOptions(["blogs"]), CancellationToken.None);

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be("new name");
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task FirstOrDefaultCachedAsync_Should_Update_Cache_Single_Result_After_Expiration(Type cacheStore,
        bool usePredicate)
    {
        // Given
        InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        if (usePredicate)
        {
            await context.Blogs.FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id);
        }
        else
        {
            await context.Blogs.Where(x => x.Id == entities[0].Id).FirstOrDefaultCachedAsync();
        }

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.ChangeTracker.ExpireEntitiesCacheAsync(CancellationToken.None);
        await context.SaveChangesAsync();

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs.FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id)
            : await context.Blogs.Where(x => x.Id == entities[0].Id).FirstOrDefaultCachedAsync();

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be("new name");
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task FirstOrDefaultCachedAsync_Should_Not_Check_Cache_If_Key_Is_Empty(Type cacheStore,
        bool usePredicate)
    {
        // Given
        InitCacheManager(cacheStore, true);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        if (usePredicate)
        {
            await context.Blogs.FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id,
                new CachingOptions(["blogs"]));
        }
        else
        {
            await context.Blogs.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync( new CachingOptions(["blogs"]));
        }

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id,  new CachingOptions(["blogs"]))
            : await context.Blogs.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync( new CachingOptions(["blogs"]));

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be("new name");
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task FirstOrDefaultCachedAsync_Should_Invalidate_Cache_After_Timespan(Type cacheStore,
        bool usePredicate)
    {
        // Given
        InitCacheManager(cacheStore, true);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        if (usePredicate)
        {
            await context.Blogs.FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id, new CachingOptions(TimeSpan.FromSeconds(20)));
        }
        else
        {
            await context.Blogs.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(TimeSpan.FromMinutes(1)));
        }

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id, new CachingOptions(["blogs"]))
            : await context.Blogs.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(["blogs"]));

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be("new name");
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
