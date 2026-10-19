using System.Linq.Expressions;
using CachedQueries.Internal;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CachedQueries.Tests;

public class QueryCacheKeyGeneratorCapturedValuesTests : IDisposable
{
    private static readonly int StaticThreshold = 7;
    private readonly QueryCacheKeyGenerator _generator = new();
    private readonly TestDbContext _inMemory;
    private readonly TestDbContext _sqlite;

    public QueryCacheKeyGeneratorCapturedValuesTests()
    {
        _inMemory = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _sqlite = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:").Options);
    }

    public void Dispose()
    {
        _inMemory.Dispose();
        _sqlite.Dispose();
    }

    private static bool ClientSide(Order o)
    {
        return o.Id > 0;
    }

    [Fact]
    public void InMemory_CapturedVariables_ShouldProduceDifferentKeys()
    {
        // Regression: ToQueryString() returns a constant message on non-relational providers,
        // which used to make every query share one cache key.
        int id1 = 1, id2 = 2;

        var key1 = _generator.GenerateKey(_inMemory.Orders.Where(o => o.Id == id1));
        var key2 = _generator.GenerateKey(_inMemory.Orders.Where(o => o.Id == id2));

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void InMemory_SameCapturedValue_ShouldProduceSameKey()
    {
        var a = 5;
        var b = 5;

        _generator.GenerateKey(_inMemory.Orders.Where(o => o.Id == a))
            .Should().Be(_generator.GenerateKey(_inMemory.Orders.Where(o => o.Id == b)));
    }

    [Fact]
    public void InMemory_DifferentEntitySets_ShouldProduceDifferentKeys()
    {
        _generator.GenerateKey(_inMemory.Orders.Where(o => o.Id == 5))
            .Should().NotBe(_generator.GenerateKey(_inMemory.Customers.Where(c => c.Id == 5)));
    }

    [Fact]
    public void InMemory_CapturedCollections_ShouldProduceDifferentKeys()
    {
        var ids1 = new List<int> { 1, 2 };
        var ids2 = new List<int> { 1, 3 };

        _generator.GenerateKey(_inMemory.Orders.Where(o => ids1.Contains(o.Id)))
            .Should().NotBe(_generator.GenerateKey(_inMemory.Orders.Where(o => ids2.Contains(o.Id))));
    }

    [Fact]
    public void InMemory_NestedCapturedProperty_ShouldBeEvaluated()
    {
        var filter1 = new { Name = "a" };
        var filter2 = new { Name = "b" };

        _generator.GenerateKey(_inMemory.Orders.Where(o => o.Name == filter1.Name))
            .Should().NotBe(_generator.GenerateKey(_inMemory.Orders.Where(o => o.Name == filter2.Name)));
    }

    [Fact]
    public void InMemory_InstanceMethodOnDifferentMembers_ShouldProduceDifferentKeys()
    {
        // Regression: the method call target was not rendered, so these used to collide.
        _generator.GenerateKey(_inMemory.Customers.Where(c => c.Name.ToUpper() == "X"))
            .Should().NotBe(_generator.GenerateKey(_inMemory.Customers.Where(c => c.Email.ToUpper() == "X")));
    }

    [Fact]
    public void InMemory_GenericMethodArguments_ShouldProduceDifferentKeys()
    {
        _generator.GenerateKey(_inMemory.Orders.Select(o => (object)o).OfType<Order>())
            .Should().NotBe(_generator.GenerateKey(_inMemory.Orders.Select(o => (object)o).OfType<Customer>()));
    }

    [Fact]
    public void InMemory_DateTimeCaptures_ShouldUseFullPrecision()
    {
        var t1 = new DateTime(2026, 1, 1, 10, 0, 0, 1);
        var t2 = new DateTime(2026, 1, 1, 10, 0, 0, 2);

        _generator.GenerateKey(_inMemory.Orders.Where(o => t1 > DateTime.MinValue))
            .Should().NotBe(_generator.GenerateKey(_inMemory.Orders.Where(o => t2 > DateTime.MinValue)));
    }

    [Fact]
    public void InMemory_StaticProperty_ShouldNotBeEvaluated()
    {
        // DateTime.Now is translated server-side; evaluating it would make every key unique.
        var key1 = _generator.GenerateKey(_inMemory.Orders.Where(o => DateTime.Now > DateTime.MinValue));
        var key2 = _generator.GenerateKey(_inMemory.Orders.Where(o => DateTime.Now > DateTime.MinValue));

        key1.Should().Be(key2);
    }

    [Fact]
    public void Sqlite_CapturedVariables_ShouldProduceDifferentKeys()
    {
        int id1 = 1, id2 = 2;

        _generator.GenerateKey(_sqlite.Orders.Where(o => o.Id == id1))
            .Should().NotBe(_generator.GenerateKey(_sqlite.Orders.Where(o => o.Id == id2)));
    }

    [Fact]
    public void Sqlite_ValueTypeProjection_ShouldGenerateKey()
    {
        _generator.GenerateKey(_sqlite.Orders.Select(o => o.Id))
            .Should().NotBe(_generator.GenerateKey(_sqlite.Orders.Select(o => o.Total)));
    }

    [Fact]
    public void Sqlite_UntranslatableQuery_ShouldFallBackToExpression()
    {
        // ToQueryString() throws for client-evaluated predicates; the expression still identifies the query.
        var key1 = _generator.GenerateKey(_sqlite.Orders.Where(o => ClientSide(o) && o.Id == 1));
        var key2 = _generator.GenerateKey(_sqlite.Orders.Where(o => ClientSide(o) && o.Id == 2));

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void Sqlite_CapturedSubquery_ShouldBeIncludedInKey()
    {
        var sub1 = _sqlite.Customers.Where(c => c.Id == 1).Select(c => c.Name);
        var sub2 = _sqlite.Customers.Where(c => c.Id == 2).Select(c => c.Name);

        _generator.GenerateKey(_sqlite.Orders.Where(o => sub1.Contains(o.Name)))
            .Should().NotBe(_generator.GenerateKey(_sqlite.Orders.Where(o => sub2.Contains(o.Name))));
    }

    [Fact]
    public void LinqToObjects_SourceContents_ShouldBePartOfKey()
    {
        var source1 = new List<int> { 1 }.AsQueryable();
        var source2 = new List<int> { 2 }.AsQueryable();

        _generator.GenerateKey(source1).Should().NotBe(_generator.GenerateKey(source2));
    }

    [Fact]
    public void StaticField_ShouldBeEvaluated()
    {
        Expression<Func<Order, bool>> expr = o => o.Id > StaticThreshold;

        QueryCacheKeyGenerator.GetExpressionString(expr).Should().Contain("=7");
    }
}

public class ExpressionStringVisitorValueTests
{
    private static string Render(Expression expression)
    {
        return QueryCacheKeyGenerator.GetExpressionString(expression);
    }

    [Fact]
    public void Constants_ShouldRenderAllValueKinds()
    {
        Render(Expression.Constant(null, typeof(string))).Should().Contain("null");
        Render(Expression.Constant("ab")).Should().Contain("2:ab");
        Render(Expression.Constant(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)))
            .Should().Contain("2026-01-02T03:04:05.0000000+00:00");
        Render(Expression.Constant(1.5m)).Should().Contain("1.5");
        Render(Expression.Constant(new Opaque())).Should().Contain("opaque!");
        Render(Expression.Constant(new[] { 1, 2 })).Should().Contain("[1,2,]");
    }

    [Fact]
    public void ThrowingCapturedProperty_ShouldFallBackToSymbolicRendering()
    {
        var thrower = new Thrower();
        Expression<Func<int>> expr = () => thrower.Value;

        Render(expr).Should().Contain(".Value");
    }

    [Fact]
    public void NullCapturedTarget_ShouldFallBackToSymbolicRendering()
    {
        Order? order = null;
        Expression<Func<string>> expr = () => order!.Name;

        Render(expr).Should().Contain(".Name");
    }

    [Fact]
    public void MemberInit_ShouldRenderBindingNames()
    {
        Expression<Func<Order, Order>> a = o => new Order { Name = o.Name };
        Expression<Func<Order, Order>> b = o => new Order { Name = o.Name, Total = o.Total };

        Render(a).Should().Contain("Name=");
        Render(a).Should().NotBe(Render(b));
    }

    [Fact]
    public void TypeIs_ShouldRenderTypeOperand()
    {
        Expression<Func<object, bool>> isOrder = o => o is Order;
        Expression<Func<object, bool>> isCustomer = o => o is Customer;

        Render(isOrder).Should().NotBe(Render(isCustomer));
    }

    [Fact]
    public void NodeWithNullChild_ShouldRender()
    {
        // Rethrow is a unary node without an operand: the visitor is called with a null child.
        Render(Expression.Rethrow()).Should().StartWith("Throw<System.Void>(");
    }

    [Fact]
    public void UnaryNot_ShouldDifferFromOperand()
    {
        Expression<Func<bool, bool>> plain = x => x;
        Expression<Func<bool, bool>> negated = x => !x;

        Render(plain).Should().NotBe(Render(negated));
    }

    private sealed class Thrower
    {
        public int Value => throw new InvalidOperationException();
    }

    private sealed class Opaque
    {
        public override string ToString()
        {
            return "opaque!";
        }
    }
}
