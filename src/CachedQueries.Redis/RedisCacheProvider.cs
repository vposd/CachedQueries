using System.Globalization;
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
/// <remarks>
///     <list type="bullet">
///         <item>
///             Values are serialized with System.Text.Json (camelCase, <c>ReferenceHandler.IgnoreCycles</c>): cyclic
///             back-references are dropped, and types must be deserializable by System.Text.Json. Deserialization
///             failures are logged and treated as cache misses.
///         </item>
///         <item>
///             Each tag is a Redis set of data keys. Its TTL is only ever extended (to the longest-lived member's
///             expiration + 5 minutes), so a short-lived entry cannot make the tag set expire under a long-lived one.
///         </item>
///         <item>
///             Sliding expiration (multiplexer mode): a cache hit refreshes the entry's TTL and extends its tag sets
///             (one extra fire-and-forget command per hit on a sliding entry).
///         </item>
///         <item>
///             The Lua scripts touch keys across hash slots, so <b>Redis Cluster is not supported</b> in multiplexer
///             mode. Use a standalone or replicated (primary/replica, Sentinel) deployment.
///         </item>
///         <item>
///             Fallback mode (IDistributedCache only) is not atomic and its tag sets use absolute expiration;
///             it is intended for development, not production.
///         </item>
///     </list>
/// </remarks>
public sealed class RedisCacheProvider : ICacheProvider
{
    // Lua script: SET data key + SADD to each tag set + extend tag expiration — all atomic.
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

-- Register the cache key in each tag set so we can find it during invalidation.
-- Tag sets live slightly longer than data (TTL + 5 min). Their TTL is only ever extended:
-- shortening it would let the set expire while longer-lived members are still cached.
local tagExpMs = tonumber(ARGV[4])
for i = 5, #ARGV do
    redis.call('SADD', ARGV[i], KEYS[1])
    if redis.call('PTTL', ARGV[i]) < tagExpMs then
        redis.call('PEXPIRE', ARGV[i], tagExpMs)
    end
end
return 1";

    // Refreshes a sliding entry on cache hit.
    // KEYS[1] = data key, KEYS[2..N] = its tag keys
    // ARGV[1] = sliding window in milliseconds, ARGV[2] = tag set expiration in milliseconds
    private const string RefreshSlidingScript = @"
if redis.call('PEXPIRE', KEYS[1], ARGV[1]) == 1 then
    local tagExpMs = tonumber(ARGV[2])
    for i = 2, #KEYS do
        if redis.call('PTTL', KEYS[i]) < tagExpMs then
            redis.call('PEXPIRE', KEYS[i], tagExpMs)
        end
    end
end
return 1";

    // KEYS[1..N] = tag keys (cq:tag:xxx)
    // Returns total number of deleted data keys.
    private const string InvalidateByTagsScript = @"
local total = 0
for i = 1, #KEYS do
    -- Get all cache keys registered under this tag
    local members = redis.call('SMEMBERS', KEYS[i])

