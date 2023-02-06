using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CachedQueries.Core;
using CachedQueries.Core.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CachedQueries.Test.Core;

public class DefaultCacheInvalidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("  A")]
    public async Task Should_Not_Throw_When_Key_Is_Empty(string key)
    {
        // Given
        var cacheStore = new Mock<ICacheStore>();
        var invalidator = new DefaultCacheInvalidator(cacheStore.Object);

        // When
        var action = async () => await invalidator.LinkTagsAsync(key, new List<string>(), CancellationToken.None);

        // Then
        await action.Should().NotThrowAsync();
        cacheStore.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }
}
