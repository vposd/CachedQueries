using System.Text.Json;
using System.Text.Json.Serialization;
using CachedQueries.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CachedQueries.Redis;

/// <summary>
///     Redis-based distributed cache provider with atomic tag operations.
///     When IConnectionMultiplexer is available, all operations go through it directly
///     to ensure consistent key handling and atomic tag operations via Lua scripts.
/// </summary>
public sealed class RedisCacheProvider : ICacheProvider
{
    // Lua script: SET data key + SADD to each tag set + set tag expiration — all atomic.
    // KEYS[1] = data key
    // ARGV[1] = serialized value
    // ARGV[2] = expiration in milliseconds (absolute) or 0 for sliding
    // ARGV[3] = sliding expiration in milliseconds or 0 for absolute
    // ARGV[4] = tag set expiration in milliseconds
    // ARGV[5..N] = tag keys (cq:tag:xxx)
    private const string SetWithTagsScript = @"
-- Store the cached data with TTL (sliding or absolute expiration)
if tonumber(ARGV[3]) > 0 then
    redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[3])
else
    redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2])
end

-- Register the cache key in each tag set so we can find it during invalidation
-- Tag sets live slightly longer than data (TTL + 5 min) to avoid premature cleanup
local tagExpMs = tonumber(ARGV[4])
for i = 5, #ARGV do
    redis.call('SADD', ARGV[i], KEYS[1])
    redis.call('PEXPIRE', ARGV[i], tagExpMs)
end
return 1";

    // KEYS[1..N] = tag keys (cq:tag:xxx)
    // Returns total number of deleted data keys.
    private const string InvalidateByTagsScript = @"
local total = 0
for i = 1, #KEYS do
    -- Get all cache keys registered under this tag
    local members = redis.call('SMEMBERS', KEYS[i])

    -- Delete all cached data for those keys
    if #members > 0 then
        redis.call('DEL', unpack(members))
        total = total + #members
    end

    -- Delete the tag set itself
    redis.call('DEL', KEYS[i])
