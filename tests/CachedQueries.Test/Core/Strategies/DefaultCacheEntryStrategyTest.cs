using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;
using CachedQueries.Core.Strategies;
using FluentAssertions;
using Moq;
using Xunit;

namespace CachedQueries.Test.Core.Strategies;

public class DefaultCacheEntryStrategyTests
{
    private readonly DefaultCacheEntryStrategy _cacheEntryStrategy;
    private readonly Mock<ICacheInvalidator> _cacheInvalidatorMock;
    private readonly Mock<ICacheKeyFactory> _cacheKeyFactoryMock;
    private readonly Mock<ICacheStore> _cacheStoreMock;

    public DefaultCacheEntryStrategyTests()
    {
        _cacheKeyFactoryMock = new Mock<ICacheKeyFactory>();
        _cacheInvalidatorMock = new Mock<ICacheInvalidator>();
        _cacheStoreMock = new Mock<ICacheStore>();
        _cacheEntryStrategy = new DefaultCacheEntryStrategy(
            _cacheKeyFactoryMock.Object,
            _cacheInvalidatorMock.Object,
            _cacheStoreMock.Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_GivenEmptyCacheKey_WhenCacheKeyIsNullOrEmpty_ReturnsFirstOrDefaultWithoutCache()
    {
        // Given
        var query = new List<string> { "item1", "item2" }.AsQueryable();
        var options = new CachingOptions { Tags = new[] { "tag1" }, CacheDuration = TimeSpan.FromMinutes(5) };
        _cacheKeyFactoryMock.Setup(f => f.GetCacheKey(query, options.Tags)).Returns(string.Empty);

        // When
        var result = await _cacheEntryStrategy.ExecuteAsync(query, options);

        // Then
        result.Should().Be(query.FirstOrDefault());
        _cacheStoreMock.Verify(x => x.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheStoreMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_GivenCacheHit_WhenCacheKeyExists_ReturnsCachedValue()
    {
        // Given
        var query = new List<string> { "item1", "item2" }.AsQueryable();
        var cachedValue = "cachedItem";
        var options = new CachingOptions { Tags = new[] { "tag1" }, CacheDuration = TimeSpan.FromMinutes(5) };
        var cacheKey = "valid-cache-key";

        _cacheKeyFactoryMock.Setup(f => f.GetCacheKey(query, options.Tags)).Returns(cacheKey);
        _cacheStoreMock.Setup(s => s.GetAsync<string>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedValue);

        // When
        var result = await _cacheEntryStrategy.ExecuteAsync(query, options);

        // Then
        result.Should().Be(cachedValue);
        _cacheStoreMock.Verify(
            s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidatorMock.Verify(
            x => x.LinkTagsAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_GivenCacheMiss_WhenCacheIsEmpty_SetsCacheAndReturnsQueryFirstOrDefault()
    {
        // Given
        var query = new List<string> { "item1", "item2" }.AsQueryable();
        var options = new CachingOptions { Tags = new[] { "tag1" }, CacheDuration = TimeSpan.FromMinutes(5) };
        var cacheKey = "valid-cache-key";

        _cacheKeyFactoryMock.Setup(f => f.GetCacheKey(query, options.Tags)).Returns(cacheKey);
        _cacheStoreMock.Setup(s => s.GetAsync<string>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string)null!);

        // When
        var result = await _cacheEntryStrategy.ExecuteAsync(query, options);

        // Then
        result.Should().Be(query.FirstOrDefault());
        _cacheStoreMock.Verify(
            s => s.SetAsync(cacheKey, query.FirstOrDefault(), options.CacheDuration, It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheInvalidatorMock.Verify(x => x.LinkTagsAsync(cacheKey, options.Tags, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_GivenCacheMiss_WhenQueryIsEmpty_ReturnsNullWithoutSettingCache()
    {
        // Given
        var query = Enumerable.Empty<string>().AsQueryable();
        var options = new CachingOptions { Tags = new[] { "tag1" }, CacheDuration = TimeSpan.FromMinutes(5) };
        var cacheKey = "valid-cache-key";

        _cacheKeyFactoryMock.Setup(f => f.GetCacheKey(query, options.Tags)).Returns(cacheKey);
        _cacheStoreMock.Setup(s => s.GetAsync<string>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string)null!);

        // When
        var result = await _cacheEntryStrategy.ExecuteAsync(query, options);

        // Then
        result.Should().BeNull();
        _cacheStoreMock.Verify(
            s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidatorMock.Verify(
            x => x.LinkTagsAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
