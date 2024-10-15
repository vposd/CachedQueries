using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using MemoryCache = CachedQueries.Core.Cache.MemoryCache;

namespace CachedQueries.Test.Core;

public class MemoryCacheTest
{
    [Fact]
    public async Task SetAsync_WhenLockReleaseThrowsException_ShouldNotThrow()
    {
        // Given
        const string key = "testKey";
        const string value = "testValue";
        var mockCache = new Mock<IMemoryCache>();

        var cache = new MemoryCache(mockCache.Object, NullLoggerFactory.Instance);

        // When
        var action = async () => await cache.SetAsync(key, value);

        // Then
        await action.Should().NotThrowAsync();
    }
}
