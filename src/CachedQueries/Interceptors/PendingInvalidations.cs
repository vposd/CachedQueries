using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CachedQueries.Interceptors;

/// <summary>
///     Entity types changed by a <see cref="DbContext" /> whose cache invalidation has not run yet
///     (SaveChanges in progress, or completed inside a transaction that has not been committed).
/// </summary>
/// <remarks>
///     State is attached to the context instance through a <see cref="ConditionalWeakTable{TKey,TValue}" />, so it
///     is released together with the context even when a transaction is never committed nor rolled back (e.g.
///     disposed). Pooled contexts are told apart by <see cref="DbContext.ContextId" />, which changes on every lease.
///     A <see cref="DbContext" /> is not thread-safe, so the per-context state needs no synchronization.
/// </remarks>
internal static class PendingInvalidations
{
    private static readonly ConditionalWeakTable<DbContext, Entry> Entries = new();

    public static void Add(DbContext context, IEnumerable<Type> entityTypes)
    {
        var id = context.ContextId;
        if (!Entries.TryGetValue(context, out var entry) || entry.Id != id)
        {
            // New context, or a pooled instance re-leased: state from a previous lease is dropped.
            entry = new Entry(id);
            Entries.AddOrUpdate(context, entry);
        }

        entry.Types.UnionWith(entityTypes);
    }

    /// <summary>
    ///     Removes and returns the pending entity types, or null when there are none for the current lease.
    /// </summary>
    public static IReadOnlyCollection<Type>? Take(DbContext context)
    {
        if (!Entries.TryGetValue(context, out var entry))
        {
            return null;
        }

        Entries.Remove(context);
        return entry.Id == context.ContextId ? entry.Types : null;
    }

    public static void Discard(DbContext context)
    {
        Entries.Remove(context);
    }

    public static bool HasPending(DbContext context)
    {
        return Entries.TryGetValue(context, out var entry) && entry.Id == context.ContextId;
    }

    /// <summary>
    ///     Entity types whose cached queries must be invalidated when <paramref name="entityType" /> changes:
    ///     its CLR type and base types (TPH/TPT/TPC), the owner chain for owned types, and both sides of
    ///     many-to-many join entities (shared-type property bags).
    /// </summary>
    public static IEnumerable<Type> GetAffectedTypes(IEntityType entityType)
    {
        var result = new HashSet<Type>();
        Collect(entityType, result);
        return result;
    }

    private static void Collect(IEntityType entityType, HashSet<Type> result)
    {
        for (var current = entityType; current is not null; current = current.BaseType)
        {
            result.Add(current.ClrType);
        }

        if (entityType.IsPropertyBag)
        {
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                Collect(foreignKey.PrincipalEntityType, result);
            }
        }

        var ownership = entityType.FindOwnership();
        if (ownership is not null)
        {
            Collect(ownership.PrincipalEntityType, result);
        }
    }

    private sealed class Entry(DbContextId id)
    {
        public DbContextId Id { get; } = id;
        public HashSet<Type> Types { get; } = [];
    }
}
