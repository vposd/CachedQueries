using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using FluentAssertions;
using Xunit;

namespace Lore.QueryCache.Manager.Tests;

class Entity
{
    public long Id { get; set; }
}

public class CacheFactoryDefaultTests
{
    private readonly Fixture _fixture;

    public CacheFactoryDefaultTests()
    {
        _fixture = new Fixture();
    }

    [Fact]
    public void Should_Generate_Key()
    {
        // Given
        var keyFactory = new CacheKeyFactory();
        var list = _fixture.CreateMany<Entity>(10);
        var query = list.Where(x => x.Id > 0).AsQueryable();

        // When
        var result = keyFactory.GetCacheKey(query, new List<string> { "tag_1" });

        // Then
        result.Should().Be("B3A85BEA996E885545D88807110B0FFCB7ADB0A929B082E1BB58864C639A4D3B");
    }
}