using CachedQueries.Core.Abstractions;
using CachedQueries.Core.Models;
using CachedQueries.Core.Strategies;
using FluentAssertions;
using Moq;
using Xunit;

namespace CachedQueries.Test.Core.Strategies;

public class DefaultCacheEntryStrategyTest
{
    private readonly Mock<ICacheKeyFactory> _cacheKeyFactoryMock;
    private readonly Mock<ICacheInvalidator> _cacheInvalidatorMock;
    private readonly Mock<ICacheStore> _cacheStoreMock;
    private readonly DefaultCacheEntryStrategy _strategy;

    public DefaultCacheEntryStrategyTest()
    {
        _cacheKeyFactoryMock = new Mock<ICacheKeyFactory>();
        _cacheInvalidatorMock = new Mock<ICacheInvalidator>();
        _cacheStoreMock = new Mock<ICacheStore>();

        _strategy = new DefaultCacheEntryStrategy(
            _cacheKeyFactoryMock.Object,
            _cacheInvalidatorMock.Object,
            _cacheStoreMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnCachedValue_WhenCacheKeyExists()
    {
        // Given
        var query = new[] { 1 }.AsQueryable();
        var options = new CachingOptions { Tags = ["test-tag"], CacheDuration = TimeSpan.FromMinutes(10) };
        var cacheKey = "cache-key";
        var cachedData = 1;

        _cacheKeyFactoryMock.Setup(x => x.GetCacheKey(query, options.Tags)).Returns(cacheKey);
        _cacheStoreMock.Setup(x => x.GetAsync<int>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedData);

        // When
        var result = await _strategy.ExecuteAsync(query, options);

        // Then
        result.Should().Be(cachedData);
        _cacheStoreMock.Verify(x => x.GetAsync<int>(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheStoreMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidatorMock.Verify(x => x.LinkTagsAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // [Fact]
    // public async Task ExecuteAsync_ShouldStoreAndReturnValue_WhenCacheMissOccurs()
    // {
    //     // Given
    //     var query = new[] { 1 }.AsQueryable();
    //     var options = new CachingOptions { Tags = ["test-tag"], CacheDuration = TimeSpan.FromMinutes(10) };
    //     var cacheKey = "cache-key";
    //
    //     _cacheKeyFactoryMock.Setup(x => x.GetCacheKey(query, options.Tags)).Returns(cacheKey);
    //     _cacheStoreMock.Setup(x => x.GetAsync<int?>(cacheKey, It.IsAny<CancellationToken>())).Returns(Task.FromResult((int?)default));
    //
    //     // When
    //     var result = await _strategy.ExecuteAsync(query, options);
    //
    //     // Then
    //     result.Should().Be(1);
    //     _cacheStoreMock.Verify(x => x.SetAsync(cacheKey, 1, options.CacheDuration, It.IsAny<CancellationToken>()), Times.Once);
    //     _cacheInvalidatorMock.Verify(x => x.LinkTagsAsync(cacheKey, options.Tags, It.IsAny<CancellationToken>()), Times.Once);
    // }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFirstOrDefault_WhenCacheKeyIsEmpty()
    {
        // Given
        var query = new[] { 1, 2, 3 }.AsQueryable();
        var options = new CachingOptions { Tags = ["test-tag"], CacheDuration = TimeSpan.FromMinutes(10) };

        _cacheKeyFactoryMock.Setup(x => x.GetCacheKey(query, options.Tags)).Returns(string.Empty);

        // When
        var result = await _strategy.ExecuteAsync(query, options);

        // Then
        result.Should().Be(query.FirstOrDefault());
        _cacheStoreMock.Verify(x => x.GetAsync<int>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheStoreMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        _cacheInvalidatorMock.Verify(x => x.LinkTagsAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // [Fact]
    // public async Task ExecuteAsync_ShouldReturnNull_WhenQueryIsEmpty()
    // {
    //     // Given
    //     var query = Enumerable.Empty<int>().AsQueryable();
    //     var options = new CachingOptions { Tags = ["test-tag"], CacheDuration = TimeSpan.FromMinutes(10) };
    //
    //     _cacheKeyFactoryMock.Setup(x => x.GetCacheKey(query, options.Tags)).Returns("cache-key");
    //     _cacheStoreMock.Setup(x => x.GetAsync<int>("cache-key", It.IsAny<CancellationToken>()))
    //         .ReturnsAsync(null);
    //     
    //     // When
    //     var result = await _strategy.ExecuteAsync(query, options);
    //
    //     // Then
    //     result.Should().Be(null);
    //     _cacheStoreMock.Verify(x => x.SetAsync("cache-key", (int?)null, options.CacheDuration, It.IsAny<CancellationToken>()), Times.Once);
    // }
}
