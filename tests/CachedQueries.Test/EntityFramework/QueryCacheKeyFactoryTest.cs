using AutoFixture;
using CachedQueries.Core.Abstractions;
using CachedQueries.EntityFramework;
using FluentAssertions;
using Moq;
using Xunit;

namespace CachedQueries.Test.EntityFramework;

public class QueryCacheKeyFactoryTest
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void Should_Generate_Key()
    {
        // Given
        var cacheContext = new Mock<ICacheContextProvider>();

        var keyFactory = new QueryCacheKeyFactory(cacheContext.Object);
        var list = _fixture.CreateMany<Order>(10);
        var query = list.Where(x => x.Id > 0).AsQueryable();

        // When
        var result = keyFactory.GetCacheKey(query, ["tag_1"]);

        // Then
        result.Should().Be("E26EBC3F26C17A74C6298DB3F5B730536577B9D8329969C28AA6913B14227585");
    }

    [Fact]
    public void Should_Generate_Key_For_Non_Class_Queries()
    {
        // Given
        var cacheContext = new Mock<ICacheContextProvider>();

        var keyFactory = new QueryCacheKeyFactory(cacheContext.Object);
        var list = _fixture.CreateMany<Order>(10);
        var query = list.Where(x => x.Id > 0).Select(x => x.Id + 100).AsQueryable();

        // When
        var result = keyFactory.GetCacheKey(query, ["tag_1"]);

        // Then
        result.Should().Be("D9F9BBA43AA7E37941444BE9E46E6D633F0CFFB9B8370054757E5196065118F5");
    }
}
