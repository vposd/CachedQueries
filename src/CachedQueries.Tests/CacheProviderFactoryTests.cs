using CachedQueries.Abstractions;
using CachedQueries.Internal;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

public class CacheProviderFactoryTests
{
    private readonly ICacheProvider _collectionProvider;
    private readonly ICacheProvider _defaultProvider;
    private readonly ICacheProvider _scalarProvider;
    private readonly ICacheProvider _singleProvider;

    public CacheProviderFactoryTests()
    {
        _defaultProvider = Substitute.For<ICacheProvider>();
        _singleProvider = Substitute.For<ICacheProvider>();
        _collectionProvider = Substitute.For<ICacheProvider>();
        _scalarProvider = Substitute.For<ICacheProvider>();
    }

    [Fact]
    public void GetProvider_WithSingleTarget_ShouldReturnSingleProvider()
    {
        // Arrange
        var factory = new CacheProviderFactory(_defaultProvider, _singleProvider);

        // Act
        var provider = factory.GetProvider(CacheTarget.Single);

        // Assert
        provider.Should().BeSameAs(_singleProvider);
    }

    [Fact]
    public void GetProvider_WithCollectionTarget_ShouldReturnCollectionProvider()
    {
        // Arrange
        var factory = new CacheProviderFactory(_defaultProvider, null, _collectionProvider);

        // Act
        var provider = factory.GetProvider(CacheTarget.Collection);

        // Assert
        provider.Should().BeSameAs(_collectionProvider);
    }

    [Fact]
    public void GetProvider_WithScalarTarget_ShouldReturnScalarProvider()
    {
        // Arrange
        var factory = new CacheProviderFactory(_defaultProvider, null, null, _scalarProvider);

        // Act
        var provider = factory.GetProvider(CacheTarget.Scalar);

        // Assert
        provider.Should().BeSameAs(_scalarProvider);
    }

    [Fact]
    public void GetProvider_WithAutoTarget_ShouldReturnDefaultProvider()
    {
        // Arrange
        var factory = new CacheProviderFactory(_defaultProvider, _singleProvider, _collectionProvider, _scalarProvider);

        // Act
        var provider = factory.GetProvider(CacheTarget.Auto);

        // Assert
        provider.Should().BeSameAs(_defaultProvider);
    }

    [Fact]
    public void GetProvider_WithSingleTarget_WhenNoSingleProvider_ShouldReturnDefault()
    {
        // Arrange
        var factory = new CacheProviderFactory(_defaultProvider);

        // Act
        var provider = factory.GetProvider(CacheTarget.Single);

        // Assert
        provider.Should().BeSameAs(_defaultProvider);
    }

    [Fact]
    public void GetProvider_WithCollectionTarget_WhenNoCollectionProvider_ShouldReturnDefault()
    {
        // Arrange
        var factory = new CacheProviderFactory(_defaultProvider);

        // Act
        var provider = factory.GetProvider(CacheTarget.Collection);

        // Assert
        provider.Should().BeSameAs(_defaultProvider);
    }

    [Fact]
    public void GetProvider_WithScalarTarget_WhenNoScalarProvider_ShouldReturnDefault()
    {
        // Arrange
        var factory = new CacheProviderFactory(_defaultProvider);

        // Act
        var provider = factory.GetProvider(CacheTarget.Scalar);

        // Assert
        provider.Should().BeSameAs(_defaultProvider);
    }

    [Fact]
    public void GetAllProviders_WithOnlyDefault_ShouldReturnSingleProvider()
    {
        // Arrange
        var factory = new CacheProviderFactory(_defaultProvider);

        // Act
        var providers = factory.GetAllProviders().ToList();

        // Assert
        providers.Should().HaveCount(1);
        providers.Should().Contain(_defaultProvider);
    }

    [Fact]
    public void GetAllProviders_WithAllProviders_ShouldReturnUniqueProviders()
    {
        // Arrange
        var factory = new CacheProviderFactory(_defaultProvider, _singleProvider, _collectionProvider, _scalarProvider);

        // Act
        var providers = factory.GetAllProviders().ToList();

        // Assert
        providers.Should().HaveCount(4);
        providers.Should().Contain(_defaultProvider);
        providers.Should().Contain(_singleProvider);
        providers.Should().Contain(_collectionProvider);
        providers.Should().Contain(_scalarProvider);
    }

    [Fact]
    public void GetAllProviders_WithDuplicateProviders_ShouldReturnUniqueProviders()
    {
        // Arrange - same provider for all targets
        var factory = new CacheProviderFactory(_defaultProvider, _defaultProvider, _defaultProvider, _defaultProvider);

        // Act
        var providers = factory.GetAllProviders().ToList();

        // Assert
        providers.Should().HaveCount(1);
        providers.Should().Contain(_defaultProvider);
    }

    [Fact]
    public void GetAllProviders_WithSomeProviders_ShouldReturnOnlyRegistered()
    {
        // Arrange
        var factory = new CacheProviderFactory(_defaultProvider, _singleProvider);

        // Act
        var providers = factory.GetAllProviders().ToList();

        // Assert
        providers.Should().HaveCount(2);
        providers.Should().Contain(_defaultProvider);
        providers.Should().Contain(_singleProvider);
    }
}