end
return total";

    private readonly IDistributedCache _cache;
    private readonly string _clearPattern;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _keyPrefix;

    private readonly ILogger<RedisCacheProvider> _logger;
    private readonly IConnectionMultiplexer? _redis;
    private readonly string _tagPrefix;

    /// <summary>
    ///     Creates a new RedisCacheProvider with IDistributedCache only.
    ///     Tag operations (SET+SADD, invalidation) will NOT be atomic in this mode.
    ///     Use the constructor with IConnectionMultiplexer for production workloads.
    /// </summary>
    public RedisCacheProvider(IDistributedCache cache, ILogger<RedisCacheProvider> logger)
        : this(cache, null, logger, "", "cq")
    {
        _logger.LogWarning(
            "RedisCacheProvider created without IConnectionMultiplexer. " +
            "Tag operations will not be atomic. Use IConnectionMultiplexer for production workloads");
    }

    /// <summary>
    ///     Creates a new RedisCacheProvider with IConnectionMultiplexer (full mode, atomic tag operations).
    /// </summary>
    public RedisCacheProvider(
        IDistributedCache cache,
        IConnectionMultiplexer redis,
        ILogger<RedisCacheProvider> logger)
        : this(cache, redis, logger, "", "cq")
    {
    }

    /// <summary>
    ///     Creates a new RedisCacheProvider with IConnectionMultiplexer, key prefix, and cache prefix.
    /// </summary>
    /// <param name="cache">The distributed cache instance.</param>
    /// <param name="redis">The Redis connection multiplexer (nullable for fallback mode).</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="keyPrefix">Key prefix from Redis InstanceName (e.g., "CacheLocal:").</param>
    /// <param name="cachePrefix">Library namespace prefix from CachedQueriesConfiguration.CachePrefix (e.g., "cq").</param>
    public RedisCacheProvider(
        IDistributedCache cache,
        IConnectionMultiplexer? redis,
        ILogger<RedisCacheProvider> logger,
        string keyPrefix,
        string cachePrefix)
    {
        _cache = cache;
        _redis = redis;
        _logger = logger;
        _keyPrefix = keyPrefix;
        _tagPrefix = keyPrefix + cachePrefix + ":";
        _clearPattern = keyPrefix + cachePrefix + ":*";
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var prefixedKey = _keyPrefix + key;
            string? data;

            if (_redis is not null)
            {
                var db = _redis.GetDatabase();
                data = await db.StringGetAsync(prefixedKey);
            }
            else
            {
                data = await _cache.GetStringAsync(prefixedKey, cancellationToken);
            }

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

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, CachingOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = JsonSerializer.Serialize(value, _jsonOptions);

            var prefixedKey = _keyPrefix + key;

            if (_redis is not null && options.Tags.Count > 0)
            {
                // Atomic: SET data + SADD all tags in one Lua script
                await SetWithTagsAtomicAsync(prefixedKey, data, options);
            }
            else if (_redis is not null)
            {
                // No tags — simple SET via native Redis
                var db = _redis.GetDatabase();
                var expiry = options.Expiration;
                await db.StringSetAsync(prefixedKey, data, expiry);
            }
            else
            {
                // Fallback: IDistributedCache only — tag operations are NOT atomic
                if (options.Tags.Count > 0)
                {
                    _logger.LogWarning(
                        "Non-atomic tag registration for key {CacheKey}. " +
                        "Use IConnectionMultiplexer for atomic tag operations", prefixedKey);
                }

                var distributedOptions = new DistributedCacheEntryOptions();
                if (options.UseSlidingExpiration)
                {
                    distributedOptions.SlidingExpiration = options.Expiration;
                }
                else
                {
                    distributedOptions.AbsoluteExpirationRelativeToNow = options.Expiration;
                }

                await _cache.SetStringAsync(prefixedKey, data, distributedOptions, cancellationToken);

                foreach (var tag in options.Tags)
                {
                    await AddKeyToTagFallbackAsync(tag, prefixedKey, options.Expiration);
                }
            }

            _logger.LogDebug("Cached value for key: {CacheKey}, Expiration: {Expiration}", key, options.Expiration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set cache value for key: {CacheKey}", key);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var prefixedKey = _keyPrefix + key;

            if (_redis is not null)
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(prefixedKey);
            }
            else
            {
                await _cache.RemoveAsync(prefixedKey, cancellationToken);
            }

            _logger.LogDebug("Removed cache key: {CacheKey}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove cache key: {CacheKey}", key);
        }
    }

    /// <inheritdoc />
    public async Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var tagsList = tags.ToList();
        if (tagsList.Count == 0)
        {
            return;
        }

        var invalidatedCount = 0;

        try
        {
            if (_redis is not null)
            {
                // Atomic: read all tag sets, delete data keys, delete tag keys — one round-trip
                invalidatedCount = await InvalidateByTagsAtomicAsync(tagsList);
            }
            else
            {
                invalidatedCount = await InvalidateByTagsFallbackAsync(tagsList, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache by tags");
        }

        if (invalidatedCount > 0)
        {
            _logger.LogInformation("Invalidated {Count} cache entries by tags", invalidatedCount);
        }
    }

    /// <inheritdoc />
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

                    // Delete only keys with our prefix (data keys and tag keys)
                    var keys = server.Keys(pattern: _clearPattern).ToArray();
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

    private async Task SetWithTagsAtomicAsync(string key, string data, CachingOptions options)
    {
        var db = _redis!.GetDatabase();

        var expirationMs = (long)options.Expiration.TotalMilliseconds;
        var slidingMs = options.UseSlidingExpiration ? expirationMs : 0L;
        var absoluteMs = options.UseSlidingExpiration ? 0L : expirationMs;
        var tagExpirationMs = (long)(options.Expiration + TimeSpan.FromMinutes(5)).TotalMilliseconds;

        // ARGV: value, absoluteMs, slidingMs, tagExpirationMs, tagKey1, tagKey2, ...
        var argv = new RedisValue[4 + options.Tags.Count];
        argv[0] = data;
        argv[1] = absoluteMs;
        argv[2] = slidingMs;
        argv[3] = tagExpirationMs;

        var i = 4;
        foreach (var tag in options.Tags)
        {
            argv[i++] = _tagPrefix + tag;
        }

        await db.ScriptEvaluateAsync(SetWithTagsScript, [(RedisKey)key], argv);
    }

    private async Task<int> InvalidateByTagsAtomicAsync(List<string> tags)
    {
        var db = _redis!.GetDatabase();

        var tagKeys = tags.Select(t => (RedisKey)(_tagPrefix + t)).ToArray();
        var result = await db.ScriptEvaluateAsync(InvalidateByTagsScript, tagKeys);

        return (int)result;
    }

    private async Task<int> InvalidateByTagsFallbackAsync(List<string> tags, CancellationToken cancellationToken)
    {
        var invalidatedCount = 0;

        foreach (var tag in tags)
        {
            invalidatedCount += await InvalidateTagFallbackAsync(tag, cancellationToken);
        }

        return invalidatedCount;
    }

    private async Task<int> InvalidateTagFallbackAsync(string tag, CancellationToken cancellationToken)
    {
        try
        {
            var tagKey = _tagPrefix + tag;
            var keysData = await _cache.GetStringAsync(tagKey, cancellationToken);
            if (keysData is null)
            {
                return 0;
            }

            var keys = JsonSerializer.Deserialize<HashSet<string>>(keysData, _jsonOptions) ?? [];
            foreach (var key in keys)
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }

            await _cache.RemoveAsync(tagKey, cancellationToken);
            return keys.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache by tag: {Tag}", tag);
            return 0;
        }
    }

    private async Task AddKeyToTagFallbackAsync(string tag, string key, TimeSpan expiration)
    {
        var tagKey = _tagPrefix + tag;

        try
        {
            var existingData = await _cache.GetStringAsync(tagKey);
            var keys = existingData is not null
                ? JsonSerializer.Deserialize<HashSet<string>>(existingData, _jsonOptions) ?? []
                : [];

            keys.Add(key);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration + TimeSpan.FromMinutes(5)
            };

            await _cache.SetStringAsync(tagKey, JsonSerializer.Serialize(keys, _jsonOptions), options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add key to tag: {Tag}", tag);
        }
    }
}
