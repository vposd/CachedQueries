using AutoFixture;
using CachedQueries.EntityFramework.Extensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CachedQueries.Test.EntityFramework;

public class ChangeTrackerExtensionsTest
{
    private readonly Mock<Func<TestDbContext>> _contextFactoryMock;
    private readonly Fixture _fixture;

    public ChangeTrackerExtensionsTest()
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
    public async Task GetAffectedReferences_Should_Return_Types()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(20).ToList();
        context.Orders.AddRange(entities);

        // When
        var (affectedTypes, affectedTags) = context.ChangeTracker.GetAffectedReferences();

        // Then
        affectedTypes.Should().HaveCount(affectedTags.Length);
        affectedTypes.Should().Contain(typeof(Order));
        affectedTypes.Should().Contain(typeof(Customer));
        affectedTypes.Should().Contain(typeof(Product));
        affectedTypes.Should().Contain(typeof(Attribute));

        affectedTags.Should().Contain("CachedQueries.Test.Order");
        affectedTags.Should().Contain("CachedQueries.Test.Customer");
        affectedTags.Should().Contain("CachedQueries.Test.Product");
        affectedTags.Should().Contain("CachedQueries.Test.Attribute");
    }
}
