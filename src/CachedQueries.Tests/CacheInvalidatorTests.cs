using CachedQueries.Abstractions;
using CachedQueries.Internal;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

public class CacheInvalidatorTests
{
    private readonly ICacheProvider _cacheProvider;
    private readonly ILogger<CacheInvalidator> _logger;
    private readonly CacheInvalidator _invalidator;

    public CacheInvalidatorTests()
    {
        _cacheProvider = Substitute.For<ICacheProvider>();
        _logger = Substitute.For<ILogger<CacheInvalidator>>();
        _invalidator = new CacheInvalidator(_cacheProvider, _logger);
    }

    [Fact]
    public void RegisterCacheEntry_WithEntityTypes_ShouldTrackEntry()
    {
        // Arrange
        var cacheKey = "test-key";
        var entityTypes = new[] { typeof(Order), typeof(OrderItem) };

        // Act
        _invalidator.RegisterCacheEntry(cacheKey, entityTypes);

        // Assert - no exception means success
    }

    [Fact]
    public void RegisterCacheEntry_WithTags_ShouldTrackEntry()
    {
        // Arrange
        var cacheKey = "test-key";
        var tags = new[] { "orders", "items" };

        // Act
        _invalidator.RegisterCacheEntry(cacheKey, tags);

        // Assert - no exception means success
    }

