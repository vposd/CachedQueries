using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using CachedQueries.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries.Internal;

/// <summary>
///     Default cache key generator. The key is a SHA-256 hash of:
///     <list type="bullet">
///         <item>the SQL produced by EF Core's <c>ToQueryString()</c> (relational providers only — includes parameter
///         values and global query filter parameters), and</item>
///         <item>a canonical rendering of the expression tree with captured variable values inlined
///         (provider-independent; this is what distinguishes queries on non-relational providers such as InMemory or
///         Cosmos, whose <c>ToQueryString()</c> returns a constant message).</item>
///     </list>
/// </summary>
internal sealed class QueryCacheKeyGenerator : ICacheKeyGenerator
{
    public string GenerateKey<T>(IQueryable<T> query)
    {
        var sql = TryGetQueryString(query);
        var expression = GetExpressionString(query.Expression);
        return ComputeHash(sql + "\n" + expression);
    }

    public string GenerateKey<T>(IQueryable<T> query, Expression<Func<T, bool>>? predicate)
    {
        var baseQuery = predicate is not null ? query.Where(predicate) : query;
        return GenerateKey(baseQuery);
    }

    private static string TryGetQueryString<T>(IQueryable<T> query)
    {
        try
        {
            return query is IQueryable<object> entityQuery
                ? entityQuery.AsSingleQuery().ToQueryString()
                : query.ToQueryString();
        }
        catch (Exception)
        {
            // Not translatable to a query string (client evaluation, non-EF provider, ...):
            // the expression rendering alone identifies the query.
            return string.Empty;
        }
    }

    internal static string GetExpressionString(Expression expression)
    {
        var visitor = new ExpressionStringVisitor();
        visitor.Visit(expression);
        return visitor.ToString();
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}

/// <summary>
///     Produces a stable, unambiguous string representation of an expression tree.
///     Every node is rendered as <c>NodeType&lt;Type&gt;(...)</c>; captured variables are replaced by their values.
/// </summary>
internal sealed class ExpressionStringVisitor : ExpressionVisitor
{
    private readonly StringBuilder _builder = new();

    public override string ToString()
    {
        return _builder.ToString();
    }

    public override Expression? Visit(Expression? node)
    {
        if (node is null)
        {
            return null;
        }

        _builder.Append(node.NodeType).Append('<').Append(node.Type).Append(">(");
        var result = base.Visit(node);
        _builder.Append(')');
        return result;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        AppendValue(node.Value);
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (ClosureEvaluator.TryEvaluate(node, out var value))
        {
            _builder.Append('=');
            AppendValue(value);
            return node;
        }

        if (node.Expression is null)
        {
            // Static property (e.g. DateTime.Now) — translated by the provider, keep it symbolic.
            _builder.Append(node.Member.DeclaringType);
        }
        else
        {
            Visit(node.Expression);
        }

        _builder.Append('.').Append(node.Member.Name);
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Object is null)
        {
            _builder.Append(node.Method.DeclaringType);
        }
        else
        {
            Visit(node.Object);
        }

        _builder.Append('.').Append(node.Method.Name);
        if (node.Method.IsGenericMethod)
        {
            _builder.Append('<').AppendJoin(',', node.Method.GetGenericArguments().Select(t => t.ToString()))
                .Append('>');
        }

        _builder.Append('(');
        for (var i = 0; i < node.Arguments.Count; i++)
        {
            if (i > 0)
            {
                _builder.Append(',');
            }

            Visit(node.Arguments[i]);
        }

        _builder.Append(')');
        return node;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        _builder.Append("λ(");
        foreach (var param in node.Parameters)
        {
            _builder.Append($"{param.Type.Name} {param.Name},");
        }

        _builder.Append(")=>");
        Visit(node.Body);
        return node;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        _builder.Append(node.Name);
        return node;
    }

    protected override MemberBinding VisitMemberBinding(MemberBinding node)
    {
        _builder.Append(node.Member.Name).Append('=');
        return base.VisitMemberBinding(node);
    }

    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
    {
        _builder.Append(node.TypeOperand).Append(':');
        return base.VisitTypeBinary(node);
    }

    protected override Expression VisitExtension(Expression node)
    {
        // EF Core query roots (DbSet, FromSql, ...) — the type identifies the root; the SQL part of the key
        // covers raw SQL text and its arguments.
        _builder.Append(node.GetType().Name);
        return node;
    }

    private void AppendValue(object? value)
    {
        switch (value)
        {
            case null:
                _builder.Append("null");
                break;
            case string s:
                // Length prefix keeps strings unambiguous regardless of their content.
                _builder.Append(s.Length).Append(':').Append(s);
                break;
            case IQueryable queryable when ClosureEvaluator.HasOwnExpression(queryable):
                Visit(queryable.Expression);
                break;
            case IEnumerable sequence:
                _builder.Append('[');
                foreach (var item in sequence)
                {
                    AppendValue(item);
                    _builder.Append(',');
                }

                _builder.Append(']');
                break;
            case DateTime dateTime:
                _builder.Append(dateTime.ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset dateTimeOffset:
                _builder.Append(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));
                break;
            case IFormattable formattable:
                _builder.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                _builder.Append(value);
                break;
        }
    }
}
