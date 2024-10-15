namespace CachedQueries.Core.Models;

public class CachingOptions
{
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
    public string[] Tags { get; set; } = [];
    public bool RetrieveTagsFromQuery => Tags.Length != 0;
}
