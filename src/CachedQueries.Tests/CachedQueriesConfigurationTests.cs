using CachedQueries.Abstractions;
using CachedQueries.Providers;
using FluentAssertions;
using Xunit;

namespace CachedQueries.Tests;

public class CachedQueriesConfigurationTests
{
    [Fact]
    public void DefaultConfiguration_ShouldHaveExpectedDefaults()
    {
        // Arrange & Act
        var config = new CachedQueriesConfiguration();

        // Assert
        config.DefaultOptions.Should().Be(CachingOptions.Default);
        config.AutoInvalidation.Should().BeTrue();
        config.EnableLogging.Should().BeTrue();
    }

    [Fact]
    public void Configuration_CanBeModified()
    {
        // Arrange
        var config = new CachedQueriesConfiguration();

        // Act
        config.DefaultOptions = new CachingOptions(TimeSpan.FromHours(1));
        config.AutoInvalidation = false;
        config.EnableLogging = false;

        // Assert
        config.DefaultOptions.Expiration.Should().Be(TimeSpan.FromHours(1));
        config.AutoInvalidation.Should().BeFalse();
        config.EnableLogging.Should().BeFalse();
    }

    [Fact]
    public void UseSingleItemProvider_ShouldSetProviderType()
    {
        // Arrange
        var config = new CachedQueriesConfiguration();

        // Act
        var result = config.UseSingleItemProvider<MemoryCacheProvider>();

        // Assert
        result.Should().BeSameAs(config);
    }

    [Fact]
    public void UseCollectionProvider_ShouldSetProviderType()
    {
        // Arrange
        var config = new CachedQueriesConfiguration();

        // Act
        var result = config.UseCollectionProvider<MemoryCacheProvider>();

        // Assert
        result.Should().BeSameAs(config);
    }

    [Fact]
    public void UseScalarProvider_ShouldSetProviderType()
    {
        // Arrange
        var config = new CachedQueriesConfiguration();

        // Act
        var result = config.UseScalarProvider<MemoryCacheProvider>();

        // Assert
        result.Should().BeSameAs(config);
    }

    [Fact]
    public void UseProvider_ShouldSetAllProviderTypes()
    {
        // Arrange
        var config = new CachedQueriesConfiguration();

        // Act
        var result = config.UseProvider<MemoryCacheProvider>();

        // Assert
        result.Should().BeSameAs(config);
    }

    [Fact]
    public void FluentApi_ShouldSupportChaining()
    {
        // Arrange & Act
        var config = new CachedQueriesConfiguration()
            .UseSingleItemProvider<MemoryCacheProvider>()
            .UseCollectionProvider<MemoryCacheProvider>()
            .UseScalarProvider<MemoryCacheProvider>();

        // Assert
        config.Should().NotBeNull();
    }

    [Fact]
    public void UseContextProvider_ShouldSetContextProviderType()
    {
        // Arrange
        var config = new CachedQueriesConfiguration();

        // Act
        var result = config.UseContextProvider<TestCacheContextProvider>();

        // Assert
        result.Should().BeSameAs(config);
    }

    [Fact]
    public void UseContextProvider_ShouldSupportChaining()
    {
        // Arrange & Act
        var config = new CachedQueriesConfiguration()
            .UseContextProvider<TestCacheContextProvider>()
            .UseProvider<MemoryCacheProvider>();

        // Assert
        config.Should().NotBeNull();
    }

    [Fact]
    public void ProviderFactory_CanBeSet()
    {
        // Arrange
        var config = new CachedQueriesConfiguration();

        // Act
        config.ProviderFactory = sp => null!;

        // Assert
        config.ProviderFactory.Should().NotBeNull();
    }

    private class TestCacheContextProvider : Abstractions.ICacheContextProvider
    {
        public string? GetContextKey() => "test";
    }
}


