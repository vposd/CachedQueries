# CachedQueries

[![License](http://img.shields.io/:license-mit-blue.svg)](https://vposd.mit-license.org/)
[![NuGet version (CachedQueries)](https://img.shields.io/nuget/v/CachedQueries.svg)](https://www.nuget.org/packages/CachedQueries/)
[![NuGet downloads (CachedQueries)](https://img.shields.io/nuget/dt/CachedQueries.svg)](https://www.nuget.org/packages/CachedQueries/)
[![Build status](https://github.com/vposd/CachedQueries/actions/workflows/release.yml/badge.svg)]()
[![Coverage Status](https://coveralls.io/repos/github/vposd/CachedQueries/badge.svg?branch=master)](https://coveralls.io/github/vposd/CachedQueries?branch=master)
[![CodeFactor](https://www.codefactor.io/repository/github/vposd/cachedqueries/badge)](https://www.codefactor.io/repository/github/vposd/cachedqueries)

A library provides IQueryable results caching with smart invalidation.

The main goal is to bring caching to ef queries with flexible invalidation without any abstraction over DbSet.

For example:
```c#
    await context.Customers
        .Query(request)
        .ToCachedListAsync(cancellationToken);
        
    await context.Customers
        .FindById(id)
        .CachedFirstOrDefaultAsync(cancellationToken);
```
`Query` and `FindById` extensions could contain filters, include related entities, etc.
Using these queries in different places still returns cached results until the Customer entity will be modified.

## Setup

Setup with DI

```c#
// services is IServicesCollection
services.AddQueriesCaching(options =>
    options.UseEntityFramework());

...
// app is IApplicationBuilder
app.UseQueriesCaching();
```
Options also allows to define own implementations of cache key factory, lock manager, cache options, cache invalidator.

## Usage

### Cache collection

```c#
var results = context.Blogs
    .Include(x => x.Posts)
    .ToCachedListAsync(cancellationToken);

// with expiration
var results = context.Posts
    .ToCachedListAsync(Timespan.FromHours(8), cancellationToken);
    
// with custom tags
var results = context.Posts
    .ToCachedListAsync(Timespan.FromHours(8), new List<string> { "all" }, cancellationToken);
```

### Cache item

```c#
var result = context.Blogs
    .Include(x => x.Posts)
    .CachedFirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

// with expiration
var result = context.Posts
    .CachedFirstOrDefaultAsync(Timespan.FromHours(8), cancellationToken);
    
// with custom tags
var result = context.Posts
    .CachedFirstOrDefaultAsync(Timespan.FromHours(8), new List<string> { "all" }, cancellationToken);
```

### Invalidate cache

By default, all invalidation tags are retrieved from Include and ThenInclude as navigation properties type names.
To invalidate the cache just call this extension before context save changes.

```c#
await context.ChangeTracker.ExpireEntitiesCacheAsync();
```

For more details pls take a look on the tests =)
