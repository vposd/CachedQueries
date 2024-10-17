namespace CachedQueries.Core.Models;

/// <summary>
/// Represents options for caching, including duration and tags for cache management.
/// </summary>
public class CachingOptions
{
    /// <summary>
    /// Gets or sets the duration for which cached items remain valid.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets an array of tags associated with the cached items for invalidation purposes.
    /// Defaults to an empty array.
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets a value indicating whether tags should be retrieved from the query.
    /// Returns true if no tags are specified; otherwise, false.
    /// </summary>
    public bool RetrieveTagsFromQuery => Tags.Length == 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingOptions"/> class with default values.
    /// </summary>
    public CachingOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingOptions"/> class with the specified tags.
    /// </summary>
    /// <param name="tags">The tags associated with the cached items.</param>
    public CachingOptions(string[] tags) : this()
    {
        Tags = tags;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingOptions"/> class with the specified cache duration.
    /// </summary>
    /// <param name="cacheDuration">The duration for which cached items remain valid.</param>
    public CachingOptions(TimeSpan cacheDuration) : this()
    {
        CacheDuration = cacheDuration;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingOptions"/> class with the specified cache duration and tags.
    /// </summary>
    /// <param name="cacheDuration">The duration for which cached items remain valid.</param>
    /// <param name="tags">The tags associated with the cached items.</param>
    public CachingOptions(TimeSpan cacheDuration, string[] tags) : this(tags)
    {
        CacheDuration = cacheDuration;
    }
}
