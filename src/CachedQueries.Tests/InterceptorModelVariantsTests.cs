using System.Transactions;
using CachedQueries.Abstractions;
using CachedQueries.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CachedQueries.Tests;

public abstract class Animal
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Dog : Animal
{
    public bool GoodBoy { get; set; }
}

public class Cat : Animal
{
    public int Lives { get; set; }
}

public class Address
{
    public string City { get; set; } = string.Empty;
}

public class Person
{
    public int Id { get; set; }
    public Address Address { get; set; } = new();
}

public class Post
{
    public int Id { get; set; }
    public List<Label> Labels { get; set; } = [];
}

public class Label
{
    public int Id { get; set; }
    public List<Post> Posts { get; set; } = [];
}

public class VariantsDbContext(DbContextOptions<VariantsDbContext> options) : DbContext(options)
{
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Label> Labels => Set<Label>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Dog>();
        modelBuilder.Entity<Cat>();
        modelBuilder.Entity<Person>().OwnsOne(p => p.Address);
        modelBuilder.Entity<Post>().HasMany(p => p.Labels).WithMany(l => l.Posts);
    }
}

[Collection("Interceptors")]
public class InterceptorModelVariantsTests : IDisposable
{
    private readonly VariantsDbContext _context;
    private readonly ICacheInvalidator _invalidator = Substitute.For<ICacheInvalidator>();

    public InterceptorModelVariantsTests()
    {
        var options = new DbContextOptionsBuilder<VariantsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new CacheInvalidationInterceptor(_invalidator))
            .Options;
        _context = new VariantsDbContext(options);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private Task<IEnumerable<Type>> CapturedTypesAsync()
    {
        var call = _invalidator.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "InvalidateAsync");
        return Task.FromResult((IEnumerable<Type>)call.GetArguments()[0]!);
    }

    [Fact]
    public async Task DerivedEntity_ShouldInvalidateBaseTypes()
    {
        _context.Animals.Add(new Dog { Name = "Rex" });
        await _context.SaveChangesAsync();

        var types = await CapturedTypesAsync();
        types.Should().Contain([typeof(Dog), typeof(Animal)]).And.NotContain(typeof(Cat));
    }

    [Fact]
    public async Task OwnedEntityChange_ShouldInvalidateOwner()
    {
        var person = new Person { Address = new Address { City = "A" } };
        _context.People.Add(person);
        await _context.SaveChangesAsync();
        _invalidator.ClearReceivedCalls();

        person.Address.City = "B";
        await _context.SaveChangesAsync();

        var types = await CapturedTypesAsync();
        types.Should().Contain([typeof(Address), typeof(Person)]);
    }

    [Fact]
    public async Task ManyToManyLinkChange_ShouldInvalidateBothSides()
    {
        var post = new Post();
        var label = new Label();
        _context.AddRange(post, label);
        await _context.SaveChangesAsync();
        _invalidator.ClearReceivedCalls();

        // Only the (property bag) join entity changes here
        post.Labels.Add(label);
        await _context.SaveChangesAsync();

        var types = await CapturedTypesAsync();
        types.Should().Contain([typeof(Post), typeof(Label)]);
    }
}

