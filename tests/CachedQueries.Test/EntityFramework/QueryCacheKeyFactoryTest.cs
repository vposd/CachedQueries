using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using CachedQueries.EntityFramework;
using FluentAssertions;
using Xunit;

namespace CachedQueries.Test.EntityFramework;

public class QueryCacheKeyFactoryTest
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void Should_Generate_Key()
    {
        // Given
        var keyFactory = new QueryCacheKeyFactory();
        var list = _fixture.CreateMany<Order>(10);
        var query = list.Where(x => x.Id > 0).AsQueryable();

        // When
        var result = keyFactory.GetCacheKey(query, [ "tag_1" ]);

        // Then
        result.Should().Be("E26EBC3F26C17A74C6298DB3F5B730536577B9D8329969C28AA6913B14227585");
    }
}
