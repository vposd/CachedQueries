using FluentAssertions;
using Xunit;

namespace CachedQueries.Tests;

public class CachingOptionsTests
{
    [Fact]
    public void Default_ShouldHave30MinutesExpiration()
    {
        // Act
        var options = CachingOptions.Default;

        // Assert
        options.Expiration.Should().Be(TimeSpan.FromMinutes(30));
        options.UseSlidingExpiration.Should().BeFalse();
        options.CacheKey.Should().BeNull();
        options.Tags.Should().BeEmpty();
        options.SkipCache.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithExpiration_ShouldSetExpiration()
    {
        // Arrange
        var expiration = TimeSpan.FromHours(4);

        // Act
        var options = new CachingOptions(expiration);

        // Assert
        options.Expiration.Should().Be(expiration);
        options.UseSlidingExpiration.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithExpirationAndSliding_ShouldSetBoth()
    {
        // Arrange
        var expiration = TimeSpan.FromMinutes(10);

        // Act
        var options = new CachingOptions(expiration, true);

        // Assert
        options.Expiration.Should().Be(expiration);
        options.UseSlidingExpiration.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCacheKeyAndExpiration_ShouldSetBoth()
    {
        // Arrange
        var cacheKey = "my-key";
        var expiration = TimeSpan.FromHours(1);

        // Act
        var options = new CachingOptions(cacheKey, expiration);

        // Assert
        options.CacheKey.Should().Be(cacheKey);
        options.Expiration.Should().Be(expiration);
    }

    [Fact]
    public void InitSyntax_ShouldSetAllProperties()
    {
        // Act
        var options = new CachingOptions
        {
            Expiration = TimeSpan.FromHours(2),
            UseSlidingExpiration = true,
            CacheKey = "custom-key",
            Tags = ["tag1", "tag2"],
            SkipCache = true,
            IgnoreContext = true
        };

        // Assert
        options.Expiration.Should().Be(TimeSpan.FromHours(2));
        options.UseSlidingExpiration.Should().BeTrue();
        options.CacheKey.Should().Be("custom-key");
        options.Tags.Should().BeEquivalentTo("tag1", "tag2");
        options.SkipCache.Should().BeTrue();
        options.IgnoreContext.Should().BeTrue();
    }

    [Fact]
    public void Default_ShouldHaveIgnoreContextFalse()
    {
        var options = CachingOptions.Default;
        options.IgnoreContext.Should().BeFalse();
    }
}
