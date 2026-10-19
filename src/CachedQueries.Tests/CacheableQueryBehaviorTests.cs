using CachedQueries.Abstractions;
using CachedQueries.Extensions;
using CachedQueries.Internal;
using CachedQueries.Providers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CachedQueries.Tests;

[Collection("CacheServiceAccessor")]
public class CacheableQueryBehaviorTests : IDisposable
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly CountingProvider _provider;

    public CacheableQueryBehaviorTests()
    {
        using (var seed = CreateContext())
        {
            seed.Orders.AddRange(
                new Order { Id = 1, Name = "A", Total = 1 },
                new Order { Id = 2, Name = "B", Total = 2 });
            seed.SaveChanges();
        }

        _provider = new CountingProvider(
            new MemoryCacheProvider(_memoryCache, NullLogger<MemoryCacheProvider>.Instance));
        CacheServiceAccessor.Configure(_provider, new QueryCacheKeyGenerator(),
            new CacheInvalidator(_provider, NullLogger<CacheInvalidator>.Instance));
    }

    public void Dispose()
    {
        CacheServiceAccessor.Reset();
        _memoryCache.Dispose();
    }

    private TestDbContext CreateContext()
    {
        return new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(_databaseName).Options);
    }

    [Fact]
    public async Task CachedExecution_ShouldNotTrackEntities()
    {
        // Cached instances are shared between callers; they must not be attached to the loading context.
        using var context = CreateContext();

        var list = await context.Orders.Cacheable().ToListAsync();
        var single = await context.Orders.Cacheable().FirstOrDefaultAsync(o => o.Id == 1);

        list.Should().HaveCount(2);
        single.Should().NotBeNull();
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task SkipCache_ShouldKeepTracking()
    {
        using var context = CreateContext();

        await context.Orders.Cacheable(o => o.SkipIf(true)).ToListAsync();

        context.ChangeTracker.Entries().Should().HaveCount(2);
    }

    [Fact]
    public async Task IdentityResolutionQuery_ShouldBeRespected()
    {
        using var context = CreateContext();

        var result = await context.Orders.AsNoTrackingWithIdentityResolution().Where(o => o.Total > 0)
            .Cacheable().ToListAsync();

        result.Should().HaveCount(2);
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentFirstOrDefault_ShouldLoadAndStoreOnce()
    {
        // Single-item queries used to bypass stampede protection.
        _provider.GetDelay = TimeSpan.FromMilliseconds(20);

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            using var context = CreateContext();
            return await context.Orders.Cacheable().FirstOrDefaultAsync(o => o.Id == 2);
        }));
        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(o => o != null && o.Id == 2);
        _provider.SetCount.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentSingleOrDefault_ShouldLoadAndStoreOnce()
    {
        _provider.GetDelay = TimeSpan.FromMilliseconds(20);

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            using var context = CreateContext();
            return await context.Orders.Cacheable().SingleOrDefaultAsync(o => o.Id == 1);
        }));
        await Task.WhenAll(tasks);

        _provider.SetCount.Should().Be(1);
    }

    [Fact]
    public async Task NullSingleResult_ShouldNotBeCached()
    {
        using var context = CreateContext();

        (await context.Orders.Cacheable().SingleOrDefaultAsync(o => o.Id == 42)).Should().BeNull();

        _provider.SetCount.Should().Be(0);
    }

    private sealed class CountingProvider(ICacheProvider inner) : ICacheProvider
    {
        private int _setCount;
        public TimeSpan GetDelay { get; set; } = TimeSpan.Zero;
        public int SetCount => Volatile.Read(ref _setCount);

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            await Task.Delay(GetDelay, cancellationToken);
            return await inner.GetAsync<T>(key, cancellationToken);
        }

        public Task SetAsync<T>(string key, T value, CachingOptions options,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _setCount);
            return inner.SetAsync(key, value, options, cancellationToken);
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return inner.RemoveAsync(key, cancellationToken);
        }

        public Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            return inner.InvalidateByTagsAsync(tags, cancellationToken);
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            return inner.ClearAsync(cancellationToken);
        }
    }
}

[Collection("CacheServiceAccessor")]
public class DefaultOptionsTests : IDisposable
{
    public void Dispose()
    {
        CacheServiceAccessor.Reset();
    }

    [Fact]
    public void ConfiguredDefaultOptions_ShouldSeedBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCachedQueries(config => config.DefaultOptions = new CachingOptions(TimeSpan.FromHours(2), true));
        using var provider = services.BuildServiceProvider();
        provider.UseCachedQueries();

        var options = new CacheOptionsBuilder().Build();

        options.Expiration.Should().Be(TimeSpan.FromHours(2));
        options.UseSlidingExpiration.Should().BeTrue();
    }

    [Fact]
    public async Task Cacheable_WithoutOptions_ShouldUseConfiguredDefaultExpiration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCachedQueries(config => config.DefaultOptions = new CachingOptions(TimeSpan.FromMinutes(7)));
        using var provider = services.BuildServiceProvider();
        provider.UseCachedQueries();

        var recorder = new RecordingProvider();
        CacheServiceAccessor.Configure(recorder, new QueryCacheKeyGenerator(),
            new CacheInvalidator(recorder, NullLogger<CacheInvalidator>.Instance));

        using var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await context.Orders.Cacheable().ToListAsync();

        recorder.LastOptions!.Expiration.Should().Be(TimeSpan.FromMinutes(7));
    }

    [Fact]
    public void Reset_ShouldRestoreDefaultOptions()
    {
        CacheServiceAccessor.Reset();

        new CacheOptionsBuilder().Build().Expiration.Should().Be(TimeSpan.FromMinutes(30));
    }

    private sealed class RecordingProvider : ICacheProvider
    {
        public CachingOptions? LastOptions { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, CachingOptions options,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
