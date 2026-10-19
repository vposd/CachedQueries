using CachedQueries.Extensions;
using CachedQueries.Internal;
using CachedQueries.Providers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CachedQueries.Tests;

public class CacheTagsTests
{
    [Fact]
    public void ForEntity_ShouldReturnFullTypeName()
    {
        CacheTags.ForEntity<Order>().Should().Be("CachedQueries.Tests.Order");
        CacheTags.ForEntity(typeof(Customer)).Should().Be("CachedQueries.Tests.Customer");
    }

    [Fact]
    public void ForEntity_WithTypeWithoutFullName_ShouldFallBackToName()
    {
        var genericParameter = typeof(List<>).GetGenericArguments()[0];

        CacheTags.ForEntity(genericParameter).Should().Be("T");
    }

    [Fact]
    public void ForEntity_WithNullType_ShouldThrow()
    {
        var act = () => CacheTags.ForEntity(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("tenant-1")]
    public void EntityTag_ShouldEqualUserTagOfForEntity(string? contextKey)
    {
        TrackingTags.EntityTag(typeof(Order), contextKey)
            .Should().Be(TrackingTags.UserTag(CacheTags.ForEntity<Order>(), contextKey));
    }
}

[Collection("CacheServiceAccessor")]
public class CacheTagsIntegrationTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

    public CacheTagsIntegrationTests()
    {
        _context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _context.Orders.Add(new Order { Id = 1, Name = "A", Total = 1 });
        _context.SaveChanges();

        var provider = new MemoryCacheProvider(_memoryCache, NullLogger<MemoryCacheProvider>.Instance);
        CacheServiceAccessor.Configure(provider, new QueryCacheKeyGenerator(),
            new CacheInvalidator(provider, NullLogger<CacheInvalidator>.Instance));
    }

    public void Dispose()
    {
        CacheServiceAccessor.Reset();
        _memoryCache.Dispose();
        _context.Dispose();
    }

    [Fact]
    public async Task WithTags_IncludingEntityTag_ShouldStillBeInvalidatedByEntityType()
    {
        await _context.Orders.Cacheable(o => o.WithTags("reports", CacheTags.ForEntity<Order>())).ToListAsync();
        _context.Orders.Add(new Order { Id = 2, Name = "B", Total = 2 });
        await _context.SaveChangesAsync();

        await Cache.InvalidateAsync<Order>();

        var result = await _context.Orders.Cacheable(o => o.WithTags("reports", CacheTags.ForEntity<Order>()))
            .ToListAsync();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task WithTags_WithoutEntityTag_ShouldNotBeInvalidatedByEntityType()
    {
        await _context.Orders.Cacheable(o => o.WithTags("reports")).ToListAsync();
        _context.Orders.Add(new Order { Id = 2, Name = "B", Total = 2 });
        await _context.SaveChangesAsync();

        await Cache.InvalidateAsync<Order>();

        var result = await _context.Orders.Cacheable(o => o.WithTags("reports")).ToListAsync();
        result.Should().HaveCount(1, "explicit tags replace automatic entity tracking");
    }

    [Fact]
    public async Task InvalidateByTag_WithEntityTag_ShouldInvalidateAutomaticallyTrackedEntries()
    {
        await _context.Orders.Cacheable().ToListAsync();
        _context.Orders.Add(new Order { Id = 2, Name = "B", Total = 2 });
        await _context.SaveChangesAsync();

        await Cache.InvalidateByTagAsync(CacheTags.ForEntity<Order>());

        (await _context.Orders.Cacheable().ToListAsync()).Should().HaveCount(2);
    }
}
