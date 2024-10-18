using AutoFixture;
using CachedQueries.Core.Abstractions;
using CachedQueries.EntityFramework;
using CachedQueries.Test.TestHelpers;
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
        result.Should().Be("EF8D643A5660385756EBC4254D0C45BFE9BA8B5442E9E4A2BB1E465DEF5892E2");
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
        result.Should().Be("C8A85815307727503F8306926120D19E28C9754E1E15981FF4EE481410FC529C");
    }
}
