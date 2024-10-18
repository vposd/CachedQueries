namespace CachedQueries.Core.Abstractions;

/// <summary>
///     Defines the contract for a service that provides context information for caching operations.
/// </summary>
public interface ICacheContextProvider
{
    /// <summary>
    ///     Retrieves a unique context key that represents the current caching context.
    /// </summary>
    /// <returns>
    ///     A string that serves as the context key, which can be used to differentiate between different caching scenarios or
    ///     environments.
    /// </returns>
    string GetContextKey();
}
