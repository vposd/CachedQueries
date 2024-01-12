# CachedQueries: Enhancing EF Queries with Flexible Caching

[![License](http://img.shields.io/:license-mit-blue.svg)](https://vposd.mit-license.org/)
[![NuGet version (CachedQueries)](https://img.shields.io/nuget/v/CachedQueries.svg)](https://www.nuget.org/packages/CachedQueries/)
[![NuGet downloads (CachedQueries)](https://img.shields.io/nuget/dt/CachedQueries.svg)](https://www.nuget.org/packages/CachedQueries/)
[![Build status](https://github.com/vposd/CachedQueries/actions/workflows/release.yml/badge.svg)]()
[![Coverage Status](https://coveralls.io/repos/github/vposd/CachedQueries/badge.svg?branch=master)](https://coveralls.io/github/vposd/CachedQueries?branch=master)
[![CodeFactor](https://www.codefactor.io/repository/github/vposd/cachedqueries/badge)](https://www.codefactor.io/repository/github/vposd/cachedqueries)

## Introduction

CachedQueries is a robust library designed for caching IQueryable results in Entity Framework with intelligent invalidation. It allows for efficient caching directly with EF queries, eliminating the need for additional abstraction over DbSet.

## Key Features:

- **Smart Caching**: Automatically caches EF query results, enhancing performance.
- **Flexible Invalidation**: Cached results are refreshed only when relevant data changes.
- **Seamless Integration**: Works directly with existing EF queries and DbSet.
- **Customizable Options**: Supports custom settings for cache keys, invalidation rules, and more.

## Getting Started

1. Install package

```
dotnet add package CachedQueries
```

2. Dependency Injection Setup (example using memory cache):

```c#
// Setup system cache
services.AddMemoryCache();

// Add CachedQueries to your services
services.AddQueriesCaching(options =>
    options
        .UseCacheStore(MemoryCache)
        .UseEntityFramework());

// Use CachedQueries in your application
app.UseQueriesCaching();

```

3. Cache Invalidation Integration

```c#
// Extend SaveChanges and SaveChangesAsync methods in EF context
public override async Task<int> SaveChangesAsync(CancellationToken token = default)
{
    // Invoke cache expiration
    await ChangeTracker.ExpireEntitiesCacheAsync(token);
    return await base.SaveChangesAsync(token);
}
```

## Basic Usage

### Cache collection

Easily cache collections, including related data:

```c#
// Standard caching
var results = await context.Blogs
    .Include(x => x.Posts)
    .ToCachedListAsync(cancellationToken);

// Caching with expiration
var results = await context.Posts
    .ToCachedListAsync(TimeSpan.FromHours(8), cancellationToken);

// Caching with custom tags
var results = await context.Posts
    .ToCachedListAsync(TimeSpan.FromHours(8), new List<string> { "custom_tag" }, cancellationToken);
```

### Caching Individual Items

Cache single entities:

```c#
// Cache a single entity based on a condition
var result = await context.Blogs
    .Include(x => x.Posts)
    .CachedFirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

// Cache an entity with a predetermined expiration time
var result = await context.Posts
    .CachedFirstOrDefaultAsync(TimeSpan.FromHours(8), cancellationToken);

// Cache using custom tags for nuanced control
var result = await context.Posts
    .CachedFirstOrDefaultAsync(TimeSpan.FromHours(8), new List<string> { "custom_tag" }, cancellationToken);
```

## Invalidating Cache

CachedQueries efficiently handles cache invalidation, maintaining data accuracy and relevance.

### Auto Invalidation

To integrate automatic cache invalidation within CachedQueries, it is efficient to override the EF context `SaveChanges` and `SaveChangesAsync` methods.

```c#
public override async Task<int> SaveChangesAsync(CancellationToken token = default)
{
    await ChangeTracker.ExpireEntitiesCacheAsync(token);
    return base.SaveChangesAsync(token);
}
```

### Manual Invalidation Using Custom Tags

Control cache updates in case using custom tags:

```c#
// Setup the cache invalidator (typically done during initialization)
private ICacheInvalidator _cacheInvalidator;
...
// Invalidate specific cache segments by custom tag
_cacheInvalidator.InvalidateCacheAsync(new List<string> { "custom_tag" })
```

## Use Redis distributed Cache

Use Redis for scalable, distributed caching:

```c#
// Setup distributed cache
services.AddDistributedCache();
// Setup Redis
services.AddStackExchangeRedisCache(config);

// Add CachedQueries to your services
services.AddQueriesCaching(options =>
    options
        .UseCacheStore(DistributedCache)
        .UseLockManager<RedisLockManager>()
        .UseEntityFramework());

// Use CachedQueries in your application
app.UseQueriesCaching();
```

## More Customization Options

Customize key aspects for specific needs:

- **Cache Key Factory**: Allows for the implementation of unique logic in generating cache keys.
- **Lock Manager**: Customization of concurrent access and locking strategies for cache entries.
- **Cache Options**: Enables the setting and adjustment of global cache settings.
- **Cache Invalidator**: Provides the capability to devise specific rules for invalidating cache entries.

Custom Dependency Injection example:

```c#
services.AddQueriesCaching(options =>
    options
    .UseOptions(new CacheOptions {
        LockTimeout = TimeSpan.FromSeconds(10),
        DefaultExpiration = TimeSpan.FromMinutes(30)
    })
    .UseCacheStore(CustomCacheStore) // Custom ICacheStore implementation
    .UseCacheStoreProvider(CustomCacheStoreProvider) // Custom ICacheStoreProvider implementation
    .UseCacheInvalidator(CustomCacheInvalidator) // Custom ICacheInvalidator implementation
    .UseLockManager(CustomLockManager) // Custom ILockManager implementation
    .UseKeyFactory(CustomKeyFactory) // Custom ICacheKeyFactory implementation
    .UseEntityFramework()); // Integration with Entity Framework

// Activation of CachedQueries in applications
app.UseQueriesCaching();

```

## Conclusion

Discover more about CachedQueries through the library's test cases, offering insights into detailed functionalities and advanced usage examples.
