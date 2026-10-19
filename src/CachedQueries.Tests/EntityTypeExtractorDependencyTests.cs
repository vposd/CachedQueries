using CachedQueries.Internal;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CachedQueries.Tests;

public class EntityTypeExtractorDependencyTests : IDisposable
{
    private readonly TestDbContext _context = new(new DbContextOptionsBuilder<TestDbContext>()
        .UseSqlite("DataSource=:memory:").Options);

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void Join_ShouldIncludeJoinedEntityType()
    {
        var query = _context.Orders.Join(_context.Customers, o => o.Name, c => c.Name, (o, c) => o);

        EntityTypeExtractor.ExtractEntityTypes(query).Should().Contain([typeof(Order), typeof(Customer)]);
    }

    [Fact]
    public void SubqueryOnDbSet_ShouldIncludeSubqueryEntityType()
    {
        var query = _context.Orders.Where(o => _context.Customers.Any(c => c.Name == o.Name));

        EntityTypeExtractor.ExtractEntityTypes(query).Should().Contain([typeof(Order), typeof(Customer)]);
    }

    [Fact]
    public void CapturedQueryable_ShouldIncludeItsDependencies()
    {
        var withItems = _context.Customers.Where(c => _context.OrderItems.Any(i => i.ProductName == c.Name))
            .Select(c => c.Name);
        var query = _context.Orders.Where(o => withItems.Contains(o.Name));

        EntityTypeExtractor.ExtractEntityTypes(query)
            .Should().Contain([typeof(Order), typeof(Customer), typeof(OrderItem)]);
    }

    [Fact]
    public void CapturedNullQueryable_ShouldNotThrow()
    {
        IQueryable<Customer>? none = null;
        var query = _context.Orders.Where(o => none!.Any(c => c.Name == o.Name));

        EntityTypeExtractor.ExtractEntityTypes(query).Should().Contain(typeof(Customer));
    }

    [Fact]
    public void Projection_ShouldIncludeNavigationTypes()
    {
        var query = _context.OrderItems.Select(i => new { i.Order.Name });

        EntityTypeExtractor.ExtractEntityTypes(query).Should().Contain([typeof(OrderItem), typeof(Order)]);
    }

    [Fact]
    public void LinqToObjectsSource_ShouldIncludeElementType()
    {
        var query = new List<Order>().AsQueryable().Where(o => o.Id > 0);

        EntityTypeExtractor.ExtractEntityTypes(query).Should().Contain(typeof(Order));
    }

    [Fact]
    public void ScalarQueryableConstant_ShouldNotAddPrimitiveType()
    {
        var query = new List<int> { 1 }.AsQueryable().Select(i => new GlobalNamespaceEntity());

        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        types.Should().NotContain(typeof(int));
        types.Should().Contain(typeof(GlobalNamespaceEntity));
    }

    [Theory]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(Uri), false)]
    [InlineData(typeof(DbContext), false)]
    [InlineData(typeof(Dictionary<string, object>), false)]
    [InlineData(typeof(Order), true)]
    [InlineData(typeof(GlobalNamespaceEntity), true)]
    public void IsEntityType_ShouldClassifyTypes(Type type, bool expected)
    {
        EntityTypeExtractor.IsEntityType(type).Should().Be(expected);
    }

    [Theory]
    [InlineData(typeof(string), null)]
    [InlineData(typeof(Order), null)]
    [InlineData(typeof(IEnumerable<Order>), typeof(Order))]
    [InlineData(typeof(HashSet<Order>), typeof(Order))]
    [InlineData(typeof(IReadOnlyCollection<Customer>), typeof(Customer))]
    public void GetSequenceElementType_ShouldResolveElementTypes(Type type, Type? expected)
    {
        EntityTypeExtractor.GetSequenceElementType(type).Should().Be(expected);
    }
}
