using AutoFixture;
using CachedQueries.Core.Cache;
using CachedQueries.Core.Models;
using CachedQueries.DependencyInjection;
using CachedQueries.EntityFramework.Extensions;
using CachedQueries.Linq;
using CachedQueries.Test.Linq.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        CacheManagerTestBed.InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        var firstEntityName = entities[0].Number;
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        if (usePredicate)
        {
            await context.Orders
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id, new CachingOptions(["blogs"]));
        }
        else
        {
            await context.Orders.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);
        }

        var changed = await context.Orders.FirstAsync(x => x.Id == entities[0].Id);
        changed.Number = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Orders.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Orders
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id, new CachingOptions(["blogs"]))
            : await context.Orders.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

        // Then
        entityFromDb?.Number.Should().Be("new name");
        entityFromCache?.Number.Should().Be(firstEntityName);
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task FirstOrDefaultCachedAsync_Should_Cache_Single_Result(Type cacheStore, bool usePredicate)
    {
        // Given
        CacheManagerTestBed.InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        var firstEntityName = entities[0].Number;
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        if (usePredicate)
        {
            await context.Orders
                .Include(x => x.Customer)
                .Include(x => x.Products).ThenInclude(x => x.Attributes)
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id, CancellationToken.None);
        }
        else
        {
            await context.Orders
                .Include(x => x.Customer)
                .Include(x => x.Products).ThenInclude(x => x.Attributes)
                .Where(x => x.Id == entities[0].Id).FirstOrDefaultCachedAsync(CancellationToken.None);
        }

        var changed = await context.Orders.FirstAsync(x => x.Id == entities[0].Id);
        changed.Number = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Orders.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Orders
                .Include(x => x.Customer)
                .Include(x => x.Products).ThenInclude(x => x.Attributes)
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id)
            : await context.Orders
                .Include(x => x.Customer)
                .Include(x => x.Products).ThenInclude(x => x.Attributes)
                .Where(x => x.Id == entities[0].Id).FirstOrDefaultCachedAsync();

        // Then
        entityFromDb?.Number.Should().Be("new name");
        entityFromCache?.Number.Should().Be(firstEntityName);
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
        CacheManagerTestBed.InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();
        var cacheManager = CacheManagerContainer.Resolve();

        // When
        if (usePredicate)
        {
            await context.Orders.FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id,
                new CachingOptions(["blogs"]), CancellationToken.None);
        }
        else
        {
            await context.Orders.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);
        }

        var changed = await context.Orders.FirstAsync(x => x.Id == entities[0].Id);
        changed.Number = "new name";
        await context.SaveChangesAsync();
        await cacheManager.CacheInvalidator.InvalidateCacheAsync(["blogs"], CancellationToken.None);

        var entityFromDb = await context.Orders.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Orders
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id, new CachingOptions(["blogs"]),
                    CancellationToken.None)
            : await context.Orders.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(["blogs"]), CancellationToken.None);

        // Then
        entityFromDb?.Number.Should().Be("new name");
        entityFromCache?.Number.Should().Be("new name");
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
        CacheManagerTestBed.InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        if (usePredicate)
        {
            await context.Orders.FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id);
        }
        else
        {
            await context.Orders.Where(x => x.Id == entities[0].Id).FirstOrDefaultCachedAsync();
        }

        var changed = await context.Orders.FirstAsync(x => x.Id == entities[0].Id);
        changed.Number = "new name";
        await context.ChangeTracker.ExpireEntitiesCacheAsync(CancellationToken.None);
        await context.SaveChangesAsync();

        var entityFromDb = await context.Orders.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Orders.FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id)
            : await context.Orders.Where(x => x.Id == entities[0].Id).FirstOrDefaultCachedAsync();

        // Then
        entityFromDb?.Number.Should().Be("new name");
        entityFromCache?.Number.Should().Be("new name");
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
        CacheManagerTestBed.InitCacheManager(cacheStore, true);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        if (usePredicate)
        {
            await context.Orders.FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id,
                new CachingOptions(["blogs"]));
        }
        else
        {
            await context.Orders.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(["blogs"]));
        }

        var changed = await context.Orders.FirstAsync(x => x.Id == entities[0].Id);
        changed.Number = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Orders.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Orders
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id, new CachingOptions(["blogs"]))
            : await context.Orders.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(["blogs"]));

        // Then
        entityFromDb?.Number.Should().Be("new name");
        entityFromCache?.Number.Should().Be("new name");
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
        CacheManagerTestBed.InitCacheManager(cacheStore, true);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(2).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        if (usePredicate)
        {
            await context.Orders.FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id,
                new CachingOptions(TimeSpan.FromSeconds(20)));
        }
        else
        {
            await context.Orders.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(TimeSpan.FromMinutes(1)));
        }

        var changed = await context.Orders.FirstAsync(x => x.Id == entities[0].Id);
        changed.Number = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Orders.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Orders
                .FirstOrDefaultCachedAsync(x => x.Id == entities[0].Id, new CachingOptions(["blogs"]))
            : await context.Orders.Where(x => x.Id == entities[0].Id)
                .FirstOrDefaultCachedAsync(new CachingOptions(["blogs"]));

        // Then
        entityFromDb?.Number.Should().Be("new name");
        entityFromCache?.Number.Should().Be("new name");
    }
}
