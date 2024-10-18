using AutoFixture;
using CachedQueries.EntityFramework.Extensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CachedQueries.Test.EntityFramework;

public class QueryableExtensionsTest
{
    private readonly Mock<Func<TestDbContext>> _contextFactoryMock;
    private readonly Fixture _fixture;

    public QueryableExtensionsTest()
    {
        _fixture = new Fixture();
        _contextFactoryMock = new Mock<Func<TestDbContext>>();
        _contextFactoryMock.Setup(x => x()).Returns(() =>
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(_fixture.Create<string>())
                .Options;
            var context = new TestDbContext(options);
            return context;
        });

        var services = new ServiceCollection();

        services.AddMemoryCache();
    }

    [Fact]
    public async Task RetrieveRawInvalidationTagsFromQuery_ShouldReturnTags_WhenIncludeTypesArePresent()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var query = context.Orders
            .Include(x => x.Products)
            .Include(x => x.Customer);

        // When
        var tags = query.RetrieveRawInvalidationTagsFromQuery();

        // Then
        tags.Should()
            .BeEquivalentTo("CachedQueries.Test.Order", "CachedQueries.Test.Product", "CachedQueries.Test.Customer");
    }
}
