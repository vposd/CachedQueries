using CachedQueries.Abstractions;
using CachedQueries.Extensions;
using CachedQueries.Interceptors;
using CachedQueries.Providers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CachedQueries.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCachedQueries_ShouldRegisterAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddCachedQueries();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<ICacheProvider>().Should().NotBeNull();
        provider.GetService<ICacheProvider>().Should().BeOfType<MemoryCacheProvider>();
        provider.GetService<ICacheKeyGenerator>().Should().NotBeNull();
        provider.GetService<ICacheInvalidator>().Should().NotBeNull();
        provider.GetService<CacheInvalidationInterceptor>().Should().NotBeNull();
        provider.GetService<TransactionCacheInvalidationInterceptor>().Should().NotBeNull();
        provider.GetService<CachedQueriesConfiguration>().Should().NotBeNull();
    }

    [Fact]
    public void AddCachedQueries_WithConfiguration_ShouldApplyConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCachedQueries(config =>
        {
            config.AutoInvalidation = false;
            config.EnableLogging = false;
            config.DefaultOptions = new CachingOptions(TimeSpan.FromHours(2));
        });
        var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<CachedQueriesConfiguration>();

        // Assert
        config.AutoInvalidation.Should().BeFalse();
        config.EnableLogging.Should().BeFalse();
        config.DefaultOptions.Expiration.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void AddCachedQueries_Generic_ShouldRegisterCustomProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddCachedQueries<TestCacheProvider>();
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<ICacheProvider>().Should().BeOfType<TestCacheProvider>();
    }

    [Fact]
    public void UseCachedQueries_ShouldConfigureAccessor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCachedQueries();
        var provider = services.BuildServiceProvider();

        // Reset to ensure clean state
        CacheServiceAccessor.Reset();
        CacheServiceAccessor.IsConfigured.Should().BeFalse();

        // Act
        provider.UseCachedQueries();

        // Assert
        CacheServiceAccessor.IsConfigured.Should().BeTrue();
        CacheServiceAccessor.CacheProvider.Should().NotBeNull();
        CacheServiceAccessor.KeyGenerator.Should().NotBeNull();
        CacheServiceAccessor.Invalidator.Should().NotBeNull();

        // Cleanup
        CacheServiceAccessor.Reset();
    }

    [Fact]
    public void AddCachedQueries_WithFluentProviderConfig_ShouldRegisterProviderFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TestCacheProvider>();
        services.AddSingleton<AnotherTestCacheProvider>();

        // Act
        services.AddCachedQueries(config => config
            .UseSingleItemProvider<TestCacheProvider>()
            .UseCollectionProvider<AnotherTestCacheProvider>());

        var provider = services.BuildServiceProvider();

        // Assert
        var factory = provider.GetService<ICacheProviderFactory>();
        factory.Should().NotBeNull();

        var singleProvider = factory!.GetProvider(CacheTarget.Single);
        singleProvider.Should().BeOfType<TestCacheProvider>();

        var collectionProvider = factory.GetProvider(CacheTarget.Collection);
        collectionProvider.Should().BeOfType<AnotherTestCacheProvider>();
    }

    [Fact]
    public void AddCachedQueries_WithUseProvider_ShouldSetAllTargets()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TestCacheProvider>();

        // Act
        services.AddCachedQueries(config => config.UseProvider<TestCacheProvider>());
        var provider = services.BuildServiceProvider();

        // Assert
        var factory = provider.GetRequiredService<ICacheProviderFactory>();

        factory.GetProvider(CacheTarget.Single).Should().BeOfType<TestCacheProvider>();
        factory.GetProvider(CacheTarget.Collection).Should().BeOfType<TestCacheProvider>();
        factory.GetProvider(CacheTarget.Scalar).Should().BeOfType<TestCacheProvider>();
    }

    [Fact]
    public void AddCachedQueries_WithCustomProviderFactory_ShouldUseIt()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var customProvider = new TestCacheProvider();

        // Act
        services.AddCachedQueries(config =>
        {
            config.ProviderFactory = sp => new TestProviderFactory(customProvider);
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICacheProviderFactory>();

        // Assert
        factory.Should().BeOfType<TestProviderFactory>();
        factory.GetProvider(CacheTarget.Single).Should().BeSameAs(customProvider);
    }

    [Fact]
    public void AddCachedQueries_WithScalarProvider_ShouldRegister()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TestCacheProvider>();

        // Act
        services.AddCachedQueries(config => config.UseScalarProvider<TestCacheProvider>());
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ICacheProviderFactory>();

        // Assert
        factory.GetProvider(CacheTarget.Scalar).Should().BeOfType<TestCacheProvider>();
    }

    [Fact]
    public void AddCachedQueries_WithConfiguration_ShouldApplyDefaultOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddCachedQueries(config => { config.DefaultOptions = new CachingOptions(TimeSpan.FromHours(2)); });
        var provider = services.BuildServiceProvider();
        var config = provider.GetRequiredService<CachedQueriesConfiguration>();

        // Assert
        config.DefaultOptions.Expiration.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void AddCacheInvalidation_WithFactory_ShouldDecorateDbContextOptions()
    {
        // Arrange - AddDbContext registers via factory
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCachedQueries();
        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase("AddCacheInvalidation_Factory"));

        // Act
        services.AddCacheInvalidation<TestDbContext>();
        var provider = services.BuildServiceProvider();

        // Assert
        var context = provider.GetRequiredService<TestDbContext>();
        context.Should().NotBeNull();
    }

    [Fact]
    public void AddCacheInvalidation_WithInstance_ShouldDecorateDbContextOptions()
    {
        // Arrange - register with ImplementationInstance
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCachedQueries();

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("AddCacheInvalidation_Instance");
        services.AddSingleton(optionsBuilder.Options);

        // Act
        services.AddCacheInvalidation<TestDbContext>();
        var provider = services.BuildServiceProvider();

        // Assert
        var options = provider.GetRequiredService<DbContextOptions<TestDbContext>>();
        options.Should().NotBeNull();
    }

    [Fact]
    public void AddCacheInvalidation_WhenNoDbContextRegistered_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCachedQueries();

        // Act - no DbContext<TestDbContext> registered
        services.AddCacheInvalidation<TestDbContext>();
        var provider = services.BuildServiceProvider();

        // Assert - should not throw during registration
        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddCacheContextProvider_Generic_ShouldRegisterScopedProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCacheContextProvider<TestContextProvider>();
        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var contextProvider = scope.ServiceProvider.GetService<ICacheContextProvider>();
        contextProvider.Should().NotBeNull();
        contextProvider.Should().BeOfType<TestContextProvider>();
    }

    [Fact]
    public void AddCacheContextProvider_Factory_ShouldRegisterScopedProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCacheContextProvider(_ => new TestContextProvider());
        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var contextProvider = scope.ServiceProvider.GetService<ICacheContextProvider>();
        contextProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddCachedQueries_WithContextProvider_ShouldRegisterContextProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddCachedQueries(config => config.UseContextProvider<TestContextProvider>());
        var provider = services.BuildServiceProvider();

        // Assert
        using var scope = provider.CreateScope();
        var contextProvider = scope.ServiceProvider.GetService<ICacheContextProvider>();
        contextProvider.Should().NotBeNull();
        contextProvider.Should().BeOfType<TestContextProvider>();
    }

    [Fact]
    public void AddCachedQueries_Generic_WithConfiguration_ShouldApply()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddCachedQueries<TestCacheProvider>(config =>
        {
            config.DefaultOptions = new CachingOptions(TimeSpan.FromHours(3));
        });
        var provider = services.BuildServiceProvider();

        // Assert
        provider.GetService<ICacheProvider>().Should().BeOfType<TestCacheProvider>();
        provider.GetRequiredService<CachedQueriesConfiguration>().DefaultOptions.Expiration
            .Should().Be(TimeSpan.FromHours(3));
    }

    private class TestCacheProvider : ICacheProvider
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(default(T));
        }

        public Task SetAsync<T>(string key, T value, CachingOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class AnotherTestCacheProvider : ICacheProvider
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(default(T));
        }

        public Task SetAsync<T>(string key, T value, CachingOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private class TestContextProvider : ICacheContextProvider
    {
        public string? GetContextKey()
        {
            return "test-context";
        }
    }

    private class TestProviderFactory : ICacheProviderFactory
    {
        private readonly ICacheProvider _provider;

        public TestProviderFactory(ICacheProvider provider)
        {
            _provider = provider;
        }

        public ICacheProvider GetProvider(CacheTarget target)
        {
            return _provider;
        }

        public IEnumerable<ICacheProvider> GetAllProviders()
        {
            return [_provider];
        }
    }
}
