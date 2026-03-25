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
    private readonly CacheInvalidator _invalidator;
    private readonly ILogger<CacheInvalidator> _logger;

    public CacheInvalidatorTests()
    {
        _cacheProvider = Substitute.For<ICacheProvider>();
        _logger = Substitute.For<ILogger<CacheInvalidator>>();
        _invalidator = new CacheInvalidator(_cacheProvider, _logger);
    }

    [Fact]
    public void RegisterCacheEntry_WithEntityTypes_IsNoOp()
    {
        // RegisterCacheEntry is now a no-op; tracking is handled by the provider via tags in SetAsync
        _invalidator.RegisterCacheEntry("test-key", new[] { typeof(Order), typeof(OrderItem) });
    }

    [Fact]
    public void RegisterCacheEntry_WithTags_IsNoOp()
    {
        _invalidator.RegisterCacheEntry("test-key", new[] { "orders", "items" });
    }

    [Fact]
    public async Task InvalidateAsync_ShouldCallProviderInvalidateByTags_WithEntityTypeTags()
    {
        // Act
        await _invalidator.InvalidateAsync(new[] { typeof(Order) });

        // Assert - should delegate to provider with global entity tag
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags =>
                tags.Contains($"tag:{typeof(Order).FullName}")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_WithUnrelatedEntityTypes_ShouldStillCallProvider()
    {
        // Act - even without prior registration, the provider handles tag lookup
        await _invalidator.InvalidateAsync(new[] { typeof(Customer) });

        // Assert
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags =>
                tags.Contains($"tag:{typeof(Customer).FullName}")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_WhenProviderThrows_ShouldLogWarning()
    {
        // Arrange
        _cacheProvider.InvalidateByTagsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Cache error")));

        // Act - should not throw
        await _invalidator.InvalidateAsync(new[] { typeof(Order) });

        // Assert
        _logger.ReceivedWithAnyArgs().LogWarning(default, default(Exception), default);
    }

    [Fact]
    public async Task InvalidateAsync_WithMultipleEntityTypes_ShouldBuildTagsForAll()
    {
        // Act
        await _invalidator.InvalidateAsync(new[] { typeof(Order), typeof(Customer) });

        // Assert - both entity types should be in the tags
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags =>
                tags.Contains($"tag:{typeof(Order).FullName}") &&
                tags.Contains($"tag:{typeof(Customer).FullName}")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_ShouldCallProviderInvalidateByTags()
    {
        // Act
        await _invalidator.InvalidateByTagsAsync(new[] { "orders" });

        // Assert - global user tag (no context)
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags => tags.Contains("tag:orders")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WithMultipleTags_ShouldIncludeAllTags()
    {
        // Act
        await _invalidator.InvalidateByTagsAsync(new[] { "tag1", "tag2" });

        // Assert
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags =>
                tags.Contains("tag:tag1") && tags.Contains("tag:tag2")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WithProviderFactory_ShouldInvalidateAcrossAllProviders()
    {
        // Arrange
        var defaultProvider = Substitute.For<ICacheProvider>();
        var singleProvider = Substitute.For<ICacheProvider>();
        var collectionProvider = Substitute.For<ICacheProvider>();
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([defaultProvider, singleProvider, collectionProvider]);

        var invalidator = new CacheInvalidator(defaultProvider, providerFactory, _logger);

        // Act
        await invalidator.InvalidateByTagsAsync(new[] { "tag1" });

        // Assert - should call InvalidateByTagsAsync on all providers
        await defaultProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IEnumerable<string>>(t => t.Contains("tag:tag1")),
            Arg.Any<CancellationToken>());
        await singleProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IEnumerable<string>>(t => t.Contains("tag:tag1")),
            Arg.Any<CancellationToken>());
        await collectionProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IEnumerable<string>>(t => t.Contains("tag:tag1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_WithProviderFactory_ShouldInvalidateAcrossAllProviders()
    {
        // Arrange
        var defaultProvider = Substitute.For<ICacheProvider>();
        var singleProvider = Substitute.For<ICacheProvider>();
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([defaultProvider, singleProvider]);

        var invalidator = new CacheInvalidator(defaultProvider, providerFactory, _logger);

        // Act
        await invalidator.InvalidateAsync(new[] { typeof(Order) });

        // Assert
        await defaultProvider.Received(1).InvalidateByTagsAsync(
            Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        await singleProvider.Received(1).InvalidateByTagsAsync(
            Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
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
    public async Task ClearContextAsync_WithNoContext_ShouldNotCallProvider()
    {
        // Act
        await _invalidator.ClearContextAsync();

        // Assert
        await _cacheProvider.DidNotReceive().InvalidateByTagsAsync(
            Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearContextAsync_ShouldInvalidateContextTag()
    {
        // Arrange
        var contextProvider = Substitute.For<ICacheContextProvider>();
        contextProvider.GetContextKey().Returns("my-tenant");
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([_cacheProvider]);
        var services = new ServiceCollection();
        services.AddScoped<ICacheContextProvider>(_ => contextProvider);
        var sp = services.BuildServiceProvider();

        var invalidator = new CacheInvalidator(_cacheProvider, providerFactory, sp, _logger);

        // Act
        await invalidator.ClearContextAsync();

        // Assert - should invalidate the context-level tag
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags => tags.Contains("my-tenant:tag:__context__")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByKeysAsync_ShouldRemoveKeyAndSuffixVariants()
    {
        // Act
        await _invalidator.InvalidateByKeysAsync(new[] { "key1" });

        // Assert — removes base key + :count + :any variants
        await _cacheProvider.Received(1).RemoveAsync("key1", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("key1:count", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("key1:any", Arg.Any<CancellationToken>());
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

        // Assert — both providers receive the base key
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
    public async Task InvalidateByKeysAsync_WithContext_ShouldRemoveBothPrefixedAndUnprefixed()
    {
        // Arrange
        var contextProvider = Substitute.For<ICacheContextProvider>();
        contextProvider.GetContextKey().Returns("tenant-a");
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([_cacheProvider]);
        var services = new ServiceCollection();
        services.AddScoped<ICacheContextProvider>(_ => contextProvider);
        var sp = services.BuildServiceProvider();

        var invalidator = new CacheInvalidator(_cacheProvider, providerFactory, sp, _logger);

        // Act
        await invalidator.InvalidateByKeysAsync(new[] { "order-1" });

        // Assert — should remove both prefixed and unprefixed variants
        await _cacheProvider.Received(1).RemoveAsync("tenant-a:order-1", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("tenant-a:order-1:count", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("tenant-a:order-1:any", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("order-1", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("order-1:count", Arg.Any<CancellationToken>());
        await _cacheProvider.Received(1).RemoveAsync("order-1:any", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByKeysAsync_WithIgnoreContext_ShouldRemoveUnprefixedKeys()
    {
        // Scenario: entry was cached with IgnoreContext() → stored as "shared-data" (no prefix)
        // Invalidation is called from a tenant context → should still remove "shared-data"
        var contextProvider = Substitute.For<ICacheContextProvider>();
        contextProvider.GetContextKey().Returns("tenant-a");
        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([_cacheProvider]);
        var services = new ServiceCollection();
        services.AddScoped<ICacheContextProvider>(_ => contextProvider);
        var sp = services.BuildServiceProvider();

        var invalidator = new CacheInvalidator(_cacheProvider, providerFactory, sp, _logger);

        // Act
        await invalidator.InvalidateByKeysAsync(new[] { "shared-data" });

        // Assert — unprefixed key is removed (handles IgnoreContext case)
        await _cacheProvider.Received(1).RemoveAsync("shared-data", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetCurrentContextKey_WhenNoScopeFactory_ShouldReturnNull()
    {
        var result = _invalidator.GetCurrentContextKey();
        result.Should().BeNull();
    }
}
