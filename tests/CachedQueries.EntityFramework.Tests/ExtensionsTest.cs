using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using CachedQueries.Core;
using CachedQueries.Core.Interfaces;
using CachedQueries.EntityFramework.Extensions;
using CachedQueries.EntityFramework.Tests.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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

        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        var serviceProvider = services.BuildServiceProvider();
        var memoryCache = serviceProvider.GetService<IMemoryCache>()!;
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        CacheManager.Cache = new MemoryCache(memoryCache, loggerFactory);
        CacheManager.CacheInvalidator = new DefaultCacheInvalidator(CacheManager.Cache);
        CacheManager.CacheKeyFactory = new QueryCacheKeyFactory();
    }

    [Fact]
    public async Task ToCachedListAsync_Should_Cache_List_Results_With_Explicit_Tags()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
            .Where(x => x.Id > 0)
            .ToCachedListAsync(new List<string> { nameof(Blog) });

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs
            .Include(x => x.Posts)
            .ThenInclude(x => x.Comments)
            .Where(x => x.Id > 0)
            .ToCachedListAsync(new List<string> { nameof(Blog) });

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(2);
    }

    [Fact]
    public async Task ToCachedListAsync_Should_Cache_List_Results()
    {
        // Given
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
            .ToCachedListAsync();

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(2);
    }

    [Fact]
    public async Task ToCachedListAsync_Should_Update_Cache_List_Results_After_Expiration_With_Explicit_Tags()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs.ToCachedListAsync(new List<string> { nameof(Blog) });

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.SaveChangesAsync();
        await CacheManager.InvalidateCacheAsync(new List<string> { nameof(Blog) });

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs.ToCachedListAsync(new List<string> { nameof(Blog) });

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(3);
    }

    [Fact]
    public async Task ToCachedListAsync_Should_Update_Cache_List_Results_After_Expiration()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs.ToCachedListAsync();

        context.Blogs.Add(_fixture.Create<Blog>());
        await context.ChangeTracker.ExpireEntitiesCacheAsync();
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs.ToCachedListAsync();

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(3);
    }

    [Fact]
    public async Task ToCachedListAsync_Should_Return_Data_From_Source_If_Set_Cache_Data_Throws_Error()
    {
        // Given
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

        var cacheMock = new Mock<ICache>();
        var data = new List<Blog>();
        data = null;

        CacheManager.Cache = cacheMock.Object;
        cacheMock.Setup(x => x.GetAsync<IEnumerable<Blog>>(It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(data);
        cacheMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), true, null, CancellationToken.None))
            .Throws(new Exception(""));

        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Blog>(2).ToList();
        context.Blogs.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        await context.Blogs.ToCachedListAsync();

        // Then
        cacheMock.Verify(cache => cache.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToCachedListAsync_Should_Not_Check_Cache_If_Key_Is_Empty_With_Explicit_Tags()
    {
        // Given
        CacheManager.CacheKeyFactory = new EmptyKeyCacheFactory();
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

        await context.ChangeTracker.ExpireEntitiesCacheAsync();
        await context.SaveChangesAsync();

        var entitiesFromDb = await context.Blogs.ToListAsync();
        var entitiesFromCache = await context.Blogs.ToCachedListAsync();

        // Then
        entitiesFromDb.Should().HaveCount(3);
        entitiesFromCache.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CachedFirstOrDefaultAsync_Should_Cache_Single_Result_With_Explicit_Tags(bool usePredicate)
    {
        // Given
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
    [InlineData(true)]
    [InlineData(false)]
    public async Task CachedFirstOrDefaultAsync_Should_Cache_Single_Result(bool usePredicate)
    {
        // Given
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
        ;

        // Then
        entityFromDb?.Name.Should().Be("new name");
        entityFromCache?.Name.Should().Be(firstEntityName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CachedFirstOrDefaultAsync_Should_Update_Cache_Single_Result_After_Expiration_With_Explicit_Tags(
        bool usePredicate)
    {
        // Given
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
        await CacheManager.InvalidateCacheAsync(new List<string> { nameof(Blog) });

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
    [InlineData(true)]
    [InlineData(false)]
    public async Task CachedFirstOrDefaultAsync_Should_Update_Cache_Single_Result_After_Expiration(bool usePredicate)
    {
        // Given
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
        await context.ChangeTracker.ExpireEntitiesCacheAsync();
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
    [InlineData(true)]
    [InlineData(false)]
    public async Task CachedFirstOrDefaultAsync_Should_Not_Check_Cache_If_Key_Is_Empty(bool usePredicate)
    {
        // Given
        CacheManager.CacheKeyFactory = new EmptyKeyCacheFactory();
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
    [InlineData(true)]
    [InlineData(false)]
    public async Task CachedFirstOrDefaultAsync_Should_Invalidate_Cache_After_Timespan(bool usePredicate)
    {
        // Given
        CacheManager.CacheKeyFactory = new EmptyKeyCacheFactory();
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
}

public class EmptyKeyCacheFactory : CacheKeyFactory
{
    public override string GetCacheKey<T>(IQueryable<T> query, IEnumerable<string> tags) where T : class
    {
        return "";
    }
}
