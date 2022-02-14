namespace Lore.QueryCache.Interfaces;

public interface ICache
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expire = null);
    Task DeleteAsync(string key);
}