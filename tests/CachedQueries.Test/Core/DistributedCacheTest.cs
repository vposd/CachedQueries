using System;
using System.Threading;
using System.Threading.Tasks;
using CachedQueries.Core;
using CachedQueries.Core.Cache;
using CachedQueries.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using MemoryCache = CachedQueries.Core.Cache.MemoryCache;

namespace CachedQueries.Test.Core;

public class DistributedCacheTests
{
    [Fact]
    public async Task SetAsync_WhenLockReleaseThrowsException_ShouldNotThrow()
    {
        // Given
        const string key = "testKey";
        const string value = "testValue";
        var mockCache = new Mock<IDistributedCache>();
        var mockLockManager = new Mock<ILockManager>();
        var options = new CacheOptions();

        mockLockManager.Setup(m => m.LockAsync(key, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockLockManager.Setup(m => m.ReleaseLockAsync(key))
            .Throws(new Exception("Lock release exception"));

        var cache = new DistributedCache(mockCache.Object, NullLoggerFactory.Instance, mockLockManager.Object, options);

        // When
        var action = async () => await cache.SetAsync(key, value);

        // Then
        await action.Should().NotThrowAsync();
    }
}
