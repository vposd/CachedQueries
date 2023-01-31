using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using CachedQueries.Core;
using CachedQueries.Core.Cache;
using CachedQueries.Core.Interfaces;
using CachedQueries.DependencyInjection;
using CachedQueries.EntityFramework.Extensions;
using CachedQueries.EntityFramework.Tests.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using MemoryCache = CachedQueries.Core.Cache.MemoryCache;

namespace CachedQueries.EntityFramework.Tests;

public sealed class ExtensionsTest
{
    private readonly Mock<Func<TestDbContext>> _contextFactoryMock;
    private readonly Fixture _fixture;

    public ExtensionsTest()
    {
        _fixture = new Fixture();
        _contextFactoryMock = new Mock<Func<TestDbContext>>();
        _contextFactoryMock.Setup(x => x()).Returns(() =>
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(_fixture.Create<string>())
                .Options;
            var context = new TestDbContext(
                options);
            return context;
        });
    }

    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToCachedListAsync_Should_Cache_List_Results_With_Explicit_Tags(Type cacheStore)
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
            .ToCachedListAsync(new List<string> { nameof(Blog) }, CancellationToken.None);

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
            .Where(x => x.Id > 0)
            .ToCachedListAsync(new List<string> { nameof(Blog) }, CancellationToken.None);

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToCachedListAsync_Should_Cache_List_Results(Type cacheStore)
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
            .ToCachedListAsync(CancellationToken.None);

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
            .Where(x => x.Id > 0)
            .ToCachedListAsync(CancellationToken.None);

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToCachedListAsync_Should_Cache_List_Results_With_Lifetime(Type cacheStore)
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
            .ToCachedListAsync(CancellationToken.None);

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
            .Where(x => x.Id > 0)
            .ToCachedListAsync(TimeSpan.FromHours(1), CancellationToken.None);

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToCachedListAsync_Should_Update_Cache_List_Results_After_Expiration_With_Explicit_Tags(
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
        await context.Blogs.ToCachedListAsync(new List<string> { nameof(Blog) }, CancellationToken.None);

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();
        await cacheManager.CacheInvalidator.InvalidateCacheAsync(new List<string> { nameof(Blog) },
            CancellationToken.None);

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs.ToCachedListAsync(new List<string> { nameof(Blog) });

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
    public async Task ToCachedListAsync_Should_Not_Throw_Error_When_Set_Cache_Error(string method, Type cacheType)
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
        await context.Blogs.ToCachedListAsync(new List<string> { nameof(Blog) }, CancellationToken.None);

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();
        await cacheManager.CacheInvalidator.InvalidateCacheAsync(new List<string> { nameof(Blog) },
            CancellationToken.None);

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs.ToCachedListAsync(new List<string> { nameof(Blog) });

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(3);
    }


    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToCachedListAsync_Should_Update_Cache_List_Results_After_Expiration(Type cacheStore)
    {
        // Given
        InitCacheManager(cacheStore);

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs.ToCachedListAsync();

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.ChangeTracker.ExpireEntitiesCacheAsync(CancellationToken.None);
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs.ToCachedListAsync();

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(typeof(MemoryCache))]
    [InlineData(typeof(DistributedCache))]
    public async Task ToCachedListAsync_Should_Not_Check_Cache_If_Key_Is_Empty_With_Explicit_Tags(Type cacheStore)
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
            .ToCachedListAsync();

        context.Blogs.Add(_fixture.Create<Blog>());

        await context.ChangeTracker.ExpireEntitiesCacheAsync(CancellationToken.None);
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs.ToCachedListAsync();

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task CachedFirstOrDefaultAsync_Should_Cache_Single_Result_With_Explicit_Tags(Type cacheStore,
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
            await context.Blogs
                .CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id, new List<string> { nameof(Blog) });
        else
            await context.Blogs.Where(x => x.Id == entities[0].Id)
                .CachedFirstOrDefaultAsync(new List<string> { nameof(Blog) });

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs
                .CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id, new List<string> { nameof(Blog) })
            : await context.Blogs.Where(x => x.Id == entities[0].Id)
                .CachedFirstOrDefaultAsync(new List<string> { nameof(Blog) });

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be(firstEntityName);
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task CachedFirstOrDefaultAsync_Should_Cache_Single_Result(Type cacheStore, bool usePredicate)
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
            await context.Blogs
                .Include(x => x.Author)
                .Include(x => x.Posts).ThenInclude(x => x.Comments)
                .CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id);
        else
            await context.Blogs
                .Include(x => x.Author)
                .Include(x => x.Posts).ThenInclude(x => x.Comments)
                .Where(x => x.Id == entities[0].Id).CachedFirstOrDefaultAsync(CancellationToken.None);

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs
                .Include(x => x.Author)
                .Include(x => x.Posts).ThenInclude(x => x.Comments)
                .CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id)
            : await context.Blogs
                .Include(x => x.Author)
                .Include(x => x.Posts).ThenInclude(x => x.Comments)
                .Where(x => x.Id == entities[0].Id).CachedFirstOrDefaultAsync();

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be(firstEntityName);
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task CachedFirstOrDefaultAsync_Should_Update_Cache_Single_Result_After_Expiration_With_Explicit_Tags(
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
            await context.Blogs.CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id,
                new List<string> { nameof(Blog) });
        else
            await context.Blogs.Where(x => x.Id == entities[0].Id)
                .CachedFirstOrDefaultAsync(new List<string> { nameof(Blog) });

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.SaveChangesAsync();
        await cacheManager.CacheInvalidator.InvalidateCacheAsync(new List<string> { nameof(Blog) },
            CancellationToken.None);

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs
                .CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id, new List<string> { nameof(Blog) })
            : await context.Blogs.Where(x => x.Id == entities[0].Id)
                .CachedFirstOrDefaultAsync(new List<string> { nameof(Blog) });

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be("new name");
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task CachedFirstOrDefaultAsync_Should_Update_Cache_Single_Result_After_Expiration(Type cacheStore,
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
            await context.Blogs.CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id);
        else
            await context.Blogs.Where(x => x.Id == entities[0].Id).CachedFirstOrDefaultAsync();

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.ChangeTracker.ExpireEntitiesCacheAsync(CancellationToken.None);
        await context.SaveChangesAsync();

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs.CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id)
            : await context.Blogs.Where(x => x.Id == entities[0].Id).CachedFirstOrDefaultAsync();

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be("new name");
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task CachedFirstOrDefaultAsync_Should_Not_Check_Cache_If_Key_Is_Empty(Type cacheStore,
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
            await context.Blogs.CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id,
                new List<string> { nameof(Blog) });
        else
            await context.Blogs.Where(x => x.Id == entities[0].Id)
                .CachedFirstOrDefaultAsync(new List<string> { nameof(Blog) });

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs
                .CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id, new List<string> { nameof(Blog) })
            : await context.Blogs.Where(x => x.Id == entities[0].Id)
                .CachedFirstOrDefaultAsync(new List<string> { nameof(Blog) });

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be("new name");
    }

    [Theory]
    [InlineData(typeof(MemoryCache), true)]
    [InlineData(typeof(DistributedCache), true)]
    [InlineData(typeof(MemoryCache), false)]
    [InlineData(typeof(DistributedCache), false)]
    public async Task CachedFirstOrDefaultAsync_Should_Invalidate_Cache_After_Timespan(Type cacheStore,
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
            await context.Blogs.CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id, TimeSpan.FromSeconds(20));
        else
            await context.Blogs.Where(x => x.Id == entities[0].Id)
                .CachedFirstOrDefaultAsync(TimeSpan.FromMinutes(1));

        var changed = await context.Blogs.FirstAsync(x => x.Id == entities[0].Id);
        changed.Name = "new name";
        await context.SaveChangesAsync();

        var entityFromDb = await context.Blogs.FirstOrDefaultAsync(x => x.Id == entities[0].Id);
        var entityFromCache = usePredicate
            ? await context.Blogs
                .CachedFirstOrDefaultAsync(x => x.Id == entities[0].Id, new List<string> { nameof(Blog) })
            : await context.Blogs.Where(x => x.Id == entities[0].Id)
                .CachedFirstOrDefaultAsync(new List<string> { nameof(Blog) });

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be("new name");
    }


    private static IServiceCollection InitCacheManager(Type? cacheStoreType, bool initEmptyCacheKeyFactory = false)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        services.AddQueriesCaching(options =>
        {
            if (cacheStoreType == typeof(MemoryCache))
                options.UseCacheStore<MemoryCache>();

            if (cacheStoreType == typeof(DistributedCache))
                options.UseCacheStore<DistributedCache>();

            if (initEmptyCacheKeyFactory)
                options.UseKeyFactory<EmptyKeyCacheFactory>();
            else
                options.UseEntityFramework();
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
}

public class EmptyKeyCacheFactory : CacheKeyFactory
{
    public override string GetCacheKey<T>(IQueryable<T> query, IEnumerable<string> tags) where T : class
    {
        return string.Empty;
    }
}