using AutoFixture;
using CachedQueries.EntityFramework.Extensions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CachedQueries.Test.EntityFramework;

public sealed class ReflectExtensionsTest
{
    private readonly Mock<Func<TestDbContext>> _contextFactoryMock;
    private readonly Fixture _fixture;

    public ReflectExtensionsTest()
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
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
    }

    [Fact]
    public async Task GetIncludeTypes_Should_Return_Include_Types()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(20).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        var queryLinq = context.Orders
            .Include(x => x.Products)
            .Include(x => x.Customer)
            .Where(x => x.Id > 0);

        var querySyntax = from order in context.Orders
            join customer in context.Customers on order.CustomerId equals customer.Id
            join product in context.Products on order.Id equals product.Id
            where order.Id > 0
            select new
            {
                order, customer
            };

        var typesFromLinq = queryLinq.GetIncludeTypes().ToList();
        var typesFromQuerySyntax = querySyntax.GetIncludeTypes().ToList();

        // Then
        typesFromLinq.Should().HaveCount(3);
        typesFromLinq.Should().Contain(typeof(Order));
        typesFromLinq.Should().Contain(typeof(Customer));
        typesFromLinq.Should().Contain(typeof(Product));

        typesFromQuerySyntax.Should().HaveCount(3);
        typesFromQuerySyntax.Should().Contain(typeof(Order));
        typesFromQuerySyntax.Should().Contain(typeof(Customer));
        typesFromQuerySyntax.Should().Contain(typeof(Product));
    }

    [Fact]
    public async Task GetIncludeTypes_Should_Return_Include_And_ThenInclude_Types()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(20).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        var queryLinq = context.Orders
            .Include(x => x.Products)
            .ThenInclude(x => x.Attributes)
            .Include(x => x.Customer)
            .IgnoreQueryFilters()
            .Where(x => x.Id > 0);

        var querySyntax = from order in context.Orders
            join customer in context.Customers on order.CustomerId equals customer.Id
            join product in context.Products on order.Id equals product.Id
            join attribute in context.Attributes on product.Id equals attribute.ProductId
            where order.Id > 0
            select new
            {
                order, customer
            };

        var typesFromLinq = queryLinq.GetIncludeTypes().ToList();
        var typesFromQuerySyntax = querySyntax.GetIncludeTypes().ToList();

        // Then
        typesFromLinq.Should().HaveCount(4);
        typesFromLinq.Should().Contain(typeof(Order));
        typesFromLinq.Should().Contain(typeof(Customer));
        typesFromLinq.Should().Contain(typeof(Attribute));
        typesFromLinq.Should().Contain(typeof(Product));

        typesFromQuerySyntax.Should().HaveCount(4);
        typesFromQuerySyntax.Should().Contain(typeof(Order));
        typesFromQuerySyntax.Should().Contain(typeof(Customer));
        typesFromQuerySyntax.Should().Contain(typeof(Attribute));
        typesFromQuerySyntax.Should().Contain(typeof(Product));
    }

    [Fact]
    public async Task GetIncludeTypes_Should_Return_Include_And_ThenInclude_Types_With_Filters()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(20).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        var query = context.Orders
            .Include(x => x.Products.Where(p => !string.IsNullOrEmpty(p.Name)))
            .ThenInclude(x => x.Attributes.Where(p => !string.IsNullOrEmpty(p.Text)))
            .Include(x => x.Customer)
            .IgnoreQueryFilters()
            .Where(x => x.Id > 0)
            .Select(x => x.Number);

        var types = query.GetIncludeTypes().ToList();

        // Then
        types.Should().HaveCount(4);
        types.Should().Contain(typeof(Order));
        types.Should().Contain(typeof(Customer));
        types.Should().Contain(typeof(Attribute));
        types.Should().Contain(typeof(Product));
    }

    [Fact]
    public async Task GetIncludeTypes_Should_Return_Root_Type_If_Include_Not_In_Use()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(20).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        var types = context.Orders.GetIncludeTypes().ToList();

        // Then
        types.Should().HaveCount(1);
        types.Should().Contain(typeof(Order));
    }

    [Fact]
    public async Task
        GetIncludeTypes_Should_Return_Root_Type_If_Include_Not_In_Use_And_There_Are_Other_Expression_Defined()
    {
        // Given
        await using var context = _contextFactoryMock.Object();
        var entities = _fixture.CreateMany<Order>(20).ToList();
        context.Orders.AddRange(entities);
        await context.SaveChangesAsync();

        // When
        var types = context.Orders.Where(x => x.Id > 0).Select(x => x.Number).GetIncludeTypes().ToList();

        // Then
        types.Should().HaveCount(1);
        types.Should().Contain(typeof(Order));
    }
}
