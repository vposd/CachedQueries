using System.Text.Json;
using System.Text.Json.Serialization;
using CachedQueries.Core.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CachedQueries.Core.Cache;

/// <summary>
///     Cache service using IDistributedCache implementation.
/// </summary>
public class DistributedCache(
    IDistributedCache cache,
    ILoggerFactory loggerFactory)
    : ICacheStore
{
    private readonly ILogger<DistributedCache> _logger = loggerFactory.CreateLogger<DistributedCache>();

    private readonly JsonSerializerOptions _settings = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError("Error delete cached data: @{Message}", exception.Message);
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var cachedResponse = await cache.GetAsync(key, cancellationToken);

            return cachedResponse is not null
                ? JsonSerializer.Deserialize<T>(cachedResponse, _settings)
                : default;
        }
        catch (Exception exception)
        {
            _logger.LogError("Error loading cached data: @{Message}", exception.Message);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expire = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = JsonSerializer.SerializeToUtf8Bytes(value, _settings);

            await cache.SetAsync(
                key,
                response,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expire },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError("Error setting cached data: @{Message}", exception.Message);
        }
    }
}
