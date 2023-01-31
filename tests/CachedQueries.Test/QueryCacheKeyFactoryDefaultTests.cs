using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using CachedQueries.EntityFramework;
using CachedQueries.Test.Data;
using FluentAssertions;
using Xunit;

namespace CachedQueries.Test;

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
        result.Should().Be("85B835C82C72253EDA2CEC7C47ECCF4DF59EEA9C7379753A2121C33818A341E9");
    }
}
