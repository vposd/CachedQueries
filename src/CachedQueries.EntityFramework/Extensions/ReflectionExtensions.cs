using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace CachedQueries.EntityFramework.Extensions;

public static class ReflectionExtensions
{
    /// <summary>
    ///     Returns list of types extracted from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query param</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IEnumerable<Type> GetIncludeTypes<T>(this IQueryable<T> query)
    {
        if (query.Expression is QueryRootExpression queryRoot)
            return new List<Type> { queryRoot.EntityType.ClrType };

        var expression = (MethodCallExpression) query.Expression;
        return expression.GetMemberCallExpressionTypes();
    }

    private static IEnumerable<Type> GetMemberCallExpressionTypes(this MethodCallExpression expressionArgument)
    {
        var list = new List<Type>();

        foreach (var item in expressionArgument.Arguments.ToList())
        {
            if (item is QueryRootExpression queryRoot)
                list.Add(queryRoot.EntityType.ClrType);

            if (item is not MethodCallExpression itemExpression)
                continue;

            list.AddRange(itemExpression.GetMemberCallExpressionTypes());
        }

        if (expressionArgument.Method.Name is not ("Include" or "ThenInclude"))
            return list.ToHashSet();

        var expression = expressionArgument.Arguments.First(x => x is UnaryExpression);
        var lambda = (LambdaExpression) ((UnaryExpression) expression).Operand;

        var returnType = lambda.ReturnType;
        if (returnType.GetInterface(nameof(IEnumerable)) != null)
        {
            var type = returnType.GetGenericArguments().FirstOrDefault();
            if (type is not null)
                list.Add(type);

            return list.ToHashSet();
        }

        list.Add(returnType);

        var memberExpression = (MemberExpression) lambda.Body;
        list.Add(memberExpression.Expression!.Type);

        return list.ToHashSet();
    }
}