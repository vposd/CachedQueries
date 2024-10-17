using AutoFixture;
using CachedQueries.Core;
using CachedQueries.Core.Abstractions;
using FluentAssertions;
using Moq;
using Xunit;

namespace CachedQueries.Test.Core;

internal class Entity
{
    public long Id { get; set; }
}

public class DefaultCacheKeyFactoryTest
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void Should_Generate_Key()
    {
        // Given
        var cacheContext = new Mock<ICacheContextProvider>();

        var keyFactory = new DefaultCacheKeyFactory(cacheContext.Object);
        var list = _fixture.CreateMany<Entity>(10);
        var query = list.Where(x => x.Id > 0).AsQueryable();

        // When
        var result = keyFactory.GetCacheKey(query, ["tag_1"]);

        // Then
        result.Should().Be("B3A85BEA996E885545D88807110B0FFCB7ADB0A929B082E1BB58864C639A4D3B");
    }
}
