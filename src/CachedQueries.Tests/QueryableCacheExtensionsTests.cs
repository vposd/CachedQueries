using CachedQueries.Abstractions;
using CachedQueries.Extensions;
using CachedQueries.Internal;
using CachedQueries.Providers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

[Collection("CacheServiceAccessor")]
public class QueryableCacheExtensionsTests : IDisposable
{
    private readonly ICacheProvider _cacheProvider;
    private readonly TestDbContext _context;
    private readonly ICacheInvalidator _invalidator;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly IMemoryCache _memoryCache;

    public QueryableCacheExtensionsTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new TestDbContext(options);

        // Seed data
        _context.Orders.AddRange(
            new Order { Id = 1, Name = "Order 1", Total = 100 },
            new Order { Id = 2, Name = "Order 2", Total = 200 },
            new Order { Id = 3, Name = "Order 3", Total = 300 }
        );
        _context.Customers.AddRange(
            new Customer { Id = 1, Name = "Customer 1", Email = "c1@test.com" },
            new Customer { Id = 2, Name = "Customer 2", Email = "c2@test.com" }
        );
        _context.SaveChanges();

        // Setup cache services
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheLogger = Substitute.For<ILogger<MemoryCacheProvider>>();
        _cacheProvider = new MemoryCacheProvider(_memoryCache, cacheLogger);
        _keyGenerator = new QueryCacheKeyGenerator();

        var invalidatorLogger = Substitute.For<ILogger<CacheInvalidator>>();
        _invalidator = new CacheInvalidator(_cacheProvider, invalidatorLogger);

