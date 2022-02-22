# CachedQueries

[![License](http://img.shields.io/:license-mit-blue.svg)](https://vposd.mit-license.org/)
[![NuGet version (CachedQueries)](https://img.shields.io/nuget/v/CachedQueries.svg?style=flat-square)](https://www.nuget.org/packages/CachedQueries/)
[![Build status](https://ci.appveyor.com/api/projects/status/bykgne88bjlkb5kb?svg=true)](https://ci.appveyor.com/project/vposd/cachedqueries)
[![Coverage Status](https://coveralls.io/repos/github/vposd/CachedQueries/badge.svg?branch=master)](https://coveralls.io/github/vposd/CachedQueries?branch=master)
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
    .CachedFirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
```

### Invalidate cache

By default, all invalidation tags retrieved from Include and ThenInclude as navigation properties type names.
To invalidate cache just call this extension before context save changes.

```c#
await context.ChangeTracker.ExpireEntitiesCacheAsync();
```
