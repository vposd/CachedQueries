using CachedQueries.Abstractions;
using CachedQueries.Internal;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

/// <summary>
/// Tests for context-aware (multi-tenant) cache invalidation.
/// Verifies: when invalidating, entries for current context + global entries are removed,
/// while entries for other contexts are preserved.
/// </summary>
public class ContextAwareInvalidationTests
{
    private readonly ICacheProvider _cacheProvider;
    private readonly ILogger<CacheInvalidator> _logger;

    public ContextAwareInvalidationTests()
    {
        _cacheProvider = Substitute.For<ICacheProvider>();
        _logger = Substitute.For<ILogger<CacheInvalidator>>();
    }

    private CacheInvalidator CreateInvalidator(string? contextKey = null)
    {
        if (contextKey is null)
            return new CacheInvalidator(_cacheProvider, _logger);

        var contextProvider = Substitute.For<ICacheContextProvider>();
        contextProvider.GetContextKey().Returns(contextKey);

        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([_cacheProvider]);

        var services = new ServiceCollection();
        services.AddScoped<ICacheContextProvider>(_ => contextProvider);
        var sp = services.BuildServiceProvider();

        return new CacheInvalidator(_cacheProvider, providerFactory, sp, _logger);
    }

    [Fact]
    public async Task InvalidateAsync_ShouldRemoveGlobalEntries()
    {
        // Arrange: register entry without context (global)
        var invalidator = CreateInvalidator();
        invalidator.RegisterCacheEntry("global-key", [typeof(Order)], contextKey: null);

        // Act
        await invalidator.InvalidateAsync([typeof(Order)]);

        // Assert
        await _cacheProvider.Received(1).RemoveAsync("global-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_ShouldRemoveCurrentContextEntries()
    {
        // Arrange: register entry with context "tenant-1"
        var invalidator = CreateInvalidator("tenant-1");
        invalidator.RegisterCacheEntry("tenant1-key", [typeof(Order)], contextKey: "tenant-1");

        // Act: invalidate with current context = "tenant-1"
        await invalidator.InvalidateAsync([typeof(Order)]);

        // Assert
        await _cacheProvider.Received(1).RemoveAsync("tenant1-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_ShouldNotRemoveOtherContextEntries()
    {
        // Arrange: register entry for tenant-2 but current context is tenant-1
        var invalidator = CreateInvalidator("tenant-1");
        invalidator.RegisterCacheEntry("tenant2-key", [typeof(Order)], contextKey: "tenant-2");

        // Act
        await invalidator.InvalidateAsync([typeof(Order)]);

        // Assert: tenant-2's entry should NOT be removed
        await _cacheProvider.DidNotReceive().RemoveAsync("tenant2-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_ShouldRemoveGlobalAndCurrentContext_ButNotOtherContexts()
    {
        // Arrange
        var invalidator = CreateInvalidator("tenant-1");
        invalidator.RegisterCacheEntry("global-key", [typeof(Order)], contextKey: null);
        invalidator.RegisterCacheEntry("tenant1-key", [typeof(Order)], contextKey: "tenant-1");
        invalidator.RegisterCacheEntry("tenant2-key", [typeof(Order)], contextKey: "tenant-2");

        // Act
        await invalidator.InvalidateAsync([typeof(Order)]);

        // Assert
        await _cacheProvider.Received(1).RemoveAsync("global-key", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("tenant1-key", Arg.Any<CancellationToken>());
        await _cacheProvider.DidNotReceive().RemoveAsync("tenant2-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_ShouldRespectContext()
    {
        // Arrange
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([_cacheProvider]);

        var contextProvider = Substitute.For<ICacheContextProvider>();
        contextProvider.GetContextKey().Returns("tenant-1");
        var services = new ServiceCollection();
        services.AddScoped<ICacheContextProvider>(_ => contextProvider);
        var sp = services.BuildServiceProvider();

        var invalidator = new CacheInvalidator(_cacheProvider, providerFactory, sp, _logger);

        invalidator.RegisterCacheEntry("global-tag-key", ["orders"], contextKey: null);
        invalidator.RegisterCacheEntry("tenant1-tag-key", ["orders"], contextKey: "tenant-1");
        invalidator.RegisterCacheEntry("tenant2-tag-key", ["orders"], contextKey: "tenant-2");

        // Act
        await invalidator.InvalidateByTagsAsync(["orders"]);

        // Assert
        await _cacheProvider.Received(1).RemoveAsync("global-tag-key", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("tenant1-tag-key", Arg.Any<CancellationToken>());
        await _cacheProvider.DidNotReceive().RemoveAsync("tenant2-tag-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_WithNoContext_ShouldOnlyRemoveGlobalEntries()
    {
        // Arrange: no context provider configured → current context is null
        var invalidator = CreateInvalidator();
        invalidator.RegisterCacheEntry("global-key", [typeof(Order)], contextKey: null);
        invalidator.RegisterCacheEntry("tenant-key", [typeof(Order)], contextKey: "tenant-1");

        // Act
        await invalidator.InvalidateAsync([typeof(Order)]);

        // Assert: only global entries removed
        await _cacheProvider.Received(1).RemoveAsync("global-key", Arg.Any<CancellationToken>());
        await _cacheProvider.DidNotReceive().RemoveAsync("tenant-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearContextAsync_ShouldOnlyRemoveCurrentContextEntries()
    {
        // Arrange
        var contextProvider = Substitute.For<ICacheContextProvider>();
        contextProvider.GetContextKey().Returns("tenant-1");
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([_cacheProvider]);
        var services = new ServiceCollection();
        services.AddScoped<ICacheContextProvider>(_ => contextProvider);
        var sp = services.BuildServiceProvider();

        var invalidator = new CacheInvalidator(_cacheProvider, providerFactory, sp, _logger);

        invalidator.RegisterCacheEntry("global-key", [typeof(Order)], contextKey: null);
        invalidator.RegisterCacheEntry("tenant1-key", [typeof(Order)], contextKey: "tenant-1");
        invalidator.RegisterCacheEntry("tenant2-key", [typeof(Order)], contextKey: "tenant-2");

        // Act
        await invalidator.ClearContextAsync();

        // Assert: only tenant-1 entries removed
        await _cacheProvider.Received(1).RemoveAsync("tenant1-key", Arg.Any<CancellationToken>());
        await _cacheProvider.DidNotReceive().RemoveAsync("global-key", Arg.Any<CancellationToken>());
        await _cacheProvider.DidNotReceive().RemoveAsync("tenant2-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearContextAsync_WithNoContext_ShouldLogWarningAndDoNothing()
    {
        // Arrange: no context provider
        var invalidator = CreateInvalidator();
        invalidator.RegisterCacheEntry("some-key", [typeof(Order)], contextKey: "tenant-1");

        // Act
        await invalidator.ClearContextAsync();

        // Assert: no removals
        await _cacheProvider.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearAllAsync_ShouldClearEverything()
    {
        // Arrange
        var invalidator = CreateInvalidator("tenant-1");
        invalidator.RegisterCacheEntry("global-key", [typeof(Order)], contextKey: null);
        invalidator.RegisterCacheEntry("tenant1-key", [typeof(Order)], contextKey: "tenant-1");
        invalidator.RegisterCacheEntry("tenant2-key", [typeof(Order)], contextKey: "tenant-2");

        // Act
        await invalidator.ClearAllAsync();

        // Assert: ClearAsync called on provider
        await _cacheProvider.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterCacheEntry_WithContext_ThenInvalidate_ShouldCleanTracking()
    {
        // Arrange
        var invalidator = CreateInvalidator("tenant-1");
        invalidator.RegisterCacheEntry("key1", [typeof(Order)], contextKey: "tenant-1");

        // Act: invalidate twice
        await invalidator.InvalidateAsync([typeof(Order)]);
        _cacheProvider.ClearReceivedCalls();
        await invalidator.InvalidateAsync([typeof(Order)]);

        // Assert: second call should not remove (already cleaned)
        await _cacheProvider.DidNotReceive().RemoveAsync("key1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_MultipleEntityTypes_ShouldRemoveMatchingAcrossTypes()
    {
        // Arrange
        var invalidator = CreateInvalidator("tenant-1");
        invalidator.RegisterCacheEntry("order-key", [typeof(Order)], contextKey: "tenant-1");
        invalidator.RegisterCacheEntry("customer-key", [typeof(Customer)], contextKey: "tenant-1");
        invalidator.RegisterCacheEntry("other-tenant-key", [typeof(Order)], contextKey: "tenant-2");

        // Act
        await invalidator.InvalidateAsync([typeof(Order), typeof(Customer)]);

        // Assert
        await _cacheProvider.Received(1).RemoveAsync("order-key", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("customer-key", Arg.Any<CancellationToken>());
        await _cacheProvider.DidNotReceive().RemoveAsync("other-tenant-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IgnoreContext_EntriesAreInvalidated_FromAnyContext()
    {
        // Arrange: register a global entry (IgnoreContext produces contextKey: null)
        // and invalidate from tenant-1 — global entries should always be invalidated
        var invalidator = CreateInvalidator("tenant-1");
        invalidator.RegisterCacheEntry("ignore-ctx-key", [typeof(Order)], contextKey: null);

        // Act
        await invalidator.InvalidateAsync([typeof(Order)]);

        // Assert: global entry removed even though current context is tenant-1
        await _cacheProvider.Received(1).RemoveAsync("ignore-ctx-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IgnoreContext_EntriesAreInvalidated_FromDifferentContext()
    {
        // Arrange: register global entry, then invalidate from tenant-2
        var invalidator = CreateInvalidator("tenant-2");
        invalidator.RegisterCacheEntry("global-shared-key", [typeof(Order)], contextKey: null);

        // Act
        await invalidator.InvalidateAsync([typeof(Order)]);

        // Assert
        await _cacheProvider.Received(1).RemoveAsync("global-shared-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IgnoreContext_TagEntries_AreInvalidatedFromAnyContext()
    {
        // Arrange
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([_cacheProvider]);

        var contextProvider = Substitute.For<ICacheContextProvider>();
        contextProvider.GetContextKey().Returns("tenant-1");
        var services = new ServiceCollection();
        services.AddScoped<ICacheContextProvider>(_ => contextProvider);
        var sp = services.BuildServiceProvider();

        var invalidator = new CacheInvalidator(_cacheProvider, providerFactory, sp, _logger);

        // Register a tag entry with null context (IgnoreContext)
        invalidator.RegisterCacheEntry("global-tag-entry", ["reports"], contextKey: null);

        // Act
        await invalidator.InvalidateByTagsAsync(["reports"]);

        // Assert
        await _cacheProvider.Received(1).RemoveAsync("global-tag-entry", Arg.Any<CancellationToken>());
    }
}
