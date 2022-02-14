using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using FluentAssertions;
using Lore.QueryCache.EntityFramework.Tests.Data;
using Xunit;

namespace Lore.QueryCache.EntityFramework.Tests;

public class QueryCacheKeyFactoryDefaultTests
{
    private readonly Fixture _fixture;

    public QueryCacheKeyFactoryDefaultTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void Should_Generate_Key()
    {
        // Given
        var keyFactory = new QueryCacheKeyFactory();
        var list = _fixture.CreateMany<Blog>(10);
        var query = list.Where(x => x.Id > 0).AsQueryable();

        // When
        var result = keyFactory.GetCacheKey(query, new List<string> { "tag_1" });

        // Then
        result.Should().Be("B04F275F6619399119D790C245CE8CE39E5D4C4BF40908DCA1DB782E69663962");
    }
}