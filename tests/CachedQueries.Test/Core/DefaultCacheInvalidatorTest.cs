using CachedQueries.Core;
using CachedQueries.Core.Abstractions;
using FluentAssertions;
using Moq;
using Xunit;

namespace CachedQueries.Test.Core;

public class DefaultCacheInvalidatorTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("  A")]
    public async Task Should_Not_Throw_When_Key_Is_Empty(string? key)
    {
        // Given
        var cacheStore = new Mock<ICacheStore>();
        var cacheContext = new Mock<ICacheContextProvider>();
        var invalidator = new DefaultCacheInvalidator(cacheStore.Object, cacheContext.Object);

        // When
        var action = async () => await invalidator.LinkTagsAsync(key, [], CancellationToken.None);

        // Then
        await action.Should().NotThrowAsync();
        cacheStore.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }
}
