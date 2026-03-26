using CachedQueries.Abstractions;
using CachedQueries.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

/// <summary>
///     Tests for context-aware (multi-tenant) cache invalidation.
///     Verifies that the invalidator builds correct tag names that include/exclude context,
///     delegating to the cache provider's distributed tag infrastructure.
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
        {
            return new CacheInvalidator(_cacheProvider, _logger);
        }

        var contextProvider = Substitute.For<ICacheContextProvider>();
        contextProvider.GetContextKey().Returns(contextKey);

        var providerFactory = Substitute.For<ICacheProviderFactory>();
        providerFactory.GetAllProviders().Returns([_cacheProvider]);

        var services = new ServiceCollection();
        services.AddScoped<ICacheContextProvider>(_ => contextProvider);
        var sp = services.BuildServiceProvider();

        return new CacheInvalidator(_cacheProvider, providerFactory, sp.GetRequiredService<IServiceScopeFactory>(),
            _logger);
    }

    [Fact]
    public async Task InvalidateAsync_WithNoContext_ShouldOnlyIncludeGlobalEntityTags()
    {
        // Arrange
        var invalidator = CreateInvalidator();

        // Act
        await invalidator.InvalidateAsync([typeof(Order)]);

        // Assert - only global entity tag (no context prefix)
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags =>
                tags.Count == 1 &&
                tags.Contains($"tag:{typeof(Order).FullName}")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_WithContext_ShouldIncludeGlobalAndContextTags()
    {
        // Arrange
        var invalidator = CreateInvalidator("tenant-1");

        // Act
        await invalidator.InvalidateAsync([typeof(Order)]);

        // Assert - both global and context-specific entity tags
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags =>
                tags.Contains($"tag:{typeof(Order).FullName}") &&
                tags.Contains($"tenant-1:tag:{typeof(Order).FullName}")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_WithContext_ShouldNotIncludeOtherContextTags()
    {
        // Arrange
        var invalidator = CreateInvalidator("tenant-1");

        // Act
        await invalidator.InvalidateAsync([typeof(Order)]);

        // Assert - should NOT include tenant-2 tags
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags =>
                !tags.Any(t => t.Contains("tenant-2"))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WithContext_ShouldIncludeGlobalAndContextQualifiedTags()
    {
        // Arrange
        var invalidator = CreateInvalidator("tenant-1");

        // Act
        await invalidator.InvalidateByTagsAsync(["orders"]);

        // Assert - global tag "orders" + context-qualified tag "tenant-1:orders"
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags =>
                tags.Contains("tag:orders") &&
                tags.Contains("tenant-1:tag:orders")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WithNoContext_ShouldOnlyIncludeGlobalTags()
    {
        // Arrange
        var invalidator = CreateInvalidator();

        // Act
        await invalidator.InvalidateByTagsAsync(["orders"]);

        // Assert - only global tag
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags =>
                tags.Count == 1 &&
                tags.Contains("tag:orders")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_MultipleEntityTypes_ShouldBuildTagsForAll()
    {
        // Arrange
        var invalidator = CreateInvalidator("tenant-1");

        // Act
        await invalidator.InvalidateAsync([typeof(Order), typeof(Customer)]);

        // Assert - 4 tags: 2 global + 2 context-specific
        await _cacheProvider.Received(1).InvalidateByTagsAsync(
            Arg.Is<IReadOnlyList<string>>(tags =>
                tags.Count == 4 &&
                tags.Contains($"tag:{typeof(Order).FullName}") &&
                tags.Contains($"tenant-1:tag:{typeof(Order).FullName}") &&
                tags.Contains($"tag:{typeof(Customer).FullName}") &&
                tags.Contains($"tenant-1:tag:{typeof(Customer).FullName}")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearAllAsync_ShouldClearAllProviders()
    {
        // Arrange
        var invalidator = CreateInvalidator("tenant-1");

        // Act
        await invalidator.ClearAllAsync();

        // Assert
        await _cacheProvider.Received(1).ClearAsync(Arg.Any<CancellationToken>());
    }
}
