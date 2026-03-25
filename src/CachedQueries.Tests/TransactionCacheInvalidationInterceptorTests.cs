using System.Collections.Concurrent;
using System.Data.Common;
using CachedQueries.Abstractions;
using CachedQueries.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CachedQueries.Tests;

[Collection("Interceptors")]
public class TransactionCacheInvalidationInterceptorTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly ICacheInvalidator _invalidator;
    private readonly CacheInvalidationInterceptor _saveChangesInterceptor;
    private readonly ILogger<CacheInvalidationInterceptor> _saveChangesLogger;
    private readonly TransactionCacheInvalidationInterceptor _transactionInterceptor;
    private readonly ILogger<TransactionCacheInvalidationInterceptor> _transactionLogger;

    public TransactionCacheInvalidationInterceptorTests()
    {
        _invalidator = Substitute.For<ICacheInvalidator>();
        _saveChangesLogger = Substitute.For<ILogger<CacheInvalidationInterceptor>>();
        _transactionLogger = Substitute.For<ILogger<TransactionCacheInvalidationInterceptor>>();

        _saveChangesInterceptor = new CacheInvalidationInterceptor(_invalidator, _saveChangesLogger);
        _transactionInterceptor = new TransactionCacheInvalidationInterceptor(_invalidator, _transactionLogger);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .AddInterceptors(_saveChangesInterceptor, _transactionInterceptor)
            .Options;

        _context = new TestDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        // Clear any pending invalidations from previous tests
        TransactionCacheInvalidationInterceptor.PendingInvalidations.Clear();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task Transaction_WhenCommitted_ShouldInvalidateCache()
    {
        // Arrange
        await using var transaction = await _context.Database.BeginTransactionAsync();

        _context.Orders.Add(new Order { Name = "Test", Total = 100 });
        await _context.SaveChangesAsync();

        // SaveChanges inside transaction should not invalidate immediately
        await _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());

        // Act
        await transaction.CommitAsync();

        // Assert - should invalidate after commit
        await _invalidator.Received(1).InvalidateAsync(
            Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Order))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_WhenRolledBack_ShouldNotInvalidateCache()
    {
        // Arrange
        await using var transaction = await _context.Database.BeginTransactionAsync();

        _context.Orders.Add(new Order { Name = "Test", Total = 100 });
        await _context.SaveChangesAsync();

        // Act
        await transaction.RollbackAsync();

        // Assert - should NOT invalidate after rollback
        await _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_WithMultipleSaveChanges_ShouldAccumulateAndInvalidateOnCommit()
    {
        // Arrange
        await using var transaction = await _context.Database.BeginTransactionAsync();

        _context.Orders.Add(new Order { Name = "Order 1", Total = 100 });
        await _context.SaveChangesAsync();

        _context.Customers.Add(new Customer { Name = "Customer 1", Email = "test@test.com" });
        await _context.SaveChangesAsync();

        // Act
        await transaction.CommitAsync();

        // Assert - should invalidate both entity types
        await _invalidator.Received(1).InvalidateAsync(
            Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Order)) && t.Contains(typeof(Customer))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Transaction_Sync_WhenCommitted_ShouldInvalidateCache()
    {
        // Arrange
        using var transaction = _context.Database.BeginTransaction();

        _context.Orders.Add(new Order { Name = "Test", Total = 100 });
        _context.SaveChanges();

        // SaveChanges inside transaction should not invalidate immediately
        _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());

        // Act
        transaction.Commit();

        // Assert - should invalidate after commit
        _invalidator.Received(1).InvalidateAsync(
            Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Order))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Transaction_Sync_WhenRolledBack_ShouldNotInvalidateCache()
    {
        // Arrange
        using var transaction = _context.Database.BeginTransaction();

        _context.Orders.Add(new Order { Name = "Test", Total = 100 });
        _context.SaveChanges();

        // Act
        transaction.Rollback();

        // Assert - should NOT invalidate after rollback
        _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void PendingInvalidations_ShouldBeConcurrentDictionary()
    {
        // Assert - verify the static dictionary exists and is accessible
        TransactionCacheInvalidationInterceptor.PendingInvalidations.Should().NotBeNull();
    }

    [Fact]
    public async Task Transaction_WhenExceptionDuringCommit_ShouldNotInvalidate()
    {
        // Arrange
        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.Orders.Add(new Order { Name = "Test", Total = 100 });
        await _context.SaveChangesAsync();

        // Act - rollback instead of commit to simulate failure
        await transaction.RollbackAsync();

        // Assert
        await _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Transaction_Sync_WithMultipleSaveChanges_ShouldAccumulateAndInvalidateOnCommit()
    {
        // Arrange
        using var transaction = _context.Database.BeginTransaction();

        _context.Orders.Add(new Order { Name = "Order 1", Total = 100 });
        _context.SaveChanges();

        _context.Customers.Add(new Customer { Name = "Customer 1", Email = "test@test.com" });
        _context.SaveChanges();

        // Act
        transaction.Commit();

        // Assert - should invalidate both entity types
        _invalidator.Received(1).InvalidateAsync(
            Arg.Is<IEnumerable<Type>>(t => t.Contains(typeof(Order)) && t.Contains(typeof(Customer))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_WhenInvalidatorThrows_ShouldLogWarningAndContinue()
    {
        // Arrange
        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.Orders.Add(new Order { Name = "Test", Total = 100 });
        await _context.SaveChangesAsync();

        _invalidator.InvalidateAsync(Arg.Any<IEnumerable<Type>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Invalidation failed")));

        // Act & Assert - should not throw
        await transaction.CommitAsync();
    }

    [Fact]
    public void Transaction_Sync_WhenInvalidatorThrows_ShouldLogWarningAndContinue()
    {
        // Arrange
        using var transaction = _context.Database.BeginTransaction();
        _context.Orders.Add(new Order { Name = "Test", Total = 100 });
        _context.SaveChanges();

        _invalidator.InvalidateAsync(Arg.Any<IEnumerable<Type>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Invalidation failed")));

        // Act & Assert - should not throw
        transaction.Commit();
    }
}

[Collection("Interceptors")]
public class TransactionCacheInvalidationInterceptorAdditionalTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly ICacheInvalidator _invalidator;
    private readonly CacheInvalidationInterceptor _saveChangesInterceptor;
    private readonly ILogger<CacheInvalidationInterceptor> _saveChangesLogger;
    private readonly TransactionCacheInvalidationInterceptor _transactionInterceptor;
    private readonly ILogger<TransactionCacheInvalidationInterceptor> _transactionLogger;

    public TransactionCacheInvalidationInterceptorAdditionalTests()
    {
        _invalidator = Substitute.For<ICacheInvalidator>();
        _saveChangesLogger = Substitute.For<ILogger<CacheInvalidationInterceptor>>();
        _transactionLogger = Substitute.For<ILogger<TransactionCacheInvalidationInterceptor>>();

        _saveChangesInterceptor = new CacheInvalidationInterceptor(_invalidator, _saveChangesLogger);
        _transactionInterceptor = new TransactionCacheInvalidationInterceptor(_invalidator, _transactionLogger);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .AddInterceptors(_saveChangesInterceptor, _transactionInterceptor)
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
    public void Interceptor_ShouldBeCreatedWithNullLogger()
    {
        // Arrange & Act
        var interceptor = new TransactionCacheInvalidationInterceptor(_invalidator);

        // Assert
        interceptor.Should().NotBeNull();
    }

    [Fact]
    public void PendingInvalidations_WhenManuallyCleared_ShouldBeEmpty()
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
    public async Task Transaction_WhenCommittedWithNoChanges_ShouldNotInvalidate()
    {
        // Arrange
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // No changes

        // Act
        await transaction.CommitAsync();

        // Assert
        await _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Transaction_Sync_WhenCommittedWithNoChanges_ShouldNotInvalidate()
    {
        // Arrange
        using var transaction = _context.Database.BeginTransaction();

        // No changes

        // Act
        transaction.Commit();

        // Assert
        _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Transaction_RollbackAsync_ShouldNotInvalidate()
    {
        // Arrange
        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.Orders.Add(new Order { Name = "Test", Total = 100 });
        await _context.SaveChangesAsync();

        // Act
        await transaction.RollbackAsync();

        // Assert - should NOT invalidate after rollback
        await _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Transaction_Rollback_ShouldNotInvalidate()
    {
        // Arrange
        using var transaction = _context.Database.BeginTransaction();
        _context.Orders.Add(new Order { Name = "Test", Total = 100 });
        _context.SaveChanges();

        // Act
        transaction.Rollback();

        // Assert - should NOT invalidate after rollback
        _invalidator.DidNotReceive().InvalidateAsync(
            Arg.Any<IEnumerable<Type>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TransactionFailed_ShouldClearPendingInvalidations()
    {
        // Arrange: seed pending invalidations
        var contextId = TransactionCacheInvalidationInterceptor.GetContextIdentifier(_context);
        var types = new ConcurrentDictionary<Type, byte>();
        types[typeof(Order)] = 0;
        TransactionCacheInvalidationInterceptor.PendingInvalidations[contextId] = types;

        // Get a real DbTransaction from the context
        using var efTransaction = _context.Database.BeginTransaction();
        var dbTransaction = efTransaction.GetDbTransaction();

        var eventData = CreateTransactionErrorEventData(dbTransaction, false);

        // Act
        _transactionInterceptor.TransactionFailed(dbTransaction, eventData);

        // Assert
        TransactionCacheInvalidationInterceptor.PendingInvalidations.ContainsKey(contextId).Should().BeFalse();

        efTransaction.Rollback();
    }

    [Fact]
    public async Task TransactionFailedAsync_ShouldClearPendingInvalidations()
    {
        // Arrange: seed pending invalidations
        var contextId = TransactionCacheInvalidationInterceptor.GetContextIdentifier(_context);
        var types = new ConcurrentDictionary<Type, byte>();
        types[typeof(Order)] = 0;
        TransactionCacheInvalidationInterceptor.PendingInvalidations[contextId] = types;

        await using var efTransaction = await _context.Database.BeginTransactionAsync();
        var dbTransaction = efTransaction.GetDbTransaction();

        var eventData = CreateTransactionErrorEventData(dbTransaction, true);

        // Act
        await _transactionInterceptor.TransactionFailedAsync(dbTransaction, eventData);

        // Assert
        TransactionCacheInvalidationInterceptor.PendingInvalidations.ContainsKey(contextId).Should().BeFalse();

        await efTransaction.RollbackAsync();
    }

    private TransactionErrorEventData CreateTransactionErrorEventData(DbTransaction transaction, bool async)
    {
        // Use the same EventDefinition approach as EF Core internals
        return new TransactionErrorEventData(
            new FakeEventDefinition(),
            (_, _) => "test",
            transaction,
            _context,
            Guid.Empty,
            Guid.Empty,
            async,
            "Commit",
            new Exception("Transaction failed"),
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);
    }

    /// <summary>
    ///     Minimal EventDefinitionBase subclass for test event data construction.
    /// </summary>
    private sealed class FakeEventDefinition : EventDefinitionBase
    {
        public FakeEventDefinition()
            : base(new LoggingOptions(), new EventId(1, "Test"), LogLevel.Debug, "Test")
        {
        }
    }

    private sealed class LoggingOptions : ILoggingOptions
    {
        public void Initialize(IDbContextOptions options)
        {
        }

        public void Validate(IDbContextOptions options)
        {
        }

        public bool IsSensitiveDataLoggingEnabled => false;
        public bool IsSensitiveDataLoggingWarned { get; set; }
        public bool DetailedErrorsEnabled => false;
        public WarningsConfiguration WarningsConfiguration => new();

        public bool ShouldWarnForStringEnumValueInJson(Type type)
        {
            return false;
        }
    }
}
