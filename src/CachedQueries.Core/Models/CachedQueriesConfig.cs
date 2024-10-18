namespace CachedQueries.Core.Models;

public class CachedQueriesConfig
{
    public TimeSpan DefaultCacheDuration { get; set; } = TimeSpan.FromMinutes(5);
}
