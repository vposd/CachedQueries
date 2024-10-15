using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;
using CachedQueries.Core.Strategies;
using FluentAssertions;
using Moq;
using Xunit;

namespace CachedQueries.Test.Core.Strategies;

public class DefaultCacheCollectionStrategyTest
{
    private readonly Mock<ICacheKeyFactory> _cacheKeyFactoryMock;
    private readonly Mock<ICacheInvalidator> _cacheInvalidatorMock;
    private readonly Mock<ICacheStore> _cacheStoreMock;
    private readonly DefaultCacheCollectionStrategy _strategy;

    public DefaultCacheCollectionStrategyTest()
    {
        _cacheKeyFactoryMock = new Mock<ICacheKeyFactory>();
        _cacheInvalidatorMock = new Mock<ICacheInvalidator>();
        _cacheStoreMock = new Mock<ICacheStore>();

        _strategy = new DefaultCacheCollectionStrategy(
            _cacheKeyFactoryMock.Object,
            _cacheInvalidatorMock.Object,
            _cacheStoreMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnCachedValue_IfExists()
    {
        // Given
        var query = new List<int> { 1, 2, 3 }.AsQueryable();
        var options = new CachingOptions { Tags = ["test-tag"], CacheDuration = TimeSpan.FromMinutes(10) };
        var cacheKey = "cache-key";
        var cachedData = new List<int> { 1, 2, 3 };

        _cacheKeyFactoryMock.Setup(x => x.GetCacheKey(query, options.Tags)).Returns(cacheKey);
        _cacheStoreMock.Setup(x => x.GetAsync<IEnumerable<int>>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedData);

        // When
        var result = await _strategy.ExecuteAsync(query, options);

        // Then
        result.Should().BeEquivalentTo(cachedData);
        _cacheStoreMock.Verify(x => x.GetAsync<IEnumerable<int>>(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheStoreMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<IEnumerable<int>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidatorMock.Verify(x => x.LinkTagsAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStoreValue_IfNotCached()
    {
        // Given
        var query = new List<int> { 1, 2, 3 }.AsQueryable();
        var options = new CachingOptions { Tags = ["test-tag"], CacheDuration = TimeSpan.FromMinutes(10) };
        var cacheKey = "cache-key";

        _cacheKeyFactoryMock.Setup(x => x.GetCacheKey(query, options.Tags)).Returns(cacheKey);
        _cacheStoreMock.Setup(x => x.GetAsync<IEnumerable<int>>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<int>)null!);

        // When
        var result = await _strategy.ExecuteAsync(query, options);

        // Then
        result.Should().BeEquivalentTo(query.ToList());
        _cacheStoreMock.Verify(x => x.SetAsync(cacheKey, query.ToList(), options.CacheDuration, It.IsAny<CancellationToken>()), Times.Once);
        _cacheInvalidatorMock.Verify(x => x.LinkTagsAsync(cacheKey, options.Tags, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnQueryResult_IfKeyIsEmpty()
    {
        // Given
        var query = new List<int> { 1, 2, 3 }.AsQueryable();
        var options = new CachingOptions { Tags = ["test-tag"], CacheDuration = TimeSpan.FromMinutes(10) };

        _cacheKeyFactoryMock.Setup(x => x.GetCacheKey(query, options.Tags)).Returns(string.Empty);

        // When
        var result = await _strategy.ExecuteAsync(query, options);

        // Then
        result.Should().BeEquivalentTo(query.ToList());
        _cacheStoreMock.Verify(x => x.GetAsync<IEnumerable<int>>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheStoreMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<IEnumerable<int>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidatorMock.Verify(x => x.LinkTagsAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
