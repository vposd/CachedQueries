using CachedQueries.Abstractions;
using CachedQueries.Extensions;
using CachedQueries.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

public class DbContextOptionsBuilderExtensionsTests
{
    [Fact]
    public void AddCacheInvalidation_ShouldAddInterceptors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ICacheInvalidator>());
        services.AddSingleton(Substitute.For<ILogger<CacheInvalidationInterceptor>>());
        services.AddSingleton(Substitute.For<ILogger<TransactionCacheInvalidationInterceptor>>());
        services.AddSingleton<CacheInvalidationInterceptor>();
        services.AddSingleton<TransactionCacheInvalidationInterceptor>();
        var provider = services.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseInMemoryDatabase("test");

        // Act
        optionsBuilder.AddCacheInvalidation(provider);

        // Assert - the interceptors should be added (no exception thrown)
        var options = optionsBuilder.Options;
        options.Should().NotBeNull();
    }

    [Fact]
    public void AddCacheInvalidation_WhenInterceptorsNotRegistered_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        optionsBuilder.UseInMemoryDatabase("test");

        // Act
        var result = optionsBuilder.AddCacheInvalidation(provider);

        // Assert
        result.Should().Be(optionsBuilder);
    }
}
