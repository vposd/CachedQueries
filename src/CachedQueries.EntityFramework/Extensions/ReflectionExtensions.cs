using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace CachedQueries.EntityFramework.Extensions;

/// <summary>
///     Provides extension methods for querying types from IQueryable expressions,
///     specifically for Include and ThenInclude methods in Entity Framework.
/// </summary>
public static class ReflectionExtensions
{
    /// <summary>
    ///     Extracts a list of types from the Include and ThenInclude methods in the query expression.
    /// </summary>
    /// <param name="query">The IQueryable query to analyze.</param>
    /// <returns>An enumerable of types extracted from the Include and ThenInclude methods.</returns>
    public static IEnumerable<Type> GetIncludeTypes(this IQueryable query)
    {
        if (query.Expression is QueryRootExpression queryRoot)
        {
            return new List<Type> { queryRoot.ElementType };
        }

        var expression = (MethodCallExpression)query.Expression;
        return expression.GetMemberCallExpressionTypes();
    }

    /// <summary>
    ///     Recursively retrieves the types involved in member calls for Include and ThenInclude methods.
    /// </summary>
    /// <param name="expressionArgument">The MethodCallExpression representing the Include or ThenInclude call.</param>
    /// <returns>A set of types extracted from the member call expressions.</returns>
    private static IEnumerable<Type> GetMemberCallExpressionTypes(this MethodCallExpression expressionArgument)
    {
        var list = GetArgumentTypes(expressionArgument);

        if (expressionArgument.Method.Name is not ("Include" or "ThenInclude"))
        {
            return list.ToHashSet();
        }

        var expression = expressionArgument.Arguments.First(x => x is UnaryExpression);
        var lambda = (LambdaExpression)((UnaryExpression)expression).Operand;

        var returnType = lambda.ReturnType;
        if (returnType.GetInterface(nameof(IEnumerable)) != null)
        {
            var type = returnType.GetGenericArguments().FirstOrDefault();
            if (type is not null)
            {
                list.Add(type);
            }

            return list.ToHashSet();
        }

        list.Add(returnType);

        var memberExpression = (MemberExpression)lambda.Body;
        list.Add(memberExpression.Expression!.Type);

        return list.ToHashSet();
    }

    /// <summary>
    ///     Retrieves argument types from a MethodCallExpression.
    /// </summary>
    /// <param name="expressionArgument">The MethodCallExpression to analyze.</param>
    /// <returns>A list of types from the arguments of the MethodCallExpression.</returns>
    private static List<Type> GetArgumentTypes(MethodCallExpression expressionArgument)
    {
        var list = new List<Type>();

        foreach (var item in expressionArgument.Arguments.ToList())
        {
            if (item is QueryRootExpression queryRoot)
            {
                list.Add(queryRoot.ElementType);
            }

            if (item is not MethodCallExpression itemExpression)
            {
                continue;
            }

            list.AddRange(itemExpression.GetMemberCallExpressionTypes());
        }

        return list;
    }
}