    [Fact]
    public async Task InvalidateAsync_WithRegisteredEntityTypes_ShouldRemoveCache()
    {
        // Arrange
        var cacheKey = "test-key";
        _invalidator.RegisterCacheEntry(cacheKey, new[] { typeof(Order) });

        // Act
        await _invalidator.InvalidateAsync(new[] { typeof(Order) });

        // Assert
        await _cacheProvider.Received(1).RemoveAsync(cacheKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_WithUnregisteredEntityTypes_ShouldNotRemoveCache()
    {
        // Arrange
        _invalidator.RegisterCacheEntry("test-key", new[] { typeof(Order) });

        // Act
        await _invalidator.InvalidateAsync(new[] { typeof(Customer) });

        // Assert
        await _cacheProvider.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WithRegisteredTags_ShouldRemoveCache()
    {
        // Arrange
        var cacheKey = "test-key";
        _invalidator.RegisterCacheEntry(cacheKey, new[] { "orders" });

        // Act
        await _invalidator.InvalidateByTagsAsync(new[] { "orders" });

        // Assert
        await _cacheProvider.Received(1).RemoveAsync(cacheKey, Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IEnumerable<string>>(t => t.Contains("orders")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_WhenCacheProviderThrows_ShouldLogWarning()
    {
        // Arrange
        var cacheKey = "test-key";
        _invalidator.RegisterCacheEntry(cacheKey, new[] { typeof(Order) });
        _cacheProvider.RemoveAsync(cacheKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Cache error")));

        // Act
        await _invalidator.InvalidateAsync(new[] { typeof(Order) });

        // Assert - should not throw, just log
        _logger.ReceivedWithAnyArgs().LogWarning(default, default(Exception), default);
    }

    [Fact]
    public async Task InvalidateAsync_WithMultipleEntityTypes_ShouldRemoveAllRelatedCaches()
    {
        // Arrange
        _invalidator.RegisterCacheEntry("key1", new[] { typeof(Order) });
        _invalidator.RegisterCacheEntry("key2", new[] { typeof(Order) });
        _invalidator.RegisterCacheEntry("key3", new[] { typeof(Customer) });

        // Act
        await _invalidator.InvalidateAsync(new[] { typeof(Order) });

        // Assert
        await _cacheProvider.Received(1).RemoveAsync("key1", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("key2", Arg.Any<CancellationToken>());
        await _cacheProvider.DidNotReceive().RemoveAsync("key3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_AfterAlreadyInvalidated_ShouldNotRemoveAgain()
    {
        // Arrange
        _invalidator.RegisterCacheEntry("key1", new[] { typeof(Order) });

        // Act
        await _invalidator.InvalidateAsync(new[] { typeof(Order) });
        await _invalidator.InvalidateAsync(new[] { typeof(Order) });

        // Assert - only one call
        await _cacheProvider.Received(1).RemoveAsync("key1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WithMultipleTags_ShouldRemoveAllRelatedCaches()
    {
        // Arrange
        _invalidator.RegisterCacheEntry("key1", new[] { "tag1" });
        _invalidator.RegisterCacheEntry("key2", new[] { "tag2" });
        _invalidator.RegisterCacheEntry("key3", new[] { "tag1", "tag2" });

        // Act
        await _invalidator.InvalidateByTagsAsync(new[] { "tag1", "tag2" });

        // Assert
        await _cacheProvider.Received().RemoveAsync("key1", Arg.Any<CancellationToken>());
        await _cacheProvider.Received().RemoveAsync("key2", Arg.Any<CancellationToken>());
        await _cacheProvider.Received().RemoveAsync("key3", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WithProviderFactory_ShouldInvalidateAcrossAllProviders()
    {
        // Arrange - constructor with provider factory
        var defaultProvider = Substitute.For<ICacheProvider>();
        var singleProvider = Substitute.For<ICacheProvider>();
        var collectionProvider = Substitute.For<ICacheProvider>();
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([defaultProvider, singleProvider, collectionProvider]);

        var invalidator = new CacheInvalidator(defaultProvider, providerFactory, _logger);
        invalidator.RegisterCacheEntry("key1", new[] { "tag1" });

        // Act
        await invalidator.InvalidateByTagsAsync(new[] { "tag1" });

        // Assert - should call InvalidateByTagsAsync on all providers
        await defaultProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IEnumerable<string>>(t => t.Contains("tag1")),
            Arg.Any<CancellationToken>());
        await singleProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IEnumerable<string>>(t => t.Contains("tag1")),
            Arg.Any<CancellationToken>());
        await collectionProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IEnumerable<string>>(t => t.Contains("tag1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_WithProviderFactory_ShouldRemoveFromAllProviders()
    {
        // Arrange
        var defaultProvider = Substitute.For<ICacheProvider>();
        var singleProvider = Substitute.For<ICacheProvider>();
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([defaultProvider, singleProvider]);

        var invalidator = new CacheInvalidator(defaultProvider, providerFactory, _logger);
        invalidator.RegisterCacheEntry("key1", new[] { typeof(Order) });

        // Act
        await invalidator.InvalidateAsync(new[] { typeof(Order) });

        // Assert
        await defaultProvider.Received(1).RemoveAsync("key1", Arg.Any<CancellationToken>());
        await singleProvider.Received(1).RemoveAsync("key1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearAllAsync_ShouldClearAllProviders()
    {
        // Arrange
        var defaultProvider = Substitute.For<ICacheProvider>();
        var secondProvider = Substitute.For<ICacheProvider>();
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([defaultProvider, secondProvider]);

        var invalidator = new CacheInvalidator(defaultProvider, providerFactory, _logger);
        invalidator.RegisterCacheEntry("key1", new[] { typeof(Order) });

        // Act
        await invalidator.ClearAllAsync();

        // Assert
        await defaultProvider.Received(1).ClearAsync(Arg.Any<CancellationToken>());
        await secondProvider.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearAllAsync_WhenProviderThrows_ShouldContinueWithOtherProviders()
    {
        // Arrange
        var failingProvider = Substitute.For<ICacheProvider>();
        failingProvider.ClearAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Provider failed")));
        var workingProvider = Substitute.For<ICacheProvider>();
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([failingProvider, workingProvider]);

        var invalidator = new CacheInvalidator(failingProvider, providerFactory, _logger);

        // Act - should not throw
        await invalidator.ClearAllAsync();

        // Assert - both providers attempted
        await failingProvider.Received(1).ClearAsync(Arg.Any<CancellationToken>());
        await workingProvider.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearContextAsync_WithNoContext_ShouldNotRemoveAnything()
    {
        // Arrange: no context provider, no scope factory
        _invalidator.RegisterCacheEntry("key1", new[] { typeof(Order) }, contextKey: "tenant-1");

        // Act
        await _invalidator.ClearContextAsync();

        // Assert
        await _cacheProvider.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearContextAsync_ShouldRemoveEntriesForCurrentContext()
    {
        // Arrange - use constructor with IServiceProvider to get context
        var contextProvider = Substitute.For<ICacheContextProvider>();
        contextProvider.GetContextKey().Returns("my-tenant");
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([_cacheProvider]);
        var services = new ServiceCollection();
        services.AddScoped<ICacheContextProvider>(_ => contextProvider);
        var sp = services.BuildServiceProvider();

        var invalidator = new CacheInvalidator(_cacheProvider, providerFactory, sp, _logger);

        // Register both entity type entries and tag entries for the context
        invalidator.RegisterCacheEntry("entity-key", new[] { typeof(Order) }, contextKey: "my-tenant");
        invalidator.RegisterCacheEntry("tag-key", new[] { "my-tag" }, contextKey: "my-tenant");
        invalidator.RegisterCacheEntry("other-key", new[] { typeof(Order) }, contextKey: "other-tenant");

        // Act
        await invalidator.ClearContextAsync();

        // Assert
        await _cacheProvider.Received(1).RemoveAsync("entity-key", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("tag-key", Arg.Any<CancellationToken>());
        await _cacheProvider.DidNotReceive().RemoveAsync("other-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WithUnregisteredTags_ShouldNotRemoveCache()
    {
        // Arrange
        _invalidator.RegisterCacheEntry("key1", new[] { "tag1" });

        // Act
        await _invalidator.InvalidateByTagsAsync(new[] { "nonexistent" });

        // Assert
        await _cacheProvider.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByKeysAsync_ShouldRemoveKeysFromProvider()
    {
        // Arrange & Act
        await _invalidator.InvalidateByKeysAsync(new[] { "key1", "key2" });

        // Assert
        await _cacheProvider.Received(1).RemoveAsync("key1", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("key2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByKeysAsync_WithProviderFactory_ShouldRemoveFromAllProviders()
    {
        // Arrange
        var defaultProvider = Substitute.For<ICacheProvider>();
        var secondProvider = Substitute.For<ICacheProvider>();
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([defaultProvider, secondProvider]);

        var invalidator = new CacheInvalidator(defaultProvider, providerFactory, _logger);

        // Act
        await invalidator.InvalidateByKeysAsync(new[] { "key1" });

        // Assert
        await defaultProvider.Received(1).RemoveAsync("key1", Arg.Any<CancellationToken>());
        await secondProvider.Received(1).RemoveAsync("key1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByKeysAsync_WithEmptyKeys_ShouldNotCallProvider()
    {
        // Act
        await _invalidator.InvalidateByKeysAsync(Array.Empty<string>());

        // Assert
        await _cacheProvider.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetCurrentContextKey_WhenNoScopeFactory_ShouldReturnNull()
    {
        // The default constructor doesn't set _scopeFactory
        var result = _invalidator.GetCurrentContextKey();
        result.Should().BeNull();
    }
}