        // Configure static accessor
        CacheServiceAccessor.Configure(_cacheProvider, _keyGenerator, _invalidator);
    }

    public void Dispose()
    {
        _context.Dispose();
        _memoryCache.Dispose();
        CacheServiceAccessor.Reset();
    }

    [Fact]
    public async Task ToListCachedAsync_ShouldReturnAndCacheResults()
    {
        // Act
        var result1 = await _context.Orders.ToListCachedAsync();
        var result2 = await _context.Orders.ToListCachedAsync();

        // Assert
        result1.Should().HaveCount(3);
        result2.Should().HaveCount(3);
        result1.Should().BeEquivalentTo(result2);
    }

    [Fact]
    public async Task ToListCachedAsync_WithOptions_ShouldUseCustomExpiration()
    {
        // Arrange
        var options = new CachingOptions(TimeSpan.FromHours(4));

        // Act
        var result = await _context.Orders.ToListCachedAsync(options);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task ToListCachedAsync_WithSkipCache_ShouldNotCache()
    {
        // Arrange
        var options = new CachingOptions { SkipCache = true };

        // Act
        var result = await _context.Orders.ToListCachedAsync(options);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task FirstOrDefaultCachedAsync_ShouldReturnAndCacheResult()
    {
        // Act
        var result1 = await _context.Orders.FirstOrDefaultCachedAsync();
        var result2 = await _context.Orders.FirstOrDefaultCachedAsync();

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Id.Should().Be(result2!.Id);
    }

    [Fact]
    public async Task FirstOrDefaultCachedAsync_WithPredicate_ShouldReturnMatchingResult()
    {
        // Act
        var result = await _context.Orders.FirstOrDefaultCachedAsync(o => o.Id == 2);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(2);
        result.Name.Should().Be("Order 2");
    }

    [Fact]
    public async Task FirstOrDefaultCachedAsync_WithNonMatchingPredicate_ShouldReturnNull()
    {
        // Act
        var result = await _context.Orders.FirstOrDefaultCachedAsync(o => o.Id == 999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FirstOrDefaultCachedAsync_WithOptions_ShouldWork()
    {
        // Arrange
        var options = new CachingOptions(TimeSpan.FromMinutes(10));

        // Act
        var result = await _context.Orders.FirstOrDefaultCachedAsync(o => o.Id == 1, options);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
    }

    [Fact]
    public async Task SingleOrDefaultCachedAsync_ShouldReturnAndCacheResult()
    {
        // Act
        var result = await _context.Orders.SingleOrDefaultCachedAsync(o => o.Id == 1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
    }

    [Fact]
    public async Task SingleOrDefaultCachedAsync_WithNullPredicate_ShouldThrowIfMultiple()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _context.Orders.SingleOrDefaultCachedAsync());
    }

    [Fact]
    public async Task SingleOrDefaultCachedAsync_WithOptions_ShouldWork()
    {
        // Arrange
        var options = new CachingOptions(TimeSpan.FromMinutes(10));

        // Act
        var result = await _context.Orders.SingleOrDefaultCachedAsync(o => o.Id == 1, options);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CountCachedAsync_ShouldReturnAndCacheCount()
    {
        // Act
        var count1 = await _context.Orders.CountCachedAsync();
        var count2 = await _context.Orders.CountCachedAsync();

        // Assert
        count1.Should().Be(3);
        count2.Should().Be(3);
    }

    [Fact]
    public async Task CountCachedAsync_WithOptions_ShouldWork()
    {
        // Arrange
        var options = new CachingOptions(TimeSpan.FromMinutes(5));

        // Act
        var count = await _context.Orders.CountCachedAsync(options);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task AnyCachedAsync_ShouldReturnTrue_WhenDataExists()
    {
        // Act
        var result = await _context.Orders.AnyCachedAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AnyCachedAsync_WithPredicate_ShouldReturnCorrectResult()
    {
        // Act - use different predicates that generate different cache keys
        var hasHighTotal = await _context.Orders.AnyCachedAsync(o => o.Total > 250);

        // Assert
        hasHighTotal.Should().BeTrue();
    }

    [Fact]
    public async Task AnyCachedAsync_WithNonMatchingPredicate_ShouldReturnFalse()
    {
        // Act
        var hasVeryHighTotal = await _context.Orders
            .AnyCachedAsync(o => o.Total > 1000, new CachingOptions { SkipCache = true });

        // Assert
        hasVeryHighTotal.Should().BeFalse();
    }

    [Fact]
    public async Task AnyCachedAsync_WithOptions_ShouldWork()
    {
        // Arrange
        var options = new CachingOptions(TimeSpan.FromMinutes(5));

        // Act
        var result = await _context.Orders.AnyCachedAsync(o => o.Id == 1, options);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CachedQueries_WhenNotConfigured_ShouldFallbackToNormalExecution()
    {
        // Arrange
        CacheServiceAccessor.Reset();

        // Act
        var result = await _context.Orders.ToListCachedAsync();

        // Assert
        result.Should().HaveCount(3);

        // Restore for other tests
        CacheServiceAccessor.Configure(_cacheProvider, _keyGenerator, _invalidator);
    }

    [Fact]
    public async Task ToListCachedAsync_WithCustomCacheKey_ShouldUseProvidedKey()
    {
        // Arrange
        var options = new CachingOptions
        {
            CacheKey = "custom-orders-key",
            Expiration = TimeSpan.FromMinutes(30)
        };

        // Act
        await _context.Orders.ToListCachedAsync(options);
        var cached = await _cacheProvider.GetAsync<List<Order>>("cq:custom-orders-key");

        // Assert
        cached.Should().NotBeNull();
        cached.Should().HaveCount(3);
    }

    [Fact]
    public async Task ToListCachedAsync_WithTags_ShouldRegisterTags()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["orders", "reports"]
        };

        // Act
        await _context.Orders.ToListCachedAsync(options);

        // Assert - invalidate by tag should work
        await _invalidator.InvalidateByTagsAsync(["orders"]);
        // After invalidation, the cached query should be removed
    }

    [Fact]
    public async Task ToListCachedAsync_WithInclude_ShouldCacheRelatedEntities()
    {
        // Arrange
        var order = _context.Orders.First();
        order.Items.Add(new OrderItem { ProductName = "Item 1", Quantity = 2 });
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.Orders
            .Include(o => o.Items)
            .ToListCachedAsync();

        // Assert
        result.Should().NotBeEmpty();
        result.First(o => o.Id == order.Id).Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FirstOrDefaultCachedAsync_WhenNotConfigured_ShouldFallback()
    {
        // Arrange
        CacheServiceAccessor.Reset();

        // Act
        var result = await _context.Orders.FirstOrDefaultCachedAsync(o => o.Id == 1);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);

        // Restore
        CacheServiceAccessor.Configure(_cacheProvider, _keyGenerator, _invalidator);
    }

    [Fact]
    public async Task FirstOrDefaultCachedAsync_WithSkipCache_ShouldNotCache()
    {
        // Arrange
        var options = new CachingOptions { SkipCache = true };

        // Act
        var result = await _context.Orders.FirstOrDefaultCachedAsync(o => o.Id == 1, options);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task FirstOrDefaultCachedAsync_NullPredicate_ShouldWork()
    {
        // Arrange
        var options = new CachingOptions(TimeSpan.FromMinutes(5));

        // Act
        var result = await _context.Orders.FirstOrDefaultCachedAsync(null, options);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SingleOrDefaultCachedAsync_WhenNotConfigured_ShouldFallback()
    {
        // Arrange
        CacheServiceAccessor.Reset();

        // Act
        var result = await _context.Orders.SingleOrDefaultCachedAsync(o => o.Id == 1);

        // Assert
        result.Should().NotBeNull();

        // Restore
        CacheServiceAccessor.Configure(_cacheProvider, _keyGenerator, _invalidator);
    }

    [Fact]
    public async Task SingleOrDefaultCachedAsync_WithSkipCache_ShouldNotCache()
    {
        // Arrange
        var options = new CachingOptions { SkipCache = true };

        // Act
        var result = await _context.Orders.SingleOrDefaultCachedAsync(o => o.Id == 1, options);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SingleOrDefaultCachedAsync_WithNullPredicate_AndSkipCache_ShouldWork()
    {
        // Arrange - use a context with only one order
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var singleContext = new TestDbContext(options);
        singleContext.Orders.Add(new Order { Id = 1, Name = "Single" });
        await singleContext.SaveChangesAsync();

        // Act
        var result = await singleContext.Orders.SingleOrDefaultCachedAsync(
            null, new CachingOptions { SkipCache = true });

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SingleOrDefaultCachedAsync_ReturnsNull_WhenNoMatch()
    {
        // Act
        var result = await _context.Orders.SingleOrDefaultCachedAsync(o => o.Id == 999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CountCachedAsync_WhenNotConfigured_ShouldFallback()
    {
        // Arrange
        CacheServiceAccessor.Reset();

        // Act
        var result = await _context.Orders.CountCachedAsync();

        // Assert
        result.Should().Be(3);

        // Restore
        CacheServiceAccessor.Configure(_cacheProvider, _keyGenerator, _invalidator);
    }

    [Fact]
    public async Task CountCachedAsync_WithSkipCache_ShouldNotCache()
    {
        // Arrange
        var options = new CachingOptions { SkipCache = true };

        // Act
        var result = await _context.Orders.CountCachedAsync(options);

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public async Task AnyCachedAsync_WhenNotConfigured_ShouldFallback()
    {
        // Arrange
        CacheServiceAccessor.Reset();

        // Act
        var result = await _context.Orders.AnyCachedAsync(o => o.Id == 1);

        // Assert
        result.Should().BeTrue();

        // Restore
        CacheServiceAccessor.Configure(_cacheProvider, _keyGenerator, _invalidator);
    }

    [Fact]
    public async Task AnyCachedAsync_WithSkipCache_ShouldNotCache()
    {
        // Arrange
        var options = new CachingOptions { SkipCache = true };

        // Act
        var result = await _context.Orders.AnyCachedAsync(o => o.Id == 1, options);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AnyCachedAsync_NullPredicate_WhenNotConfigured()
    {
        // Arrange
        CacheServiceAccessor.Reset();

        // Act
        var result = await _context.Orders.AnyCachedAsync();

        // Assert
        result.Should().BeTrue();

        // Restore
        CacheServiceAccessor.Configure(_cacheProvider, _keyGenerator, _invalidator);
    }

    [Fact]
    public async Task AnyCachedAsync_NullPredicate_WithOptions()
    {
        // Act
        var result = await _context.Orders.AnyCachedAsync(null, new CachingOptions(TimeSpan.FromMinutes(5)));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ToListCachedAsync_ReturnsCached_OnSecondCall()
    {
        // Arrange
        var cacheKey = "test-list-key";
        var options = new CachingOptions { CacheKey = cacheKey };

        // Act
        var result1 = await _context.Orders.ToListCachedAsync(options);
        var result2 = await _context.Orders.ToListCachedAsync(options);

        // Assert - both should return same count
        result1.Should().HaveCount(3);
        result2.Should().HaveCount(3);
    }

    [Fact]
    public async Task FirstOrDefaultCachedAsync_ReturnsCached_OnSecondCall()
    {
        // Arrange
        var cacheKey = "test-first-key";
        var options = new CachingOptions { CacheKey = cacheKey };

        // Act
        var result1 = await _context.Orders.FirstOrDefaultCachedAsync(o => o.Id == 1, options);
        var result2 = await _context.Orders.FirstOrDefaultCachedAsync(o => o.Id == 1, options);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Id.Should().Be(result2!.Id);
    }

    [Fact]
    public async Task CountCachedAsync_ReturnsCached_OnSecondCall()
    {
        // Arrange
        var cacheKey = "test-count-key";
        var options = new CachingOptions { CacheKey = cacheKey };

        // Act
        var result1 = await _context.Orders.CountCachedAsync(options);
        var result2 = await _context.Orders.CountCachedAsync(options);

        // Assert
        result1.Should().Be(3);
        result2.Should().Be(3);
    }

    [Fact]
    public async Task AnyCachedAsync_ReturnsCached_OnSecondCall()
    {
        // Arrange
        var cacheKey = "test-any-key";
        var options = new CachingOptions { CacheKey = cacheKey };

        // Act
        var result1 = await _context.Orders.AnyCachedAsync(null, options);
        var result2 = await _context.Orders.AnyCachedAsync(null, options);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    [Fact]
    public async Task FirstOrDefaultCachedAsync_WithTags_ShouldRegisterTags()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["first-tag", "orders"]
        };

        // Act
        var result = await _context.Orders.FirstOrDefaultCachedAsync(o => o.Id == 1, options);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
    }

    [Fact]
    public async Task SingleOrDefaultCachedAsync_WithTags_ShouldRegisterTags()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["single-tag", "orders"]
        };

        // Act
        var result = await _context.Orders.SingleOrDefaultCachedAsync(o => o.Id == 2, options);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(2);
    }

    [Fact]
    public async Task CountCachedAsync_WithTags_ShouldRegisterTags()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["count-tag", "orders"]
        };

        // Act
        var result = await _context.Orders.CountCachedAsync(options);

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public async Task AnyCachedAsync_WithTags_ShouldRegisterTags()
    {
        // Arrange
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromMinutes(30),
            Tags = ["any-tag", "orders"]
        };

        // Act
        var result = await _context.Orders.AnyCachedAsync(o => o.Total > 50, options);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SingleOrDefaultCachedAsync_WhenResultIsNull_ShouldNotCache()
    {
        // Arrange
        var options = new CachingOptions { CacheKey = "single-null-key" };

        // Act
        var result = await _context.Orders.SingleOrDefaultCachedAsync(o => o.Id == 999, options);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SingleOrDefaultCachedAsync_ReturnsCached_OnSecondCall()
    {
        // Arrange
        var cacheKey = "test-single-cached-key";
        var options = new CachingOptions { CacheKey = cacheKey };

        // Act
        var result1 = await _context.Orders.SingleOrDefaultCachedAsync(o => o.Id == 1, options);
        var result2 = await _context.Orders.SingleOrDefaultCachedAsync(o => o.Id == 1, options);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Id.Should().Be(1);
        result2!.Id.Should().Be(1);
    }
}
