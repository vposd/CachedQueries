using CachedQueries.Internal;
using FluentAssertions;
using Xunit;

namespace CachedQueries.Tests;

public class KeyedLockTests
{
    [Fact]
    public async Task SameKey_ShouldBeMutuallyExclusive()
    {
        var key = Guid.NewGuid().ToString();
        var inside = 0;
        var maxInside = 0;

        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            using (await KeyedLock.AcquireAsync(key, CancellationToken.None))
            {
                var now = Interlocked.Increment(ref inside);
                InterlockedMax(ref maxInside, now);
                await Task.Delay(2);
                Interlocked.Decrement(ref inside);
            }
        }));
        await Task.WhenAll(tasks);

        maxInside.Should().Be(1);
    }

    [Fact]
    public async Task WaitersOnReleasedKey_ShouldNotRunConcurrentlyWithNewArrivals()
    {
        // Regression: the semaphore used to be removed from the dictionary while callers were still waiting
        // on it, so a new arrival created a second semaphore and ran concurrently with a waiter.
        var key = Guid.NewGuid().ToString();
        var inside = 0;
        var maxInside = 0;

        async Task Work()
        {
            using (await KeyedLock.AcquireAsync(key, CancellationToken.None))
            {
                InterlockedMax(ref maxInside, Interlocked.Increment(ref inside));
                await Task.Yield();
                Interlocked.Decrement(ref inside);
            }
        }

        for (var round = 0; round < 50; round++)
        {
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(Work)));
        }

        maxInside.Should().Be(1);
    }

    [Fact]
    public async Task DifferentKeys_ShouldNotBlockEachOther()
    {
        using var first = await KeyedLock.AcquireAsync(Guid.NewGuid().ToString(), CancellationToken.None);

        var second = KeyedLock.AcquireAsync(Guid.NewGuid().ToString(), CancellationToken.None);

        (await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(second);
        (await second).Dispose();
    }

    [Fact]
    public async Task CancelledWaiter_ShouldThrowAndReleaseItsReference()
    {
        var key = Guid.NewGuid().ToString();
        var holder = await KeyedLock.AcquireAsync(key, CancellationToken.None);
        using var cts = new CancellationTokenSource();

        var waiter = KeyedLock.AcquireAsync(key, cts.Token);
        cts.Cancel();

        await waiter.Invoking(w => w).Should().ThrowAsync<OperationCanceledException>();

        holder.Dispose();
        // The key can be acquired again immediately
        using var again = await KeyedLock.AcquireAsync(key, CancellationToken.None);
    }

    [Fact]
    public async Task Release_ShouldRemoveKeyAndTolerateDoubleDispose()
    {
        var key = Guid.NewGuid().ToString();
        var before = KeyedLock.ActiveKeyCount;

        var handle = await KeyedLock.AcquireAsync(key, CancellationToken.None);
        KeyedLock.ActiveKeyCount.Should().BeGreaterThan(before - 1);

        handle.Dispose();
        handle.Dispose();

        using var again = await KeyedLock.AcquireAsync(key, CancellationToken.None);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)) &&
               Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }
}
