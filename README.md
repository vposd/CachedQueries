# CachedQueries: Efficient Caching for Entity Framework Queries

[![License](https://img.shields.io/:license-mit-blue.svg)](https://vposd.mit-license.org/)
[![NuGet version (CachedQueries)](https://img.shields.io/nuget/v/CachedQueries.svg)](https://www.nuget.org/packages/CachedQueries/)
[![NuGet downloads (CachedQueries)](https://img.shields.io/nuget/dt/CachedQueries.svg)](https://www.nuget.org/packages/CachedQueries/)
[![Build status](https://github.com/vposd/CachedQueries/actions/workflows/release.yml/badge.svg)]()
[![Coverage Status](https://coveralls.io/repos/github/vposd/CachedQueries/badge.svg?branch=master)](https://coveralls.io/github/vposd/CachedQueries?branch=master)
[![CodeFactor](https://www.codefactor.io/repository/github/vposd/cachedqueries/badge)](https://www.codefactor.io/repository/github/vposd/cachedqueries)

## Introduction

CachedQueries is a .NET library designed to seamlessly integrate caching into Entity Framework (EF) queries. It simplifies caching IQueryable results directly within EF, removing the need for additional layers, while ensuring efficient data retrieval and cache invalidation.


## Installation

To install CachedQueries, run the following command:

```
dotnet add package CachedQueries
```

## Setup and Configuration

1. Configure Dependency Injection:

```csharp
// Add system cache
services.AddMemoryCache();

// Add CachedQueries
services.AddQueriesCaching(options =>
    options
        .UseCacheStore<MemoryCache>()   // Configure cache store
        .UseEntityFramework()           // Integrate with EF
);

// Activate caching in your application
app.UseQueriesCaching();

```

2. Cache Invalidation in EF Context:

To automatically invalidate cache entries when data changes, extend your EF context:
```csharp
// Override SaveChanges and SaveChangesAsync methods in EF context
public override async Task<int> SaveChangesAsync(CancellationToken token = default)
{
    await ChangeTracker.ExpireEntitiesCacheAsync(token);   // Trigger cache invalidation
    return await base.SaveChangesAsync(token);
}
```

## Basic Usage

### Caching Queries

Cache collections or individual entities with optional expiration:

```csharp
// Cache collections
var blogs = await context.Blogs.Include(b => b.Posts)
                               .ToListCachedAsync(cancellationToken);

// Set custom expiration (e.g., 4 hours)
var posts = await context.Posts.ToListCachedAsync(new CachingOptions { CachedDuration = TimeSpan.FromHours(4) }, cancellationToken);

// Cache single entity
var blog = await context.Blogs.CachedFirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

```

## Manual Cache Invalidation

Manually control cache invalidation with custom tags:
```csharp
// Initialize cache invalidator
private ICacheInvalidator _cacheInvalidator;

// Invalidate cache with tags
await _cacheInvalidator.InvalidateCacheAsync(new List<string> { "custom_tag" });

```

## Distributed Caching with Redis

For distributed environments, you can use Redis as your caching solution:

```csharp
// Add Redis distributed cache
services.AddStackExchangeRedisCache(config);

// Configure CachedQueries with Redis
services.AddQueriesCaching(options =>
    options
        .UseCacheStore<DistributedCache>()         // Use distributed cache
        .UseEntityFramework()                      // Integrate with EF
);
```

## Advanced Configuration

CachedQueries offers flexible configuration options, allowing you to customize various components:

### Custom Cache Stores
 You can implement your own cache store by providing a class that implements the ICacheStore interface. This allows you to manage caching according to your custom rules, including data storage and retrieval.

 ```csharp
 // Custom cache store implementation
services.AddQueriesCaching(options =>
    options
    .UseCacheStore<CustomCacheStore>() // Custom ICacheStore implementation
    .UseEntityFramework());
```

### Custom Cache Invalidators
To control when and how cached data is invalidated, you can implement a custom cache invalidator. This is useful for scenarios where invalidation needs to follow specific business rules or depend on external conditions.

 ```csharp
// Custom cache invalidator setup
services.AddQueriesCaching(options =>
    options
    .UseCacheInvalidator<CustomCacheInvalidator>() // Custom ICacheInvalidator implementation
    .UseEntityFramework());
```

### Cache Strategies
You can define custom strategies for both caching collections and individual entries. CachedQueries enables you to specify how data is stored, retrieved, and expired.

Cache Collection Strategy: Manages the caching behavior for collections, such as lists or arrays of entities.
Cache Entry Strategy: Manages the caching behavior for individual entities.
To implement custom strategies, provide classes that inherit from `ICacheCollectionStrategy` and `ICacheEntryStrategy`.

```csharp
// Custom cache strategies setup
services.AddQueriesCaching(options =>
    options
    .UseCacheCollectionStrategy<CustomCollectionStrategy>() // Custom ICacheCollectionStrategy implementation
    .UseCacheEntryStrategy<CustomEntryStrategy>() // Custom ICacheEntryStrategy implementation
    .UseEntityFramework());

```

### Cache Key Factory
The cache key factory determines how cache keys are generated. CachedQueries allows you to define custom key generation logic, which can be useful if your application has unique keying requirements for different types of queries.

```csharp
// Custom cache key factory setup
services.AddQueriesCaching(options =>
    options
    .UseKeyFactory<CustomKeyFactory>() // Custom ICacheKeyFactory implementation
    .UseEntityFramework());
```

### Custom Cache Context Provider
CachedQueries allows for the use of a custom `ICacheContextProvider` to manage context-specific caching. This is useful when cache keys need to be unique across different contexts, such as tenants, users, or environments.

```csharp
// Example of a custom cache context provider
public class TenantCacheContextProvider : ICacheContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantCacheContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetContextKey()
    {
        // Example: Use tenant ID from the HTTP context
        return _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id")?.Value ?? "default_tenant";
    }
}
```

Setup a custom cache context provider:
```csharp
// Custom cache key factory setup
services.AddQueriesCaching(options =>
    options
    .UseCacheContextProvider<TenantCacheContextProvider>()
    .UseEntityFramework());
```

## Custom Dependency Injection Example
Here's an example of how you can configure CachedQueries with various custom strategies and services:

```csharp
services.AddQueriesCaching(options =>
    options
    .UseCacheStore<CustomCacheStore>() // Custom cache store
    .UseCacheInvalidator<CustomCacheInvalidator>() // Custom invalidator
    .UseCacheCollectionStrategy<CustomCollectionStrategy>() // Custom collection strategy
    .UseCacheEntryStrategy<CustomEntryStrategy>() // Custom entry strategy
    .UseKeyFactory<CustomKeyFactory>() // Custom key factory
    .UseCacheContextProvider<CustomContextProvider>()
    .UseOptions(new CacheOptions
    {
        DefaultExpiration = TimeSpan.FromMinutes(30),
    })
    .UseEntityFramework());
```

## Conclusion

CachedQueries is designed to streamline caching within your Entity Framework queries, offering a highly customizable framework for optimizing data access. With support for custom stores, strategies, key factories, and invalidators, it can be extended to meet complex caching requirements.

To dive deeper into the functionality and explore more advanced examples, check the library’s test cases and example project in `./examples`.