[Collection("Interceptors")]
public class InterceptorTransactionScopeTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly ICacheInvalidator _invalidator = Substitute.For<ICacheInvalidator>();

    public InterceptorTransactionScopeTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new CacheInvalidationInterceptor(_invalidator))
            .Options;
        _context = new TestDbContext(options);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void SaveChanges_InCompletedScope_ShouldInvalidateOnlyAfterCommit()
    {
        using (var scope = new TransactionScope())
        {
            _context.Orders.Add(new Order { Name = "A" });
            _context.SaveChanges();

            _invalidator.ReceivedCalls().Should().BeEmpty("invalidation must wait for the scope to commit");
            scope.Complete();
        }

        _invalidator.Received(1).InvalidateAsync(Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Order))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_InCompletedAsyncFlowScope_ShouldInvalidateAfterCommit()
    {
        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            _context.Orders.Add(new Order { Name = "A" });
            await _context.SaveChangesAsync();

            _invalidator.ReceivedCalls().Should().BeEmpty();
            scope.Complete();
        }

        await _invalidator.Received(1).InvalidateAsync(Arg.Any<IEnumerable<Type>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SaveChanges_InAbortedScope_ShouldNotInvalidate()
    {
        using (new TransactionScope())
        {
            _context.Orders.Add(new Order { Name = "A" });
            _context.SaveChanges();
            // no Complete() -> rollback
        }

        _invalidator.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void SaveChanges_InScopeWithoutChanges_ShouldNotSubscribe()
    {
        using (var scope = new TransactionScope())
        {
            _context.SaveChanges();
            scope.Complete();
        }

        _invalidator.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void InvalidationFailure_AfterScopeCommit_ShouldBeSwallowed()
    {
        _invalidator.InvalidateAsync(Arg.Any<IEnumerable<Type>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("cache down"));

        var act = () =>
        {
            using var scope = new TransactionScope();
            _context.Orders.Add(new Order { Name = "A" });
            _context.SaveChanges();
            scope.Complete();
        };

        act.Should().NotThrow();
    }
}

[Collection("Interceptors")]
public class PendingInvalidationsLifetimeTests
{
    [Fact]
    public void PooledContext_ReLeased_ShouldNotSeePreviousLeasePending()
    {
        var services = new ServiceCollection();
        services.AddDbContextPool<TestDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        using var provider = services.BuildServiceProvider();

        TestDbContext first;
        using (var scope = provider.CreateScope())
        {
            first = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            PendingInvalidations.Add(first, [typeof(Order)]);
            PendingInvalidations.HasPending(first).Should().BeTrue();
        }

        using (var scope = provider.CreateScope())
        {
            var second = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            second.Should().BeSameAs(first, "the pool hands out the same instance with a new lease");

            PendingInvalidations.HasPending(second).Should().BeFalse();
            PendingInvalidations.Take(second).Should().BeNull();

            // A new lease starts fresh
            PendingInvalidations.Add(second, [typeof(Customer)]);
            PendingInvalidations.Take(second).Should().BeEquivalentTo([typeof(Customer)]);
        }
    }

    [Fact]
    public void Add_ShouldAccumulateAcrossCalls()
    {
        using var context = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        PendingInvalidations.Add(context, [typeof(Order)]);
        PendingInvalidations.Add(context, [typeof(Customer), typeof(Order)]);

        PendingInvalidations.Take(context).Should().BeEquivalentTo([typeof(Order), typeof(Customer)]);
        PendingInvalidations.Take(context).Should().BeNull();
    }
}

[Collection("Interceptors")]
public class FailedSaveInsideTransactionTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly ICacheInvalidator _invalidator = Substitute.For<ICacheInvalidator>();

    public FailedSaveInsideTransactionTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .AddInterceptors(new CacheInvalidationInterceptor(_invalidator),
                new TransactionCacheInvalidationInterceptor(_invalidator))
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
    public async Task FailedSave_AfterSuccessfulSave_InTransaction_ShouldStillInvalidateOnCommit()
    {
        // Regression: a failed SaveChanges discarded the pending invalidations of earlier, successful
        // SaveChanges in the same transaction, so committing them left stale cache entries.
        await using var transaction = await _context.Database.BeginTransactionAsync();

        _context.Customers.Add(new Customer { Name = "C" });
        await _context.SaveChangesAsync();

        await _context.Database.ExecuteSqlRawAsync("INSERT INTO Orders (Id, Name, Total) VALUES (500, 'x', 1)");
        _context.Orders.Add(new Order { Id = 500, Name = "dup" });
        var failing = () => _context.SaveChangesAsync();
        await failing.Should().ThrowAsync<DbUpdateException>();
        _context.ChangeTracker.Clear();

        await transaction.CommitAsync();

        await _invalidator.Received(1).InvalidateAsync(
            Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Customer))), Arg.Any<CancellationToken>());
    }
}
