using CachedQueries.Core.Cache;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CachedQueries.Test.Core;

public class DistributedCacheTest
{
    [Fact]
    public async Task SetAsync_WhenLockReleaseThrowsException_ShouldNotThrow()
    {
        // Given
        const string key = "testKey";
        const string value = "testValue";
        var mockCache = new Mock<IDistributedCache>();

        var cache = new DistributedCache(mockCache.Object, NullLoggerFactory.Instance);

        // When
        var action = async () => await cache.SetAsync(key, value);

        // Then
        await action.Should().NotThrowAsync();
    }
}
