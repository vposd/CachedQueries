namespace CachedQueries.Core.Abstractions;

public interface ICacheInvalidator
{
    Task InvalidateCacheAsync(string[] tags, CancellationToken cancellationToken);

    /// <summary>
    ///     Link key with invalidation tags
    /// </summary>
    /// <param name="key"></param>
    /// <param name="tags">Linking tags for further invalidation</param>
    /// <param name="cancellationToken"></param>
    Task LinkTagsAsync(string key, string[] tags, CancellationToken cancellationToken);
}
