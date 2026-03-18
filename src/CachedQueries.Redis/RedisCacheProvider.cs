using System.Text.Json;
using CachedQueries.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CachedQueries.Redis;

/// <summary>
/// Redis-based distributed cache provider with atomic tag operations.
/// </summary>
public sealed class RedisCacheProvider : ICacheProvider
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisCacheProvider> _logger;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private const string TagPrefix = "cq:tag:";
    private const string KeyPrefix = "cq:";

    /// <summary>
    /// Creates a new RedisCacheProvider with IDistributedCache (basic mode, no atomic tag operations).
    /// </summary>
    public RedisCacheProvider(IDistributedCache cache, ILogger<RedisCacheProvider> logger)
    {
        _cache = cache;
        _logger = logger;
        _redis = null;
    }

    /// <summary>
    /// Creates a new RedisCacheProvider with IConnectionMultiplexer (full mode, atomic tag operations).
    /// </summary>
    public RedisCacheProvider(
        IDistributedCache cache,
        IConnectionMultiplexer redis,
        ILogger<RedisCacheProvider> logger)
    {
        _cache = cache;
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await _cache.GetStringAsync(key, cancellationToken);

            if (data is null)
            {
                _logger.LogDebug("Cache miss for key: {CacheKey}", key);
                return default;
            }

            _logger.LogDebug("Cache hit for key: {CacheKey}", key);
            return JsonSerializer.Deserialize<T>(data, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cache value for key: {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, CachingOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = JsonSerializer.Serialize(value, _jsonOptions);

            var distributedOptions = new DistributedCacheEntryOptions();

            if (options.UseSlidingExpiration)
            {
                distributedOptions.SlidingExpiration = options.Expiration;
            }
            else
            {
                distributedOptions.AbsoluteExpirationRelativeToNow = options.Expiration;
            }

            await _cache.SetStringAsync(key, data, distributedOptions, cancellationToken);

            // Store tag associations atomically
            foreach (var tag in options.Tags)
            {
                await AddKeyToTagAsync(tag, key, options.Expiration);
            }

            _logger.LogDebug("Cached value for key: {CacheKey}, Expiration: {Expiration}", key, options.Expiration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set cache value for key: {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
            _logger.LogDebug("Removed cache key: {CacheKey}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove cache key: {CacheKey}", key);
        }
    }

    public async Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var invalidatedCount = 0;

        foreach (var tag in tags)
        {
            try
            {
                var tagKey = TagPrefix + tag;

                if (_redis is not null)
                {
                    // Atomic operation using Redis Sets
                    var db = _redis.GetDatabase();
                    var keys = await db.SetMembersAsync(tagKey);

                    if (keys.Length > 0)
                    {
                        var redisKeys = keys
                            .Where(k => k.HasValue)
                            .Select(k => (RedisKey)k.ToString())
                            .ToArray();

                        if (redisKeys.Length > 0)
                        {
                            await db.KeyDeleteAsync(redisKeys);
                            invalidatedCount += redisKeys.Length;
                        }

                        await db.KeyDeleteAsync(tagKey);
                    }
                }
                else
                {
                    // Fallback for IDistributedCache-only mode
                    var keysData = await _cache.GetStringAsync(tagKey, cancellationToken);

                    if (keysData is not null)
                    {
                        var keys = JsonSerializer.Deserialize<HashSet<string>>(keysData, _jsonOptions);

                        if (keys is not null)
                        {
                            foreach (var key in keys)
                            {
                                await _cache.RemoveAsync(key, cancellationToken);
                                invalidatedCount++;
                            }
                        }

                        await _cache.RemoveAsync(tagKey, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate cache by tag: {Tag}", tag);
            }
        }

        if (invalidatedCount > 0)
        {
            _logger.LogInformation("Invalidated {Count} cache entries by tags", invalidatedCount);
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (_redis is not null)
        {
            try
            {
                var db = _redis.GetDatabase();
                var endpoints = _redis.GetEndPoints();

                foreach (var endpoint in endpoints)
                {
                    var server = _redis.GetServer(endpoint);

                    // Delete only keys with our prefix
                    var keys = server.Keys(pattern: $"{KeyPrefix}*").ToArray();
                    if (keys.Length > 0)
                    {
                        await db.KeyDeleteAsync(keys);
                        _logger.LogInformation("Cleared {Count} cache entries", keys.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear cache");
            }
        }
        else
        {
            _logger.LogWarning("ClearAsync requires IConnectionMultiplexer. Consider using Redis CLI: FLUSHDB");
        }
    }

    private async Task AddKeyToTagAsync(string tag, string key, TimeSpan expiration)
    {
        var tagKey = TagPrefix + tag;

        try
        {
            if (_redis is not null)
            {
                // Atomic SADD operation - no race condition
                var db = _redis.GetDatabase();
                await db.SetAddAsync(tagKey, key);

                // Set expiration on tag set (slightly longer than cache entry)
                await db.KeyExpireAsync(tagKey, expiration + TimeSpan.FromMinutes(5));
            }
            else
            {
                // Fallback: non-atomic operation (acceptable for most use cases)
                var existingData = await _cache.GetStringAsync(tagKey);
                var keys = existingData is not null
                    ? JsonSerializer.Deserialize<HashSet<string>>(existingData, _jsonOptions) ?? []
                    : new HashSet<string>();

                keys.Add(key);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration + TimeSpan.FromMinutes(5)
                };

                await _cache.SetStringAsync(tagKey, JsonSerializer.Serialize(keys, _jsonOptions), options);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add key to tag: {Tag}", tag);
        }
    }
}
