using System.Security.Cryptography;
using Lore.QueryCache.Interfaces;

namespace Lore.QueryCache;

public class CacheKeyFactory : ICacheKeyFactory
{
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