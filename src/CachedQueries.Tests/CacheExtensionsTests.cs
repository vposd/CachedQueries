using CachedQueries.Abstractions;
using CachedQueries.Extensions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

[Collection("CacheServiceAccessor")]
public class CacheExtensionsTests : IDisposable
{
    private readonly ICacheInvalidator _invalidator;

    public CacheExtensionsTests()
    {
        CacheServiceAccessor.Reset();
        _invalidator = Substitute.For<ICacheInvalidator>();
    }

    public void Dispose()
    {
        CacheServiceAccessor.Reset();
    }

    private void ConfigureAccessor()
    {
        var cacheProvider = Substitute.For<ICacheProvider>();
        var keyGenerator = Substitute.For<ICacheKeyGenerator>();
        CacheServiceAccessor.Configure(cacheProvider, keyGenerator, _invalidator);
    }

    // --- CacheExtensions static methods ---

    [Fact]
    public async Task ClearAllAsync_WhenNotConfigured_ShouldThrowInvalidOperationException()
    {
        var act = () => CacheExtensions.ClearAllAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*UseCachedQueries*");
    }

    [Fact]
    public async Task ClearAllAsync_WhenConfigured_ShouldDelegateToCacheInvalidator()
    {
        ConfigureAccessor();
        await CacheExtensions.ClearAllAsync();
        await _invalidator.Received(1).ClearAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearContextAsync_WhenNotConfigured_ShouldThrowInvalidOperationException()
    {
        var act = () => CacheExtensions.ClearContextAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ClearContextAsync_WhenConfigured_ShouldDelegateToCacheInvalidator()
    {
        ConfigureAccessor();
        await CacheExtensions.ClearContextAsync();
        await _invalidator.Received(1).ClearContextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_ByEntityTypes_WhenNotConfigured_ShouldThrow()
    {
        var act = () => CacheExtensions.InvalidateAsync(new[] { typeof(Order) });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task InvalidateAsync_ByEntityTypes_WhenConfigured_ShouldDelegate()
    {
        ConfigureAccessor();
        var types = new[] { typeof(Order) };
        await CacheExtensions.InvalidateAsync(types);
        await _invalidator.Received(1).InvalidateAsync(types, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_Generic_ShouldDelegateWithCorrectType()
    {
        ConfigureAccessor();
        await CacheExtensions.InvalidateAsync<Order>();
        await _invalidator.Received(1).InvalidateAsync(
            Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Order))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WhenNotConfigured_ShouldThrow()
    {
        var act = () => CacheExtensions.InvalidateByTagsAsync(new[] { "tag1" });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task InvalidateByTagsAsync_WhenConfigured_ShouldDelegate()
    {
        ConfigureAccessor();
        var tags = new[] { "tag1", "tag2" };
        await CacheExtensions.InvalidateByTagsAsync(tags);
        await _invalidator.Received(1).InvalidateByTagsAsync(tags, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateByTagAsync_ShouldDelegateToInvalidateByTagsAsync()
    {
        ConfigureAccessor();
        await CacheExtensions.InvalidateByTagAsync("my-tag");
        await _invalidator.Received(1).InvalidateByTagsAsync(
            Arg.Is<IEnumerable<string>>(t => t.Contains("my-tag")),
            Arg.Any<CancellationToken>());
    }

    // --- Cache static helper class ---

    [Fact]
    public async Task Cache_ClearAllAsync_ShouldDelegate()
    {
        ConfigureAccessor();
        await Cache.ClearAllAsync();
        await _invalidator.Received(1).ClearAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_ClearContextAsync_ShouldDelegate()
    {
        ConfigureAccessor();
        await Cache.ClearContextAsync();
        await _invalidator.Received(1).ClearContextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_InvalidateAsync_ShouldDelegate()
    {
        ConfigureAccessor();
        await Cache.InvalidateAsync<Order>();
        await _invalidator.Received(1).InvalidateAsync(
            Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Order))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_InvalidateByTagAsync_ShouldDelegate()
    {
        ConfigureAccessor();
        await Cache.InvalidateByTagAsync("tag");
        await _invalidator.Received(1).InvalidateByTagsAsync(
            Arg.Is<IEnumerable<string>>(t => t.Contains("tag")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cache_InvalidateByTagsAsync_ShouldDelegate()
    {
        ConfigureAccessor();
        var tags = new[] { "a", "b" };
        await Cache.InvalidateByTagsAsync(tags);
        await _invalidator.Received(1).InvalidateByTagsAsync(tags, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearAllAsync_WithCancellationToken_ShouldPassThrough()
    {
        ConfigureAccessor();
        using var cts = new CancellationTokenSource();
        await CacheExtensions.ClearAllAsync(cts.Token);
        await _invalidator.Received(1).ClearAllAsync(cts.Token);
    }

    [Fact]
    public async Task ClearContextAsync_WithCancellationToken_ShouldPassThrough()
    {
        ConfigureAccessor();
        using var cts = new CancellationTokenSource();
        await CacheExtensions.ClearContextAsync(cts.Token);
        await _invalidator.Received(1).ClearContextAsync(cts.Token);
    }
}
