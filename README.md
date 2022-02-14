# Lore.QueryCache

A library provides IQueryable results caching using IMemoryCache or IDistributedCache.

## Setup

Setup with DI

```c#
services.AddLoreCache(options =>
    options
        .UseCache<DistributedCache>()
        .UseEntityFramework());

...

app.UseLoreCache();
```

Or setup with static class

```c#
CacheManager.Cache = new MyCacheImplementation();
CacheManager.CacheKeyFactory = new MyCacheKeyFactory();

...

app.UseLoreCache();
```

## Usage

Cache collection

```c#
var results = context.Blogs
    .Include(x => x.Posts)
    .ToCachedListAsync(
        cancellationToken,
        tags: new List<string> { nameof(Blog), nameof(Post)});
```

Cache item

```c#
var results = context.Blogs
    .Include(x => x.Posts)
    .Where(x => x.Id == request.Id)
    .CachedFirstOrDefault(
        cancellationToken,
        tags: new List<string> { nameof(Blog), nameof(Post)});
```

Invalidate tags

```c#
CacheManager.ExpireTagsAsync(new List<string> { nameof(Blog), nameof(Post)});
```
