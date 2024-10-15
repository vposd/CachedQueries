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
        var list = _fixture.CreateMany<Blog>(10);
        var query = list.Where(x => x.Id > 0).AsQueryable();

        // When
        var result = keyFactory.GetCacheKey(query, [ "tag_1" ]);

        // Then
        result.Should().Be("90CC2CA3A7D25B7DF0B99BB1B3A0BF72E630BC8BC6B11A922B2A55F02E37849E");
    }
}
