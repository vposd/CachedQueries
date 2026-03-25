using System.Linq.Expressions;
using CachedQueries.Internal;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CachedQueries.Tests;

public class QueryCacheKeyGeneratorTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly QueryCacheKeyGenerator _generator;

    public QueryCacheKeyGeneratorTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new TestDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _generator = new QueryCacheKeyGenerator();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public void GenerateKey_ForSameQuery_ShouldReturnSameKey()
    {
        // Arrange
        var query1 = _context.Orders.Where(o => o.Total > 100);
        var query2 = _context.Orders.Where(o => o.Total > 100);

        // Act
        var key1 = _generator.GenerateKey(query1);
        var key2 = _generator.GenerateKey(query2);

        // Assert
        key1.Should().Be(key2);
    }

    [Fact]
    public void GenerateKey_ForDifferentQueries_ShouldReturnDifferentKeys()
    {
        // Arrange
        var query1 = _context.Orders.Where(o => o.Total > 100);
        var query2 = _context.Orders.Where(o => o.Total > 200);

        // Act
        var key1 = _generator.GenerateKey(query1);
        var key2 = _generator.GenerateKey(query2);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateKey_WithPredicate_ShouldIncludePredicateInKey()
    {
        // Arrange
        var query = _context.Orders.AsQueryable();

        // Act
        var key1 = _generator.GenerateKey(query, o => o.Id == 1);
        var key2 = _generator.GenerateKey(query, o => o.Id == 2);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateKey_WithNullPredicate_ShouldWork()
    {
        // Arrange
        var query = _context.Orders.AsQueryable();

        // Act
        var key = _generator.GenerateKey(query, null);

        // Assert
        key.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateKey_ShouldReturnHashOnly()
    {
        // Arrange
        var query = _context.Orders;

        // Act
        var key = _generator.GenerateKey(query);

        // Assert — key generator returns just the hash, no prefix
        key.Should().NotBeNullOrEmpty();
        key.Should().NotContain(":");
    }

    [Fact]
    public void GenerateKey_WithInclude_ShouldProduceDifferentKey()
    {
        // Arrange
        var queryWithoutInclude = _context.Orders.AsQueryable();
        var queryWithInclude = _context.Orders.Include(o => o.Items);

        // Act
        var key1 = _generator.GenerateKey(queryWithoutInclude);
        var key2 = _generator.GenerateKey(queryWithInclude);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateKey_WithOrderBy_ShouldProduceDifferentKey()
    {
        // Arrange
        var queryAsc = _context.Orders.OrderBy(o => o.Total);
        var queryDesc = _context.Orders.OrderByDescending(o => o.Total);

        // Act
        var key1 = _generator.GenerateKey(queryAsc);
        var key2 = _generator.GenerateKey(queryDesc);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateKey_WithSkipAndTake_ShouldProduceDifferentKeys()
    {
        // Arrange
        var query1 = _context.Orders.Skip(0).Take(10);
        var query2 = _context.Orders.Skip(10).Take(10);

        // Act
        var key1 = _generator.GenerateKey(query1);
        var key2 = _generator.GenerateKey(query2);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateKey_ForDifferentEntityTypes_ShouldProduceDifferentKeys()
    {
        // Arrange
        var ordersQuery = _context.Orders.AsQueryable();
        var customersQuery = _context.Customers.AsQueryable();

        // Act
        var key1 = _generator.GenerateKey(ordersQuery);
        var key2 = _generator.GenerateKey(customersQuery);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateKey_WithSelect_ShouldProduceDifferentKey()
    {
        // Arrange
        var query1 = _context.Orders.Select(o => o.Name);
        var query2 = _context.Orders.Select(o => o.Total);

        // Act
        var key1 = _generator.GenerateKey(query1);
        var key2 = _generator.GenerateKey(query2);

        // Assert
        key1.Should().NotBe(key2);
    }
}

public class ExpressionStringVisitorTests
{
    [Fact]
    public void VisitBinary_ShouldProduceCorrectString()
    {
        // Arrange
        var visitor = new ExpressionStringVisitor();
        Expression<Func<int, bool>> expr = x => x > 5;

        // Act
        visitor.Visit(expr);
        var result = visitor.ToString();

        // Assert
        result.Should().Contain("GreaterThan");
    }

    [Fact]
    public void VisitLambda_ShouldIncludeParameterInfo()
    {
        // Arrange
        var visitor = new ExpressionStringVisitor();
        Expression<Func<int, int>> expr = x => x;

        // Act
        visitor.Visit(expr);
        var result = visitor.ToString();

        // Assert
        result.Should().Contain("λ(");
        result.Should().Contain("Int32");
        result.Should().Contain("=>");
    }

    [Fact]
    public void VisitBinary_WithAdd_ShouldProduceCorrectString()
    {
        // Arrange
        var visitor = new ExpressionStringVisitor();
        Expression<Func<int, int, int>> expr = (a, b) => a + b;

        // Act
        visitor.Visit(expr);
        var result = visitor.ToString();

        // Assert
        result.Should().Contain("Add");
    }

    [Fact]
    public void VisitMember_ShouldIncludeMemberName()
    {
        // Arrange
        var visitor = new ExpressionStringVisitor();
        Expression<Func<Order, string>> expr = o => o.Name;

        // Act
        visitor.Visit(expr);
        var result = visitor.ToString();

        // Assert
        result.Should().Contain(".Name");
    }

    [Fact]
    public void VisitMethodCall_ShouldIncludeMethodName()
    {
        // Arrange
        var visitor = new ExpressionStringVisitor();
        Expression<Func<string, string>> expr = s => s.ToUpper();

        // Act
        visitor.Visit(expr);
        var result = visitor.ToString();

        // Assert
        result.Should().Contain(".ToUpper(");
    }

    [Fact]
    public void VisitMethodCall_WithMultipleArguments_ShouldSeparateWithComma()
    {
        // Arrange
        var visitor = new ExpressionStringVisitor();
        Expression<Func<string, string>> expr = s => s.Replace("a", "b");

        // Act
        visitor.Visit(expr);
        var result = visitor.ToString();

        // Assert
        result.Should().Contain(".Replace(");
        result.Should().Contain(",");
    }

    [Fact]
    public void VisitLambda_WithMultipleParameters_ShouldIncludeAll()
    {
        // Arrange
        var visitor = new ExpressionStringVisitor();
        Expression<Func<int, string, bool>> expr = (x, s) => true;

        // Act
        visitor.Visit(expr);
        var result = visitor.ToString();

        // Assert
        result.Should().Contain("Int32");
        result.Should().Contain("String");
    }
}
