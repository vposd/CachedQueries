# CachedQueries

[![License](http://img.shields.io/:license-mit-blue.svg)](https://vposd.mit-license.org/)
[![Build status](https://ci.appveyor.com/api/projects/status/qbpx3i8eq3cl05f1?svg=true)](https://ci.appveyor.com/project/vposd/lore-querycache)
[![Coverage Status](https://coveralls.io/repos/github/vposd/Lore.QueryCache/badge.svg?branch=master)](https://coveralls.io/github/vposd/Lore.QueryCache?branch=master)
[![CodeFactor](https://www.codefactor.io/repository/github/vposd/cachedqueries/badge)](https://www.codefactor.io/repository/github/vposd/cachedqueries)

A library provides IQueryable results caching with smart invalidation.

## Setup

Setup with DI

```c#
services.AddQueriesCaching(options =>
    options
        .UseCache<DistributedCache>()
        .UseEntityFramework());

...

app.UseQueriesCaching();
```

Or Setup with static class

```c#
CacheManager.Cache = new MyCacheImplementation();
CacheManager.CacheKeyFactory = new MyCacheKeyFactory();
```

## Usage

### Cache collection

```c#
var results = context.Blogs
    .Include(x => x.Posts)
    .ToCachedListAsync(cancellationToken);
```

### Cache item

```c#
var results = context.Blogs
    .Include(x => x.Posts)
    .Where(x => x.Id == request.Id)
    .CachedFirstOrDefaultAsync(cancellationToken);
```

### Invalidate cache

By default, all invalidation tags retrieved from Include and ThenInclude as navigation properties type names.
To invalidate cache just call this extension before context save changes.

```c#
await context.ChangeTracker.ExpireEntitiesCacheAsync();
```

### Implicit tags
Also it's possible to use explicit tags:
```c#
var results = context.Blogs
    .Include(x => x.Posts)
    .ToCachedListAsync(new List<string> { "all_blogs", "today" }, cancellationToken);

...
CacheManager.ExpireTagsAsync(new List<string> { "today" });
```
