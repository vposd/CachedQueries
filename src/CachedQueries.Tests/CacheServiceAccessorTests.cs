using CachedQueries.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

[Collection("CacheServiceAccessor")]
public class CacheServiceAccessorTests : IDisposable
{
    public CacheServiceAccessorTests()
    {
        CacheServiceAccessor.Reset();
    }

    public void Dispose()
    {
        CacheServiceAccessor.Reset();
    }

    [Fact]
    public void IsConfigured_WhenNotConfigured_ShouldReturnFalse()
    {
        // Assert
        CacheServiceAccessor.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void IsConfigured_WhenConfigured_ShouldReturnTrue()
    {
        // Arrange
        var cacheProvider = Substitute.For<ICacheProvider>();
        var keyGenerator = Substitute.For<ICacheKeyGenerator>();
        var invalidator = Substitute.For<ICacheInvalidator>();

        // Act
        CacheServiceAccessor.Configure(cacheProvider, keyGenerator, invalidator);

        // Assert
        CacheServiceAccessor.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void Configure_WithServices_ShouldSetProperties()
    {
        // Arrange
        var cacheProvider = Substitute.For<ICacheProvider>();
        var keyGenerator = Substitute.For<ICacheKeyGenerator>();
        var invalidator = Substitute.For<ICacheInvalidator>();

        // Act
        CacheServiceAccessor.Configure(cacheProvider, keyGenerator, invalidator);

        // Assert
        CacheServiceAccessor.CacheProvider.Should().Be(cacheProvider);
        CacheServiceAccessor.KeyGenerator.Should().Be(keyGenerator);
        CacheServiceAccessor.Invalidator.Should().Be(invalidator);
    }

    [Fact]
    public void Configure_WithServiceProvider_ShouldResolveServices()
    {
        // Arrange
        var cacheProvider = Substitute.For<ICacheProvider>();
        var keyGenerator = Substitute.For<ICacheKeyGenerator>();
        var invalidator = Substitute.For<ICacheInvalidator>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ICacheProvider)).Returns(cacheProvider);
        serviceProvider.GetService(typeof(ICacheKeyGenerator)).Returns(keyGenerator);
        serviceProvider.GetService(typeof(ICacheInvalidator)).Returns(invalidator);

        // Act
        CacheServiceAccessor.Configure(serviceProvider);

        // Assert
        CacheServiceAccessor.IsConfigured.Should().BeTrue();
        CacheServiceAccessor.CacheProvider.Should().Be(cacheProvider);
        CacheServiceAccessor.KeyGenerator.Should().Be(keyGenerator);
        CacheServiceAccessor.Invalidator.Should().Be(invalidator);
    }

    [Fact]
    public void Reset_ShouldClearConfiguration()
    {
        // Arrange
        var cacheProvider = Substitute.For<ICacheProvider>();
        var keyGenerator = Substitute.For<ICacheKeyGenerator>();
        var invalidator = Substitute.For<ICacheInvalidator>();
        CacheServiceAccessor.Configure(cacheProvider, keyGenerator, invalidator);

        // Act
        CacheServiceAccessor.Reset();

        // Assert
        CacheServiceAccessor.IsConfigured.Should().BeFalse();
        CacheServiceAccessor.CacheProvider.Should().BeNull();
        CacheServiceAccessor.KeyGenerator.Should().BeNull();
        CacheServiceAccessor.Invalidator.Should().BeNull();
    }

    [Fact]
    public void IsConfigured_WhenPartiallyConfigured_ShouldReturnFalse()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ICacheProvider)).Returns(Substitute.For<ICacheProvider>());
        serviceProvider.GetService(typeof(ICacheKeyGenerator)).Returns(null);
        serviceProvider.GetService(typeof(ICacheInvalidator)).Returns(null);

        // Act
        CacheServiceAccessor.Configure(serviceProvider);

        // Assert
        CacheServiceAccessor.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void GetContextKey_WhenNoScopeFactory_ShouldReturnNull()
    {
        // Arrange - configure without IServiceScopeFactory
        var cacheProvider = Substitute.For<ICacheProvider>();
        var keyGenerator = Substitute.For<ICacheKeyGenerator>();
        var invalidator = Substitute.For<ICacheInvalidator>();
        CacheServiceAccessor.Configure(cacheProvider, keyGenerator, invalidator);

        // Act
        var result = CacheServiceAccessor.GetContextKey();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetContextKey_WithScopeFactory_ShouldResolveContextProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICacheContextProvider>(_ => new TestContextProvider("tenant-x"));
        services.AddSingleton(Substitute.For<ICacheProvider>());
        services.AddSingleton(Substitute.For<ICacheKeyGenerator>());
        services.AddSingleton(Substitute.For<ICacheInvalidator>());
        var sp = services.BuildServiceProvider();

        CacheServiceAccessor.Configure(sp);

        // Act
        var result = CacheServiceAccessor.GetContextKey();

        // Assert
        result.Should().Be("tenant-x");
    }

    [Fact]
    public void GetContextKey_WithScopeFactory_NoContextProvider_ShouldReturnNull()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        // No ICacheContextProvider registered
        services.AddSingleton(Substitute.For<ICacheProvider>());
        services.AddSingleton(Substitute.For<ICacheKeyGenerator>());
        services.AddSingleton(Substitute.For<ICacheInvalidator>());
        var sp = services.BuildServiceProvider();

        CacheServiceAccessor.Configure(sp);

        // Act
        var result = CacheServiceAccessor.GetContextKey();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Configure_WithProviderFactory_ShouldSetProviderFactory()
    {
        // Arrange
        var cacheProvider = Substitute.For<ICacheProvider>();
        var keyGenerator = Substitute.For<ICacheKeyGenerator>();
        var invalidator = Substitute.For<ICacheInvalidator>();
        var providerFactory = Substitute.For<ICacheProviderFactory>();

        // Act
        CacheServiceAccessor.Configure(cacheProvider, keyGenerator, invalidator, providerFactory);

        // Assert
        CacheServiceAccessor.ProviderFactory.Should().Be(providerFactory);
    }

    private class TestContextProvider : ICacheContextProvider
    {
        private readonly string? _key;

        public TestContextProvider(string? key)
        {
            _key = key;
        }

        public string? GetContextKey()
        {
            return _key;
        }
    }
}
