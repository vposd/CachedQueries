using System;
using System.Threading;
using System.Threading.Tasks;
using CachedQueries.Core;
using FluentAssertions;
using Xunit;

namespace CachedQueries.Test;

    public class DefaultLockManagerTests
    {
    private readonly DefaultLockManager _lockManager = new();

    [Fact]
    public async Task CheckLockAsync_ShouldReturnImmediately_WhenKeyIsUnlocked()
    {
        // Given
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // When
        var task = _lockManager.CheckLockAsync("key", cts.Token);

        // Then
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromMilliseconds(100), cts.Token));
        task.Should().BeEquivalentTo(completed);
    }

    [Fact]
    public async Task LockAsync_ShouldLock()
    {
        // Given
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // When
        var lockTask = _lockManager.LockAsync("key", TimeSpan.FromMilliseconds(100), cts.Token);
        var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(25), cts.Token);
        var completedTask = await Task.WhenAny(lockTask, timeoutTask);
        var releaseTask = _lockManager.ReleaseLockAsync("key");
        var completedReleaseTask = await Task.WhenAny(releaseTask, timeoutTask);

        // Then
        lockTask.Should().BeEquivalentTo(completedTask);
        releaseTask.Should().BeEquivalentTo(completedReleaseTask);
    }

    [Fact]
    public async Task CheckLockAsync_ShouldBlock_WhenKeyIsLocked()
    {
        // Given
        await _lockManager.LockAsync("key", TimeSpan.FromMilliseconds(100));

        // When
        var checkTask = _lockManager.CheckLockAsync("key");
        var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(50));
        var completedTask = await Task.WhenAny(checkTask, timeoutTask);
        var releaseTask = _lockManager.ReleaseLockAsync("key");
        var completedReleaseTask = await Task.WhenAny(releaseTask, timeoutTask);

        // Then
        timeoutTask.Should().BeEquivalentTo(completedTask);
        releaseTask.Should().BeEquivalentTo(completedReleaseTask);
    }
}
