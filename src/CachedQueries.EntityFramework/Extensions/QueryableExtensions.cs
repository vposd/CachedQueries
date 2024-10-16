namespace CachedQueries.EntityFramework.Extensions;

public static class QueryableExtensions
{
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
