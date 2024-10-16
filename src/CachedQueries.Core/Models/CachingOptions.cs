namespace CachedQueries.Core.Models;

public class CachingOptions
{
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
    public string[] Tags { get; set; } = [];
    public bool RetrieveTagsFromQuery => Tags.Length == 0;
    
    public CachingOptions()
    {
    }

    public CachingOptions(string[] tags): this()
    {
        Tags = tags;
    }
    
    public CachingOptions(TimeSpan cacheDuration): this()
    {
        CacheDuration = cacheDuration;
    }
    
    public CachingOptions(TimeSpan cacheDuration, string[] tags): this(tags)
    {
        CacheDuration = cacheDuration;
    }
}
