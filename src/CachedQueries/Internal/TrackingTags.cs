namespace CachedQueries.Internal;

/// <summary>
///     Builds tag names used for distributed cache entry tracking.
///     Entity types and user tags are stored as provider-level tags, enabling
///     multi-instance invalidation without in-memory state.
/// </summary>
internal static class TrackingTags
{
    /// <summary>
    ///     Builds a tag name for an entity type, optionally scoped to a context.
    ///     Uses Type.FullName which is already unique enough to avoid collisions with user tags.
    /// </summary>
    internal static string EntityTag(Type entityType, string? contextKey)
    {
        var typeName = entityType.FullName ?? entityType.Name;
        return contextKey is null ? $"tag:{typeName}" : $"{contextKey}:tag:{typeName}";
    }

    /// <summary>
    ///     Builds a tag name for a user-defined tag, optionally scoped to a context.
    /// </summary>
    internal static string UserTag(string tag, string? contextKey)
    {
        return contextKey is null ? $"tag:{tag}" : $"{contextKey}:tag:{tag}";
    }

    /// <summary>
    ///     Builds a context-level tag used by ClearContextAsync to remove all entries for a context.
    /// </summary>
    internal static string ContextTag(string contextKey)
    {
        return $"{contextKey}:tag:__context__";
    }

    /// <summary>
    ///     Builds all tracking tags for a cache entry being stored.
    ///     These tags are passed to the provider's SetAsync via CachingOptions.Tags.
    /// </summary>
    internal static IReadOnlyList<string> BuildTrackingTags(
        IEnumerable<Type> entityTypes,
        IEnumerable<string> userTags,
        string? contextKey)
    {
        var tags = new List<string>();
        var userTagsList = userTags as IReadOnlyCollection<string> ?? userTags.ToList();

        // Always include entity type tags so auto-invalidation via SaveChanges works.
        foreach (var type in entityTypes)
        {
            tags.Add(EntityTag(type, contextKey));
        }

        // Explicit user tags are added alongside entity type tags for manual invalidation.
        foreach (var tag in userTagsList)
        {
            tags.Add(UserTag(tag, contextKey));
        }

        // Context-level tag for ClearContextAsync
        if (contextKey is not null)
        {
            tags.Add(ContextTag(contextKey));
        }

        return tags;
    }

    /// <summary>
    ///     Builds tags to invalidate when entity types change.
    ///     Always includes global tags + current context tags.
    /// </summary>
    internal static IReadOnlyList<string> InvalidationTagsForEntityTypes(
        IEnumerable<Type> entityTypes, string? currentContextKey)
    {
        var tags = new List<string>();
        foreach (var type in entityTypes)
        {
            tags.Add(EntityTag(type, null));
            if (currentContextKey is not null)
            {
                tags.Add(EntityTag(type, currentContextKey));
            }
        }

        return tags;
    }

    /// <summary>
    ///     Builds tags to invalidate for user-defined tags.
    ///     Always includes global tags + current context tags.
    /// </summary>
    internal static IReadOnlyList<string> InvalidationTagsForUserTags(
        IEnumerable<string> userTags, string? currentContextKey)
    {
        var tags = new List<string>();
        foreach (var tag in userTags)
        {
            tags.Add(UserTag(tag, null));
            if (currentContextKey is not null)
            {
                tags.Add(UserTag(tag, currentContextKey));
            }
        }

        return tags;
    }
}
