﻿using System.Security.Cryptography;
using System.Text;
using CachedQueries.Core.Abstractions;

namespace CachedQueries.Core;

/// <summary>
///     Base class for cache key factory.
/// </summary>
public class DefaultCacheKeyFactory : ICacheKeyFactory
{
    public virtual string GetCacheKey<T>(IQueryable<T> query, string[] tags)
    {
        var command = string.Join('_', tags.ToList());
        return GetStringSha256Hash(command);
    }

    protected static string GetStringSha256Hash(string text)
    {
        var textData = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(textData);
        return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.InvariantCulture);
    }
}