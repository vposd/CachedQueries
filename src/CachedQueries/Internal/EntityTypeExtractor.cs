using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace CachedQueries.Internal;

/// <summary>
///     Extracts entity types from a LINQ expression tree.
///     Used to determine which entity types a query depends on for cache invalidation.
/// </summary>
/// <remarks>
///     Detected dependencies: query roots (<c>DbSet</c>s, including those used in joins and subqueries),
///     navigation properties referenced anywhere in the query (<c>Include</c>, filters, projections), and
///     captured <see cref="IQueryable" /> variables. String-based <c>Include("Path")</c> is not detected; use a
///     lambda include or add explicit tags for such queries.
/// </remarks>
internal static class EntityTypeExtractor
{
    public static IReadOnlySet<Type> ExtractEntityTypes<T>(IQueryable<T> query)
    {
        var visitor = new EntityTypeVisitor();
        visitor.Visit(query.Expression);

        // Always include the root entity type if it's a valid entity
        var rootType = typeof(T);
        if (IsEntityType(rootType))
        {
            visitor.AddType(rootType);
        }

        return visitor.EntityTypes;
    }

    internal static bool IsEntityType(Type type)
    {
        if (!type.IsClass || type == typeof(string))
        {
            return false;
        }

        var ns = type.Namespace;
        return ns is null || (ns != "System" && !ns.StartsWith("System.", StringComparison.Ordinal) &&
                              !ns.StartsWith("Microsoft.", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Returns T for any IEnumerable&lt;T&gt; (collections, sets, DbSet&lt;T&gt;, IQueryable&lt;T&gt;), otherwise null.
    /// </summary>
    internal static Type? GetSequenceElementType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        return type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    private sealed class EntityTypeVisitor : ExpressionVisitor
    {
        private readonly HashSet<Type> _entityTypes = [];

        public IReadOnlySet<Type> EntityTypes => _entityTypes;

        public void AddType(Type type)
        {
            _entityTypes.Add(type);
        }

        protected override Expression VisitExtension(Expression node)
        {
            // EF Core represents DbSet<T> as a QueryRootExpression (not a ConstantExpression)
            if (node is QueryRootExpression root)
            {
                _entityTypes.Add(root.ElementType);
            }

            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            // Queryables inlined as constants (e.g. non-EF providers)
            if (node.Value is IQueryable queryable)
            {
                VisitQueryable(queryable);
            }

            return base.VisitConstant(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var memberType = node.Type;
            var candidate = GetSequenceElementType(memberType) ?? memberType;
            if (IsEntityType(candidate))
            {
                _entityTypes.Add(candidate);
            }

            // Captured queryables (e.g. `db.Customers` used inside a subquery, or a pre-built IQueryable variable)
            // carry their own expression tree with further dependencies.
            if (typeof(IQueryable).IsAssignableFrom(memberType) &&
                ClosureEvaluator.TryEvaluate(node, out var value) &&
                value is IQueryable queryable)
            {
                VisitQueryable(queryable);
                return node;
            }

            return base.VisitMember(node);
        }

        private void VisitQueryable(IQueryable queryable)
        {
            if (IsEntityType(queryable.ElementType))
            {
                _entityTypes.Add(queryable.ElementType);
            }

            if (ClosureEvaluator.HasOwnExpression(queryable))
            {
                Visit(queryable.Expression);
            }
        }
    }
}
