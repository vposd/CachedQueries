namespace CachedQueries.Internal;

/// <summary>
///     Process-wide async mutex per string key, used for cache stampede protection.
/// </summary>
/// <remarks>
///     Entries are reference-counted: an entry is removed only when no caller holds or awaits it, so every
///     concurrent caller for a key always contends on the same semaphore and the dictionary does not grow
///     with the number of distinct keys ever seen.
/// </remarks>
internal static class KeyedLock
{
    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal);

    /// <summary>
    ///     Number of keys currently held or awaited. Exposed for tests.
    /// </summary>
    internal static int ActiveKeyCount
    {
        get
        {
            lock (Entries)
            {
                return Entries.Count;
            }
        }
    }

    public static async Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken)
    {
        Entry entry;
        lock (Entries)
        {
            if (!Entries.TryGetValue(key, out entry!))
            {
                entry = new Entry();
                Entries[key] = entry;
            }

            entry.RefCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Release(key, entry);
            throw;
        }

        return new Releaser(key, entry);
    }

    private static void Release(string key, Entry entry)
    {
        lock (Entries)
        {
            if (--entry.RefCount == 0)
            {
                Entries.Remove(key);
            }
        }
    }

    private sealed class Entry
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
    }

    private sealed class Releaser(string key, Entry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                entry.Semaphore.Release();
                Release(key, entry);
            }
        }
    }
}
