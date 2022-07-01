namespace CachedQueries.Core.Interfaces;

public interface ICacheInvalidator
{
    Task InvalidateCacheAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}
