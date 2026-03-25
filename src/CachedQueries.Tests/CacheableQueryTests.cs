using System.Reflection;
using CachedQueries.Abstractions;
using CachedQueries.Extensions;
using CachedQueries.Internal;
using CachedQueries.Providers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

[Collection("CacheServiceAccessor")]
public class CacheableQueryTests : IDisposable
{
    private readonly ICacheProvider _cacheProvider;
    private readonly TestDbContext _context;
    private readonly ICacheInvalidator _invalidator;
    private readonly ICacheKeyGenerator _keyGenerator;
    private readonly IMemoryCache _memoryCache;

    public CacheableQueryTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new TestDbContext(options);

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

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheLogger = Substitute.For<ILogger<MemoryCacheProvider>>();
        _cacheProvider = new MemoryCacheProvider(_memoryCache, cacheLogger);
        _keyGenerator = new QueryCacheKeyGenerator();

        var invalidatorLogger = Substitute.For<ILogger<CacheInvalidator>>();
        _invalidator = new CacheInvalidator(_cacheProvider, invalidatorLogger);

        CacheServiceAccessor.Configure(_cacheProvider, _keyGenerator, _invalidator);
    }

    public void Dispose()
    {
        _context.Dispose();
        _memoryCache.Dispose();
        CacheServiceAccessor.Reset();
    }

    // --- Query property ---

    [Fact]
    public void Cacheable_Query_ShouldExposeUnderlyingQueryable()
    {
        var source = _context.Orders.Where(o => o.Total > 100);
        var cacheable = source.Cacheable();

        cacheable.Query.Should().BeSameAs(source);
    }

    // --- ToListAsync ---

    [Fact]
    public async Task Cacheable_ToListAsync_ShouldReturnAndCacheResults()
    {
        var result1 = await _context.Orders.Cacheable().ToListAsync();
        var result2 = await _context.Orders.Cacheable().ToListAsync();

        result1.Should().HaveCount(3);
        result2.Should().HaveCount(3);
    }

    [Fact]
    public async Task Cacheable_ToListAsync_WithExpire_ShouldWork()
    {
        var result = await _context.Orders
            .Cacheable(o => o.Expire(TimeSpan.FromHours(4)))
            .ToListAsync();

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Cacheable_ToListAsync_WithSlidingExpiration_ShouldWork()
    {
        var result = await _context.Orders
            .Cacheable(o => o.SlidingExpiration(TimeSpan.FromMinutes(10)))
            .ToListAsync();

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Cacheable_ToListAsync_WithSkipIf_ShouldBypassCache()
    {
        var result = await _context.Orders
            .Cacheable(o => o.SkipIf(true))
            .ToListAsync();

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Cacheable_ToListAsync_WithCustomKey_ShouldUseThatKey()
    {
        await _context.Orders
            .Cacheable(o => o.WithKey("my-orders"))
            .ToListAsync();

        var cached = await _cacheProvider.GetAsync<List<Order>>("cq:my-orders");
        cached.Should().NotBeNull();
        cached.Should().HaveCount(3);
    }

    [Fact]
    public async Task Cacheable_ToListAsync_WithTags_ShouldRegisterTags()
    {
        await _context.Orders
            .Cacheable(o => o.WithTags("orders", "reports"))
            .ToListAsync();

        // Invalidate by tag should remove the entry
        await _invalidator.InvalidateByTagsAsync(["orders"]);
    }

    [Fact]
    public async Task Cacheable_ToListAsync_WithCachingOptions_ShouldWork()
    {
        var opts = new CachingOptions(TimeSpan.FromMinutes(10));
        var result = await _context.Orders.Cacheable(opts).ToListAsync();

        result.Should().HaveCount(3);
    }

    // --- FirstOrDefaultAsync ---

    [Fact]
    public async Task Cacheable_FirstOrDefaultAsync_ShouldReturnAndCacheResult()
    {
        var r1 = await _context.Orders.Cacheable().FirstOrDefaultAsync();
        var r2 = await _context.Orders.Cacheable().FirstOrDefaultAsync();

        r1.Should().NotBeNull();
        r2.Should().NotBeNull();
        r1!.Id.Should().Be(r2!.Id);
    }

    [Fact]
    public async Task Cacheable_FirstOrDefaultAsync_WithPredicate_ShouldWork()
    {
        var result = await _context.Orders
            .Cacheable()
            .FirstOrDefaultAsync(o => o.Id == 2);

        result.Should().NotBeNull();
        result!.Id.Should().Be(2);
    }

    [Fact]
    public async Task Cacheable_FirstOrDefaultAsync_WithNonMatchingPredicate_ShouldReturnNull()
    {
        var result = await _context.Orders
            .Cacheable()
            .FirstOrDefaultAsync(o => o.Id == 999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Cacheable_FirstOrDefaultAsync_NullPredicate_ShouldWork()
    {
        var result = await _context.Orders
            .Cacheable(o => o.Expire(TimeSpan.FromMinutes(5)))
            .FirstOrDefaultAsync(null);

        result.Should().NotBeNull();
    }

    // --- SingleOrDefaultAsync ---

    [Fact]
    public async Task Cacheable_SingleOrDefaultAsync_ShouldReturnResult()
    {
        var result = await _context.Orders
            .Cacheable()
            .SingleOrDefaultAsync(o => o.Id == 1);

        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
    }

    [Fact]
    public async Task Cacheable_SingleOrDefaultAsync_ReturnsNull_WhenNoMatch()
    {
        var result = await _context.Orders
            .Cacheable()
            .SingleOrDefaultAsync(o => o.Id == 999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Cacheable_SingleOrDefaultAsync_NullPredicate_ShouldThrowIfMultiple()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _context.Orders.Cacheable().SingleOrDefaultAsync());
    }

    // --- CountAsync ---

    [Fact]
    public async Task Cacheable_CountAsync_ShouldReturnAndCacheCount()
    {
        var c1 = await _context.Orders.Cacheable().CountAsync();
        var c2 = await _context.Orders.Cacheable().CountAsync();

        c1.Should().Be(3);
        c2.Should().Be(3);
    }

    // --- AnyAsync ---

    [Fact]
    public async Task Cacheable_AnyAsync_ShouldReturnTrue()
    {
        var result = await _context.Orders.Cacheable().AnyAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Cacheable_AnyAsync_WithPredicate_ShouldWork()
    {
        var result = await _context.Orders
            .Cacheable()
            .AnyAsync(o => o.Total > 250);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Cacheable_AnyAsync_WithNonMatchingPredicate_ShouldReturnFalse()
    {
        var result = await _context.Orders
            .Cacheable(o => o.SkipIf(true))
            .AnyAsync(o => o.Total > 1000);

        result.Should().BeFalse();
    }

    // --- Not Configured fallback ---

    [Fact]
    public async Task Cacheable_WhenNotConfigured_ShouldFallbackToNormalExecution()
    {
        CacheServiceAccessor.Reset();

        var list = await _context.Orders.Cacheable().ToListAsync();
        list.Should().HaveCount(3);

        var first = await _context.Orders.Cacheable().FirstOrDefaultAsync(o => o.Id == 1);
        first.Should().NotBeNull();

        var count = await _context.Orders.Cacheable().CountAsync();
        count.Should().Be(3);

        var any = await _context.Orders.Cacheable().AnyAsync();
        any.Should().BeTrue();

        CacheServiceAccessor.Configure(_cacheProvider, _keyGenerator, _invalidator);
    }

    // --- Cached on second call ---

    [Fact]
    public async Task Cacheable_FirstOrDefaultAsync_ReturnsCached_OnSecondCall()
    {
        var opts = new CachingOptions { CacheKey = "first-cached" };
        var r1 = await _context.Orders.Cacheable(opts).FirstOrDefaultAsync(o => o.Id == 1);
        var r2 = await _context.Orders.Cacheable(opts).FirstOrDefaultAsync(o => o.Id == 1);

        r1.Should().NotBeNull();
        r2.Should().NotBeNull();
        r1!.Id.Should().Be(r2!.Id);
    }

    [Fact]
    public async Task Cacheable_CountAsync_ReturnsCached_OnSecondCall()
    {
        var opts = new CachingOptions { CacheKey = "count-cached" };
        var c1 = await _context.Orders.Cacheable(opts).CountAsync();
        var c2 = await _context.Orders.Cacheable(opts).CountAsync();

        c1.Should().Be(3);
        c2.Should().Be(3);
    }

    [Fact]
    public async Task Cacheable_AnyAsync_ReturnsCached_OnSecondCall()
    {
        var opts = new CachingOptions { CacheKey = "any-cached" };
        var r1 = await _context.Orders.Cacheable(opts).AnyAsync(null);
        var r2 = await _context.Orders.Cacheable(opts).AnyAsync(null);

        r1.Should().BeTrue();
        r2.Should().BeTrue();
    }

    [Fact]
    public async Task Cacheable_SingleOrDefaultAsync_ReturnsCached_OnSecondCall()
    {
        var opts = new CachingOptions { CacheKey = "single-cached" };
        var r1 = await _context.Orders.Cacheable(opts).SingleOrDefaultAsync(o => o.Id == 1);
        var r2 = await _context.Orders.Cacheable(opts).SingleOrDefaultAsync(o => o.Id == 1);

        r1.Should().NotBeNull();
        r2.Should().NotBeNull();
        r1!.Id.Should().Be(1);
        r2!.Id.Should().Be(1);
    }

    // --- UseTarget override ---

    [Fact]
    public async Task Cacheable_UseTarget_ShouldOverrideAutoTarget()
    {
        var result = await _context.Orders
            .Cacheable(o => o.UseTarget(CacheTarget.Collection))
            .FirstOrDefaultAsync(o => o.Id == 1);

        result.Should().NotBeNull();
    }

    // --- IgnoreContext ---

    [Fact]
    public async Task Cacheable_IgnoreContext_ToListAsync_ShouldStoreWithoutContextPrefix()
    {
        // Arrange: configure a context provider returning "tenant-1"
        ConfigureWithContextProvider("tenant-1");

        await _context.Orders
            .Cacheable(o => o.IgnoreContext().WithKey("global-orders"))
            .ToListAsync();

        // The key should be stored as "cq:global-orders" (no context), not "cq:tenant-1:global-orders"
        var globalCached = await _cacheProvider.GetAsync<List<Order>>("cq:global-orders");
        globalCached.Should().NotBeNull();
        globalCached.Should().HaveCount(3);

        var prefixedCached = await _cacheProvider.GetAsync<List<Order>>("cq:tenant-1:global-orders");
        prefixedCached.Should().BeNull();
    }

    [Fact]
    public async Task Cacheable_WithoutIgnoreContext_ShouldStoreWithContextPrefix()
    {
        // Arrange: configure a context provider returning "tenant-1"
        ConfigureWithContextProvider("tenant-1");

        await _context.Orders
            .Cacheable(o => o.WithKey("tenant-orders"))
            .ToListAsync();

        // The key should be stored with context: "cq:tenant-1:tenant-orders"
        var prefixedCached = await _cacheProvider.GetAsync<List<Order>>("cq:tenant-1:tenant-orders");
        prefixedCached.Should().NotBeNull();

        var globalCached = await _cacheProvider.GetAsync<List<Order>>("cq:tenant-orders");
        globalCached.Should().BeNull();
    }

    [Fact]
    public async Task Cacheable_IgnoreContext_FirstOrDefaultAsync_ShouldStoreGlobally()
    {
        ConfigureWithContextProvider("tenant-1");

        var result = await _context.Orders
            .Cacheable(o => o.IgnoreContext().WithKey("global-first"))
            .FirstOrDefaultAsync(o => o.Id == 1);

        result.Should().NotBeNull();

        var globalCached = await _cacheProvider.GetAsync<Order>("cq:global-first");
        globalCached.Should().NotBeNull();
    }

    [Fact]
    public async Task Cacheable_IgnoreContext_CountAsync_ShouldStoreGlobally()
    {
        ConfigureWithContextProvider("tenant-1");

        var count = await _context.Orders
            .Cacheable(o => o.IgnoreContext().WithKey("global-count"))
            .CountAsync();

        count.Should().Be(3);

        var globalCached = await _cacheProvider.GetAsync<int?>("cq:global-count:count");
        globalCached.Should().Be(3);
    }

    [Fact]
    public async Task Cacheable_IgnoreContext_AnyAsync_ShouldStoreGlobally()
    {
        ConfigureWithContextProvider("tenant-1");

        var any = await _context.Orders
            .Cacheable(o => o.IgnoreContext().WithKey("global-any"))
            .AnyAsync();

        any.Should().BeTrue();

        var globalCached = await _cacheProvider.GetAsync<bool?>("cq:global-any:any");
        globalCached.Should().Be(true);
    }

    [Fact]
    public async Task Cacheable_IgnoreContext_SingleOrDefaultAsync_ShouldStoreGlobally()
    {
        ConfigureWithContextProvider("tenant-1");

        var result = await _context.Orders
            .Cacheable(o => o.IgnoreContext().WithKey("global-single"))
            .SingleOrDefaultAsync(o => o.Id == 1);

        result.Should().NotBeNull();

        var globalCached = await _cacheProvider.GetAsync<Order>("cq:global-single");
        globalCached.Should().NotBeNull();
    }

    [Fact]
    public async Task Cacheable_IgnoreContext_ShouldBeReadableFromDifferentContext()
    {
        // Write from tenant-1 with IgnoreContext
        ConfigureWithContextProvider("tenant-1");
        await _context.Orders
            .Cacheable(o => o.IgnoreContext().WithKey("shared-orders"))
            .ToListAsync();

        // Read from tenant-2 with IgnoreContext — should hit cache
        ConfigureWithContextProvider("tenant-2");
        var result = await _context.Orders
            .Cacheable(o => o.IgnoreContext().WithKey("shared-orders"))
            .ToListAsync();

        result.Should().HaveCount(3);
    }

    private void ConfigureWithContextProvider(string contextKey)
    {
        var contextProvider = Substitute.For<ICacheContextProvider>();
        contextProvider.GetContextKey().Returns(contextKey);

        var services = new ServiceCollection();
        services.AddScoped<ICacheContextProvider>(_ => contextProvider);
        var sp = services.BuildServiceProvider();

        CacheServiceAccessor.Configure(sp);
        // Re-set the core services since Configure(sp) only resolves from DI
        CacheServiceAccessor.Configure(_cacheProvider, _keyGenerator, _invalidator);
        // Set up scope factory for context key resolution
        var field = typeof(CacheServiceAccessor).GetField("_scopeFactory",
            BindingFlags.NonPublic | BindingFlags.Static);
        field!.SetValue(null, sp.GetService<IServiceScopeFactory>());
    }
}
