using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using CachedQueries.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CachedQueries.Internal;

/// <summary>
///     Default implementation of cache key generator using query expression tree.
/// </summary>
internal sealed class QueryCacheKeyGenerator : ICacheKeyGenerator
{
    public string GenerateKey<T>(IQueryable<T> query)
    {
        var queryString = GetQueryString(query);
        return ComputeHash(queryString);
    }

    public string GenerateKey<T>(IQueryable<T> query, Expression<Func<T, bool>>? predicate)
    {
        var baseQuery = predicate is not null ? query.Where(predicate) : query;
        return GenerateKey(baseQuery);
    }

    [ExcludeFromCodeCoverage]
    private static string GetQueryString<T>(IQueryable<T> query)
    {
        // Use EF Core's query string if available
        try
        {
            var sql = (query as IQueryable<object>)!.AsSingleQuery().ToQueryString();
            if (!string.IsNullOrEmpty(sql))
            {
                return sql;
            }
        }
        catch
        {
            // Fallback to expression tree
        }

        return GetExpressionString(query.Expression);
    }

    private static string GetExpressionString(Expression expression)
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
///     Visitor that creates a stable string representation of an expression tree.
/// </summary>
internal sealed class ExpressionStringVisitor : ExpressionVisitor
{
    private readonly StringBuilder _builder = new();

    public override string ToString()
    {
        return _builder.ToString();
    }

    [ExcludeFromCodeCoverage]
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is IQueryable queryable)
        {
            _builder.Append($"Query({queryable.ElementType.FullName})");
        }
        else
        {
            _builder.Append(node.Value?.ToString() ?? "null");
        }

        return base.VisitConstant(node);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        _builder.Append($".{node.Member.Name}");
        return base.VisitMember(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _builder.Append($".{node.Method.Name}(");

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

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _builder.Append('(');
        Visit(node.Left);
        _builder.Append($" {node.NodeType} ");
        Visit(node.Right);
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
}
