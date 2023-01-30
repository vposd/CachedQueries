namespace CachedQueries.Core;

public class CacheOptions
{
    public TimeSpan LockTimeout { get; set; }
    public TimeSpan DefaultExpiration { get; set; }
}
