using System;
using System.Threading;
using System.Threading.Tasks;
using CachedQueries.Core;
using CachedQueries.Redis;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace CachedQueries.Test.Redis;

public class RedisLockManagerTests
{
    [Fact]
    public async Task LockAsync_Should_Lock_Key()
    {
        // Given
        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        var databaseMock = new Mock<IDatabase>();

        multiplexerMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(databaseMock.Object);

        var redisLockManager = new RedisLockManager(multiplexerMock.Object,
            new CacheOptions { LockTimeout = TimeSpan.FromMilliseconds(1000) });
        var timespan = TimeSpan.FromMilliseconds(1000);
        var cancellationToken = CancellationToken.None;

        databaseMock.Setup(x => x.LockTake("key", "key_lock", timespan, CommandFlags.None)).Returns(true);

        // When
        await redisLockManager.LockAsync("key", timespan, cancellationToken);

        // Then
        databaseMock.Verify(x => x.LockTake("key", "key_lock", timespan, CommandFlags.None), Times.Once());
    }

    [Fact]
    public async Task ReleaseLockAsync_Should_Release_Lock()
    {
        // Given
        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        var databaseMock = new Mock<IDatabase>();

        multiplexerMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(databaseMock.Object);
        var redisLockManager = new RedisLockManager(multiplexerMock.Object,
            new CacheOptions { LockTimeout = TimeSpan.FromMilliseconds(1000) });

        // When
        await redisLockManager.ReleaseLockAsync("key");

        // Then
        databaseMock.Verify(x => x.LockReleaseAsync("key", "key_lock", CommandFlags.None), Times.Once());
    }

    [Fact]
    public async Task CheckLockAsync_Should_Check_Lock()
    {
        // Given
        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        var databaseMock = new Mock<IDatabase>();

        multiplexerMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(databaseMock.Object);

        var redisLockManager = new RedisLockManager(multiplexerMock.Object,
            new CacheOptions { LockTimeout = TimeSpan.FromMilliseconds(1000) });
        var cancellationToken = CancellationToken.None;

        // When
        await redisLockManager.CheckLockAsync("key", cancellationToken);

        // Then
        databaseMock.Verify(x => x.LockQuery("key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task CheckLockAsync_ReturnsWhenCancellationTokenIsCancelled()
    {
        // Given
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        var databaseMock = new Mock<IDatabase>();
        multiplexerMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(databaseMock.Object);
        var cacheOptions = new CacheOptions
        {
            LockTimeout = TimeSpan.FromSeconds(5)
        };
        var sut = new RedisLockManager(multiplexerMock.Object, cacheOptions);

        // When
        await sut.CheckLockAsync("key", cancellationTokenSource.Token);

        // Then
        databaseMock.Verify(x => x.LockQuery(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task CheckLockAsync_ReturnsWhenLockIsAchieved()
    {
        // Arrange
        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        var databaseMock = new Mock<IDatabase>();
        multiplexerMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(databaseMock.Object);
        databaseMock.Setup(x => x.LockQuery(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(RedisValue.Null);
        var cacheOptions = new CacheOptions
        {
            LockTimeout = TimeSpan.FromSeconds(5)
        };
        var sut = new RedisLockManager(multiplexerMock.Object, cacheOptions);

        // Act
        await sut.CheckLockAsync("key");

        // Assert
        databaseMock.Verify(x => x.LockQuery("key", It.IsAny<CommandFlags>()), Times.Once);
        databaseMock.Verify(x => x.LockQuery("key_lock", It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task CheckLockAsync_AchievesLock()
    {
        // Arrange
        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        var databaseMock = new Mock<IDatabase>();
        multiplexerMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(databaseMock.Object);
        databaseMock.SetupSequence(x => x.LockQuery(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns("value1")
            .Returns(RedisValue.Null);
        var cacheOptions = new CacheOptions
        {
            LockTimeout = TimeSpan.FromSeconds(5)
        };
        var sut = new RedisLockManager(multiplexerMock.Object, cacheOptions);

        // Act
        await sut.CheckLockAsync("key");

        // Assert
        databaseMock.Verify(x => x.LockQuery("key", It.IsAny<CommandFlags>()), Times.Exactly(2));
        databaseMock.Verify(x => x.LockQuery("key_lock", It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task LockAsync_Should_Acquire_Lock()
    {
        // Arrange
        var multiplexer = new Mock<IConnectionMultiplexer>();
        var database = new Mock<IDatabase>();
        multiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);
        var cacheOptions = new CacheOptions { LockTimeout = TimeSpan.FromSeconds(1) };
        var sut = new RedisLockManager(multiplexer.Object, cacheOptions);

        var timespan = TimeSpan.FromSeconds(5);
        var cancellationToken = CancellationToken.None;

        var lockAchieved = false;
        database.Setup(x => x.LockTake(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan, CommandFlags>((k, v, e, _) => { lockAchieved = true; });

        // Act
        await sut.LockAsync("lock_key", timespan, cancellationToken);

        // Assert
        Assert.True(lockAchieved);
    }
}
