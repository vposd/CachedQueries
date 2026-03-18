using CachedQueries.Internal;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CachedQueries.Tests;

public class EntityTypeExtractorTests : IDisposable
{
    private readonly TestDbContext _context;

    public EntityTypeExtractorTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new TestDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public void ExtractEntityTypes_FromSimpleQuery_ShouldReturnEntityType()
    {
        // Arrange
        var query = _context.Orders.AsQueryable();

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Order));
    }

    [Fact]
    public void ExtractEntityTypes_WithInclude_ShouldReturnBothTypes()
    {
        // Arrange
        var query = _context.Orders.Include(o => o.Items);

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Order));
        types.Should().Contain(typeof(OrderItem));
    }

    [Fact]
    public void ExtractEntityTypes_FromDifferentEntity_ShouldReturnCorrectType()
    {
        // Arrange
        var query = _context.Customers.AsQueryable();

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Customer));
        types.Should().NotContain(typeof(Order));
    }

    [Fact]
    public void ExtractEntityTypes_WithWhere_ShouldStillReturnEntityType()
    {
        // Arrange
        var query = _context.Orders.Where(o => o.Total > 100);

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Order));
    }

    [Fact]
    public void ExtractEntityTypes_WithOrderBy_ShouldReturnEntityType()
    {
        // Arrange
        var query = _context.Orders.OrderBy(o => o.Name);

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Order));
    }

    [Fact]
    public void ExtractEntityTypes_WithSelectToSameType_ShouldReturnEntityType()
    {
        // Arrange - Select that keeps the same entity type
        var query = _context.Orders.Where(o => o.Total > 0).Select(o => o);

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Order));
    }

    [Fact]
    public void ExtractEntityTypes_WithTake_ShouldReturnEntityType()
    {
        // Arrange
        var query = _context.Orders.Take(10);

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Order));
    }

    [Fact]
    public void ExtractEntityTypes_WithSkip_ShouldReturnEntityType()
    {
        // Arrange
        var query = _context.Orders.Skip(5);

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Order));
    }

    [Fact]
    public void ExtractEntityTypes_WithThenInclude_ShouldReturnAllTypes()
    {
        // Arrange
        var query = _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Order);

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Order));
        types.Should().Contain(typeof(OrderItem));
    }

    [Fact]
    public void ExtractEntityTypes_WithDistinct_ShouldReturnEntityType()
    {
        // Arrange
        var query = _context.Orders.Distinct();

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Order));
    }

    [Fact]
    public void ExtractEntityTypes_WithComplexQuery_ShouldReturnAllTypes()
    {
        // Arrange
        var query = _context.Orders
            .Where(o => o.Total > 100)
            .Include(o => o.Items)
            .OrderBy(o => o.Name)
            .Take(10);

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Order));
        types.Should().Contain(typeof(OrderItem));
    }

    [Fact]
    public void ExtractEntityTypes_WithAsNoTracking_ShouldReturnEntityType()
    {
        // Arrange
        var query = _context.Orders.AsNoTracking();

        // Act
        var types = EntityTypeExtractor.ExtractEntityTypes(query);

        // Assert
        types.Should().Contain(typeof(Order));
    }
}
