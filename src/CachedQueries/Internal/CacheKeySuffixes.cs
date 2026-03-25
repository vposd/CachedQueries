namespace CachedQueries.Internal;

/// <summary>
///     Suffixes appended to cache keys by scalar terminal methods.
/// </summary>
internal static class CacheKeySuffixes
{
    internal const string Count = ":count";
    internal const string Any = ":any";

    internal static readonly string[] All = [Count, Any];
}
