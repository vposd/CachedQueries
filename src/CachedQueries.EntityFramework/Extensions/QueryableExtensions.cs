namespace CachedQueries.EntityFramework.Extensions;

/// <summary>
/// Provides extension methods for working with <see cref="IQueryable"/> to facilitate cache 
/// management in conjunction with Entity Framework.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Retrieves the raw invalidation tags from the query by extracting the types of entities included
    /// in the query. The tags are based on the full names of the included entity types.
    /// </summary>
    /// <param name="query">The <see cref="IQueryable"/> instance from which to retrieve invalidation tags.</param>
    /// <returns>An array of strings representing the invalidation tags extracted from the included types.</returns>
    public static string[] RetrieveRawInvalidationTagsFromQuery(this IQueryable query)
    {
        var includedTypes = query.GetIncludeTypes();
        var tags = includedTypes
            .Select(x => x.FullName)
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToArray();
        return tags;
    }
}