    -- Delete all cached data for those keys, in chunks: unpack() fails beyond ~8000 values
    for j = 1, #members, 1000 do
        redis.call('DEL', unpack(members, j, math.min(j + 999, #members)))
    end
    total = total + #members

    -- Delete the tag set itself
    redis.call('DEL', KEYS[i])
end
return total";

    // Marks a sliding entry: "\u0001{windowMs}\u0001{tagKey}\u0002{tagKey}...\u0001{json}".
    // JSON never contains a raw U+0001, so absolute entries (plain JSON) are unambiguous.
    private const char SlidingHeaderMarker = '\u0001';
    private const char SlidingTagSeparator = '\u0002';
    private static readonly TimeSpan TagExpirationMargin = TimeSpan.FromMinutes(5);

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
                data = await db.StringGetAsync(prefixedKey).ConfigureAwait(false);
                if (data is not null && TryParseSlidingEntry(data, out var windowMs, out var tagKeys, out var json))
                {
                    data = json;
                    RefreshSliding(db, prefixedKey, windowMs, tagKeys);
                }
            }
            else
            {
                data = await _cache.GetStringAsync(prefixedKey, cancellationToken).ConfigureAwait(false);
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

            if (_redis is not null)
            {
                var tagKeys = options.Tags.Select(tag => _tagPrefix + tag).ToArray();
                if (options.UseSlidingExpiration)
                {
                    data = BuildSlidingEntry((long)options.Expiration.TotalMilliseconds, tagKeys, data);
                }

                if (tagKeys.Length > 0)
                {
                    // Atomic: SET data + SADD all tags in one Lua script
                    await SetWithTagsAtomicAsync(prefixedKey, data, options, tagKeys).ConfigureAwait(false);
                }
                else
                {
                    // No tags — simple SET via native Redis
                    var db = _redis.GetDatabase();
                    await db.StringSetAsync(prefixedKey, data, options.Expiration).ConfigureAwait(false);
                }
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

                await _cache.SetStringAsync(prefixedKey, data, distributedOptions, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var tag in options.Tags)
                {
                    await AddKeyToTagFallbackAsync(tag, prefixedKey, options.Expiration).ConfigureAwait(false);
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
                await db.KeyDeleteAsync(prefixedKey).ConfigureAwait(false);
            }
            else
            {
                await _cache.RemoveAsync(prefixedKey, cancellationToken).ConfigureAwait(false);
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
                invalidatedCount = await InvalidateByTagsAtomicAsync(tagsList).ConfigureAwait(false);
            }
            else
            {
                invalidatedCount = await InvalidateByTagsFallbackAsync(tagsList, cancellationToken)
                    .ConfigureAwait(false);
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
    /// <remarks>
    ///     Deletes every key matching <c>{InstanceName}{CachePrefix}:*</c> on each primary endpoint (uses SCAN).
    ///     Requires IConnectionMultiplexer; in fallback mode only a warning is logged.
    /// </remarks>
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
                    if (server.IsReplica)
                    {
                        // Replicas hold copies of the primary's keys; deleting them on the primary is enough.
                        continue;
                    }

                    // Delete only keys with our prefix (data keys and tag keys)
                    var keys = server.Keys(pattern: _clearPattern).ToArray();
                    if (keys.Length > 0)
                    {
                        await db.KeyDeleteAsync(keys).ConfigureAwait(false);
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

    internal static string BuildSlidingEntry(long windowMs, string[] tagKeys, string json)
    {
        return string.Concat(
            SlidingHeaderMarker.ToString(), windowMs.ToString(CultureInfo.InvariantCulture),
            SlidingHeaderMarker.ToString(), string.Join(SlidingTagSeparator, tagKeys),
            SlidingHeaderMarker.ToString(), json);
    }

    internal static bool TryParseSlidingEntry(string data, out long windowMs, out string[] tagKeys, out string json)
    {
        windowMs = 0;
        tagKeys = [];
        json = data;

        if (data.Length == 0 || data[0] != SlidingHeaderMarker)
        {
            return false;
        }

        var windowEnd = data.IndexOf(SlidingHeaderMarker, 1);
        var jsonStart = data.LastIndexOf(SlidingHeaderMarker) + 1;
        if (windowEnd < 0 || jsonStart <= windowEnd + 1 ||
            !long.TryParse(data.AsSpan(1, windowEnd - 1), NumberStyles.None, CultureInfo.InvariantCulture,
                out windowMs))
        {
            return false;
        }

        var tags = data[(windowEnd + 1)..(jsonStart - 1)];
        tagKeys = tags.Length == 0 ? [] : tags.Split(SlidingTagSeparator);
        json = data[jsonStart..];
        return true;
    }

    private async Task SetWithTagsAtomicAsync(string key, string data, CachingOptions options, string[] tagKeys)
    {
        var db = _redis!.GetDatabase();

        var expirationMs = (long)options.Expiration.TotalMilliseconds;
        var slidingMs = options.UseSlidingExpiration ? expirationMs : 0L;
        var absoluteMs = options.UseSlidingExpiration ? 0L : expirationMs;
        var tagExpirationMs = (long)(options.Expiration + TagExpirationMargin).TotalMilliseconds;

        // ARGV: value, absoluteMs, slidingMs, tagExpirationMs, tagKey1, tagKey2, ...
        var argv = new RedisValue[4 + tagKeys.Length];
        argv[0] = data;
        argv[1] = absoluteMs;
        argv[2] = slidingMs;
        argv[3] = tagExpirationMs;

        for (var i = 0; i < tagKeys.Length; i++)
        {
            argv[4 + i] = tagKeys[i];
        }

        await db.ScriptEvaluateAsync(SetWithTagsScript, [(RedisKey)key], argv).ConfigureAwait(false);
    }

    private void RefreshSliding(IDatabase db, string key, long windowMs, string[] tagKeys)
    {
        var keys = new RedisKey[1 + tagKeys.Length];
        keys[0] = key;
        for (var i = 0; i < tagKeys.Length; i++)
        {
            keys[1 + i] = tagKeys[i];
        }

        var tagExpirationMs = windowMs + (long)TagExpirationMargin.TotalMilliseconds;

        // Fire-and-forget: the hit is served immediately, the refresh does not add latency.
        db.ScriptEvaluateAsync(RefreshSlidingScript, keys, [windowMs, tagExpirationMs], CommandFlags.FireAndForget);
        _logger.LogDebug("Refreshed sliding expiration for key: {CacheKey}", key);
    }

    private async Task<int> InvalidateByTagsAtomicAsync(List<string> tags)
    {
        var db = _redis!.GetDatabase();

        var tagKeys = tags.Select(t => (RedisKey)(_tagPrefix + t)).ToArray();
        var result = await db.ScriptEvaluateAsync(InvalidateByTagsScript, tagKeys).ConfigureAwait(false);

        return (int)result;
    }

    private async Task<int> InvalidateByTagsFallbackAsync(List<string> tags, CancellationToken cancellationToken)
    {
        var invalidatedCount = 0;

        foreach (var tag in tags)
        {
            invalidatedCount += await InvalidateTagFallbackAsync(tag, cancellationToken).ConfigureAwait(false);
        }

        return invalidatedCount;
    }

    private async Task<int> InvalidateTagFallbackAsync(string tag, CancellationToken cancellationToken)
    {
        try
        {
            var tagKey = _tagPrefix + tag;
            var keysData = await _cache.GetStringAsync(tagKey, cancellationToken).ConfigureAwait(false);
            if (keysData is null)
            {
                return 0;
            }

            var keys = JsonSerializer.Deserialize<HashSet<string>>(keysData, _jsonOptions) ?? [];
            foreach (var key in keys)
            {
                await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }

            await _cache.RemoveAsync(tagKey, cancellationToken).ConfigureAwait(false);
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
            var existingData = await _cache.GetStringAsync(tagKey).ConfigureAwait(false);
            var keys = existingData is not null
                ? JsonSerializer.Deserialize<HashSet<string>>(existingData, _jsonOptions) ?? []
                : [];

            keys.Add(key);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration + TagExpirationMargin
            };

            await _cache.SetStringAsync(tagKey, JsonSerializer.Serialize(keys, _jsonOptions), options)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add key to tag: {Tag}", tag);
        }
    }
}
