using CachedQueries.Core.Abstractions;

namespace CachedQueries.Core;

public class DefaultCacheContextProvider: ICacheContextProvider
{
    public string GetContextKey()
    {
        return string.Empty;
    }
}
