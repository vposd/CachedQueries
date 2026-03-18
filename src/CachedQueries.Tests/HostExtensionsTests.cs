using CachedQueries.Abstractions;
using CachedQueries.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

[Collection("CacheServiceAccessor")]
public class HostExtensionsTests : IDisposable
{
    public HostExtensionsTests()
    {
        CacheServiceAccessor.Reset();
    }

    public void Dispose()
    {
        CacheServiceAccessor.Reset();
    }

    [Fact]
    public void UseCachedQueries_ShouldConfigureAccessor()
    {
        // Arrange - use AddCachedQueries to register all required services
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCachedQueries();

        var serviceProvider = services.BuildServiceProvider();

        var host = Substitute.For<IHost>();
        host.Services.Returns(serviceProvider);

        // Act
        var result = host.UseCachedQueries();

        // Assert
        result.Should().Be(host);
        CacheServiceAccessor.IsConfigured.Should().BeTrue();
    }
}


