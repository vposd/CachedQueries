using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;
using CachedQueries.Core.Strategies;
using FluentAssertions;
using Moq;
using Xunit;

namespace CachedQueries.Test.Core.Strategies;

public class DefaultCacheCollectionStrategyTests
{
    private readonly Mock<ICacheKeyFactory> _cacheKeyFactoryMock;
    private readonly Mock<ICacheInvalidator> _cacheInvalidatorMock;
    private readonly Mock<ICacheStore> _cacheStoreMock;
    private readonly DefaultCacheCollectionStrategy _cacheCollectionStrategy;

    public DefaultCacheCollectionStrategyTests()
    {
        _cacheKeyFactoryMock = new Mock<ICacheKeyFactory>();
        _cacheInvalidatorMock = new Mock<ICacheInvalidator>();
        _cacheStoreMock = new Mock<ICacheStore>();
        _cacheCollectionStrategy = new DefaultCacheCollectionStrategy(
            _cacheKeyFactoryMock.Object,
            _cacheInvalidatorMock.Object,
            _cacheStoreMock.Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_GivenEmptyCacheKey_WhenCacheKeyIsNullOrEmpty_ReturnsQueryResultWithoutCache()
    {
        // Given
        var query = new List<string> { "item1", "item2" }.AsQueryable();
        var options = new CachingOptions { Tags = ["tag1"], CacheDuration = TimeSpan.FromMinutes(5) };
        _cacheKeyFactoryMock.Setup(f => f.GetCacheKey(query, options.Tags)).Returns(string.Empty);

        // When
        var result = await _cacheCollectionStrategy.ExecuteAsync(query, options);

        // Then
        result.Should().BeEquivalentTo(query.ToList());
        _cacheStoreMock.Verify(x => x.GetAsync<IEnumerable<string>>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheStoreMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_GivenCacheHit_WhenCacheKeyExists_ReturnsCachedValue()
    {
        // Given
        var query = new List<string> { "item1", "item2" }.AsQueryable();
        var cachedValue = new List<string> { "cachedItem1", "cachedItem2" };
        var options = new CachingOptions { Tags = ["tag1"], CacheDuration = TimeSpan.FromMinutes(5) };
        var cacheKey = "valid-cache-key";
        
        _cacheKeyFactoryMock.Setup(f => f.GetCacheKey(query, options.Tags)).Returns(cacheKey);
        _cacheStoreMock.Setup(s => s.GetAsync<IEnumerable<string>>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedValue);

        // When
        var result = await _cacheCollectionStrategy.ExecuteAsync(query, options);

        // Then
        result.Should().BeEquivalentTo(cachedValue);
        _cacheStoreMock.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidatorMock.Verify(x => x.LinkTagsAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_GivenCacheMiss_WhenCacheIsEmpty_SetsCacheAndReturnsQueryResult()
    {
        // Given
        var query = new List<string> { "item1", "item2" }.AsQueryable();
        var options = new CachingOptions { Tags = ["tag1"], CacheDuration = TimeSpan.FromMinutes(5) };
        var cacheKey = "valid-cache-key";
        
        _cacheKeyFactoryMock.Setup(f => f.GetCacheKey(query, options.Tags)).Returns(cacheKey);
        _cacheStoreMock.Setup(s => s.GetAsync<IEnumerable<string>>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string>)null!);

        // When
        var result = await _cacheCollectionStrategy.ExecuteAsync(query, options);

        // Then
        result.Should().BeEquivalentTo(query.ToList());
        _cacheStoreMock.Verify(s => s.SetAsync(cacheKey, query.ToList(), options.CacheDuration, It.IsAny<CancellationToken>()), Times.Once);
        _cacheInvalidatorMock.Verify(x => x.LinkTagsAsync(cacheKey, options.Tags, It.IsAny<CancellationToken>()), Times.Once);
    }
}
