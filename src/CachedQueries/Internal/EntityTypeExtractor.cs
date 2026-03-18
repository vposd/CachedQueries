using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries.Internal;

/// <summary>
/// Extracts entity types from a LINQ expression tree.
/// Used to determine which entity types a query depends on for cache invalidation.
/// </summary>
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

    private static bool IsEntityType(Type type)
    {
        return type.IsClass &&
               !type.IsPrimitive &&
               !type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true &&
               type != typeof(string);
    }

    private sealed class EntityTypeVisitor : ExpressionVisitor
    {
        private readonly HashSet<Type> _entityTypes = [];

        public IReadOnlySet<Type> EntityTypes => _entityTypes;

        public void AddType(Type type) => _entityTypes.Add(type);

        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        protected override Expression VisitConstant(ConstantExpression node)
        {
            // DbSet<T> appears as a constant in the expression tree
            if (node.Value is IQueryable queryable)
            {
                _entityTypes.Add(queryable.ElementType);
            }

            return base.VisitConstant(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // Include navigation properties
            var memberType = node.Type;

            if (memberType.IsGenericType)
            {
                var genericType = memberType.GetGenericTypeDefinition();
                if (genericType == typeof(ICollection<>) ||
                    genericType == typeof(IEnumerable<>) ||
                    genericType == typeof(List<>) ||
                    genericType == typeof(IList<>))
                {
                    var elementType = memberType.GetGenericArguments()[0];
                    if (IsEntityType(elementType))
                    {
                        _entityTypes.Add(elementType);
                    }
                }
            }
            else if (IsEntityType(memberType))
            {
                _entityTypes.Add(memberType);
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Handle Include/ThenInclude
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
            {
                if (node.Method.Name is "Include" or "ThenInclude")
                {
                    ExtractIncludedType(node);
                }
            }

            return base.VisitMethodCall(node);
        }

        private void ExtractIncludedType(MethodCallExpression node)
        {
            // The lambda expression in Include contains the navigation property
            foreach (var arg in node.Arguments)
            {
                if (arg is UnaryExpression { Operand: LambdaExpression lambda })
                {
                    var returnType = lambda.ReturnType;

                    if (returnType.IsGenericType)
                    {
                        var genericDef = returnType.GetGenericTypeDefinition();
                        if (genericDef == typeof(ICollection<>) ||
                            genericDef == typeof(IEnumerable<>) ||
                            genericDef == typeof(List<>))
                        {
                            var elementType = returnType.GetGenericArguments()[0];
                            if (IsEntityType(elementType))
                            {
                                _entityTypes.Add(elementType);
                            }
                        }
                    }
                    else if (IsEntityType(returnType))
                    {
                        _entityTypes.Add(returnType);
                    }
                }
            }
        }
    }
}

