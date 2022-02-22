using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using CachedQueries.EntityFramework.Tests.Data;
using FluentAssertions;
using Xunit;

namespace CachedQueries.EntityFramework.Tests;

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
        result.Should().Be("5A75650695F5D73333D7912E3EDF74F54414B8A52429C77D3D05D3997B7D32C9");
    }
}