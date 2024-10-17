using CachedQueries.Core.Models;

namespace CachedQueries.Core.Abstractions;

public interface ICacheEntryStrategy
{
    Task<T?> ExecuteAsync<T>(IQueryable<T> query, CachingOptions options,
        CancellationToken cancellationToken = default);
}
