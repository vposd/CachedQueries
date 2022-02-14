﻿using System.Security.Cryptography;
using Lore.QueryCache.Interfaces;

namespace Lore.QueryCache;

/// <summary>
/// Base class for cache key factory.
/// </summary>
public class CacheKeyFactory : ICacheKeyFactory
{
    /// <summary>
    /// Returns cache key as hash of query string plus joined tags
    /// </summary>
    /// <param name="query">Query param</param>
    /// <param name="tags">Linking tags for further invalidation</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>The cache key</returns>
    public virtual string GetCacheKey<T>(IQueryable<T> query, IEnumerable<string> tags) where T : class
    {
        var command = string.Join('_', tags.ToList());
        return GetStringSha256Hash(command);
    }

    protected static string GetStringSha256Hash(string text)
    {
        using var sha = SHA256.Create();
        var textData = System.Text.Encoding.UTF8.GetBytes(text);
        var hash = sha.ComputeHash(textData);
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }
}