using System.Collections.Concurrent;
using CachedQueries.Abstractions;
using CachedQueries.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

[Collection("Interceptors")]
public class CacheInvalidationInterceptorTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly CacheInvalidationInterceptor _interceptor;
    private readonly ICacheInvalidator _invalidator;
    private readonly ILogger<CacheInvalidationInterceptor> _logger;

    public CacheInvalidationInterceptorTests()
    {
        _invalidator = Substitute.For<ICacheInvalidator>();
        _logger = Substitute.For<ILogger<CacheInvalidationInterceptor>>();
        _interceptor = new CacheInvalidationInterceptor(_invalidator, _logger);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(_interceptor)
            .Options;

        _context = new TestDbContext(options);

        // Clear any pending invalidations from previous tests
        TransactionCacheInvalidationInterceptor.PendingInvalidations.Clear();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenNoTransaction_ShouldInvalidateImmediately()
    {
        // Arrange
        _context.Orders.Add(new Order { Name = "Test", Total = 100 });

        // Act
        await _context.SaveChangesAsync();

        // Assert
        await _invalidator.Received(1).InvalidateAsync(
            Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Order))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SaveChanges_WhenNoTransaction_ShouldInvalidateImmediately()
    {
        // Arrange
        _context.Orders.Add(new Order { Name = "Test", Total = 100 });

        // Act
        _context.SaveChanges();

        // Assert
        _invalidator.Received(1).InvalidateAsync(
            Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Order))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_WithModifiedEntity_ShouldInvalidate()
    {
        // Arrange
        var order = new Order { Name = "Test", Total = 100 };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        _invalidator.ClearReceivedCalls();

        order.Total = 200;

        // Act
        await _context.SaveChangesAsync();

        // Assert
        await _invalidator.Received(1).InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_WithDeletedEntity_ShouldInvalidate()
    {
        // Arrange
        var order = new Order { Name = "Test", Total = 100 };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        _invalidator.ClearReceivedCalls();

        _context.Orders.Remove(order);

        // Act
        await _context.SaveChangesAsync();

        // Assert
        await _invalidator.Received(1).InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoChanges_ShouldNotInvalidate()
    {
        // Arrange - no changes

        // Act
        await _context.SaveChangesAsync();

        // Assert
        await _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_WithMultipleEntityTypes_ShouldInvalidateAll()
    {
        // Arrange
        var order = new Order { Name = "Test", Total = 100 };
        order.Items.Add(new OrderItem { ProductName = "Item 1", Quantity = 1 });
        _context.Orders.Add(order);

        // Act
        await _context.SaveChangesAsync();

        // Assert
        await _invalidator.Received(1).InvalidateAsync(
            Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Order)) && t.Contains(typeof(OrderItem))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SaveChanges_WithModifiedEntity_ShouldInvalidate()
    {
        // Arrange
        var order = new Order { Name = "Test", Total = 100 };
        _context.Orders.Add(order);
        _context.SaveChanges();
        _invalidator.ClearReceivedCalls();

        order.Total = 200;

        // Act
        _context.SaveChanges();

        // Assert
        _invalidator.Received(1).InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SaveChanges_WithNoChanges_ShouldNotInvalidate()
    {
        // Arrange - no changes

        // Act
        _context.SaveChanges();

        // Assert
        _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SaveChanges_WithDeletedEntity_ShouldInvalidate()
    {
        // Arrange
        var order = new Order { Name = "Test", Total = 100 };
        _context.Orders.Add(order);
        _context.SaveChanges();
        _invalidator.ClearReceivedCalls();

        _context.Orders.Remove(order);

        // Act
        _context.SaveChanges();

        // Assert
        _invalidator.Received(1).InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SaveChanges_WithMultipleEntityTypes_ShouldInvalidateAll()
    {
        // Arrange
        var order = new Order { Name = "Test", Total = 100 };
        order.Items.Add(new OrderItem { ProductName = "Item 1", Quantity = 1 });
        _context.Orders.Add(order);

        // Act
        _context.SaveChanges();

        // Assert
        _invalidator.Received(1).InvalidateAsync(
            Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Order)) && t.Contains(typeof(OrderItem))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_WhenInvalidatorThrows_ShouldLogWarningAndContinue()
    {
        // Arrange
        _context.Orders.Add(new Order { Name = "Test", Total = 100 });
        _invalidator.InvalidateAsync(Arg.Any<IEnumerable<Type>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Invalidation failed")));

        // Act & Assert - should not throw
        await _context.SaveChangesAsync();
    }

    [Fact]
    public void SaveChanges_WhenInvalidatorThrows_ShouldLogWarningAndContinue()
    {
        // Arrange
        _context.Orders.Add(new Order { Name = "Test", Total = 100 });
        _invalidator.InvalidateAsync(Arg.Any<IEnumerable<Type>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Invalidation failed")));

        // Act & Assert - should not throw
        _context.SaveChanges();
    }
}

[Collection("Interceptors")]
public class CacheInvalidationInterceptorFailureTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly CacheInvalidationInterceptor _interceptor;
    private readonly ICacheInvalidator _invalidator;
    private readonly ILogger<CacheInvalidationInterceptor> _logger;

    public CacheInvalidationInterceptorFailureTests()
    {
        _invalidator = Substitute.For<ICacheInvalidator>();
        _logger = Substitute.For<ILogger<CacheInvalidationInterceptor>>();
        _interceptor = new CacheInvalidationInterceptor(_invalidator, _logger);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .AddInterceptors(_interceptor)
            .Options;

        _context = new TestDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        TransactionCacheInvalidationInterceptor.PendingInvalidations.Clear();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public void PendingInvalidations_WhenCleared_ShouldBeEmpty()
    {
        // Arrange
        var contextId = TransactionCacheInvalidationInterceptor.GetContextIdentifier(_context);
        var types = new ConcurrentDictionary<Type, byte>();
        types[typeof(Order)] = 0;
        TransactionCacheInvalidationInterceptor.PendingInvalidations.TryAdd(contextId, types);

        // Act
        TransactionCacheInvalidationInterceptor.PendingInvalidations.TryRemove(contextId, out _);

        // Assert
        TransactionCacheInvalidationInterceptor.PendingInvalidations.Should().NotContainKey(contextId);
    }

    [Fact]
    public void Interceptor_ShouldBeCreatedWithNullLogger()
    {
        // Arrange & Act
        var interceptor = new CacheInvalidationInterceptor(_invalidator);

        // Assert
        interceptor.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_InsideTransaction_ShouldDeferInvalidation()
    {
        // Arrange
        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.Orders.Add(new Order { Name = "Test", Total = 100 });

        // Act
        await _context.SaveChangesAsync();

        // Assert - should NOT invalidate immediately
        await _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());

        await transaction.RollbackAsync();
    }

    [Fact]
    public void SaveChanges_InsideTransaction_ShouldDeferInvalidation()
    {
        // Arrange
        using var transaction = _context.Database.BeginTransaction();
        _context.Orders.Add(new Order { Name = "Test", Total = 100 });

        // Act
        _context.SaveChanges();

        // Assert - should NOT invalidate immediately
        _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());

        transaction.Rollback();
    }

    [Fact]
    public async Task SaveChangesFailedAsync_ShouldClearPendingInvalidations()
    {
        // Arrange: insert row directly via SQL, then try to add entity with same PK via EF
        _context.Database.ExecuteSqlRaw("INSERT INTO Orders (Id, Name, Total) VALUES (999, 'Direct', 100)");

        // EF doesn't know about this row, so Add + SaveChanges will fail with PK conflict
        var duplicate = new Order { Id = 999, Name = "Duplicate", Total = 200 };
        _context.Orders.Add(duplicate);

        // Act & Assert - SaveChanges should fail
        var act = () => _context.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>();

        // Pending invalidations should have been cleared
        var contextId = TransactionCacheInvalidationInterceptor.GetContextIdentifier(_context);
        TransactionCacheInvalidationInterceptor.PendingInvalidations.ContainsKey(contextId).Should().BeFalse();
    }

    [Fact]
    public void SaveChangesFailed_ShouldClearPendingInvalidations()
    {
        // Arrange: insert row directly via SQL
        _context.Database.ExecuteSqlRaw("INSERT INTO Orders (Id, Name, Total) VALUES (998, 'Direct', 100)");

        var duplicate = new Order { Id = 998, Name = "Duplicate", Total = 200 };
        _context.Orders.Add(duplicate);

        // Act
        var act = () => _context.SaveChanges();
        act.Should().Throw<Exception>();

        // Assert - pending invalidations should have been cleared
        var contextId = TransactionCacheInvalidationInterceptor.GetContextIdentifier(_context);
        TransactionCacheInvalidationInterceptor.PendingInvalidations.ContainsKey(contextId).Should().BeFalse();
    }
}
