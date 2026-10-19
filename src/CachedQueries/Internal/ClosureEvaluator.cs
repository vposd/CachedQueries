using System.Linq.Expressions;
using System.Reflection;

namespace CachedQueries.Internal;

/// <summary>
///     Evaluates the values of captured variables in an expression tree.
/// </summary>
/// <remarks>
///     Only member chains rooted at a constant (compiler-generated closures, captured fields and their
///     properties) or at a static field are evaluated. Static properties such as <c>DateTime.Now</c> are
///     intentionally left alone: EF Core translates them server-side, and evaluating them would make
///     every cache key unique.
/// </remarks>
internal static class ClosureEvaluator
{
    public static bool TryEvaluate(Expression? expression, out object? value)
    {
        switch (expression)
        {
            case ConstantExpression constant:
                value = constant.Value;
                return true;

            case MemberExpression { Expression: null, Member: FieldInfo staticField }:
                value = staticField.GetValue(null);
                return true;

            case MemberExpression { Expression: not null } member
                when TryEvaluate(member.Expression, out var target):
                try
                {
                    value = member.Member is FieldInfo field
                        ? field.GetValue(target)
                        : ((PropertyInfo)member.Member).GetValue(target);
                    return true;
                }
                catch (Exception)
                {
                    // e.g. null target or a throwing property getter — treat as not evaluable
                    value = null;
                    return false;
                }

            default:
                value = null;
                return false;
        }
    }

    /// <summary>
    ///     Returns true when the queryable's expression is not just a constant pointing back at itself
    ///     (as with <c>list.AsQueryable()</c>), i.e. when visiting its expression adds information.
    /// </summary>
    public static bool HasOwnExpression(IQueryable queryable)
    {
        return queryable.Expression is not ConstantExpression constant || !ReferenceEquals(constant.Value, queryable);
    }
}
