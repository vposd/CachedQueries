# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- Cache keys on non-relational providers (InMemory, Cosmos): every query produced the same key because
  `ToQueryString()` returns a constant message there. Keys now also include a rendering of the expression tree with
  captured variable values. The fallback renderer also no longer ignores captured values, method call targets
  (`a.Name.ToUpper()` vs `a.Email.ToUpper()`) or generic arguments (`OfType<A>()` vs `OfType<B>()`).
- `MemoryCacheProvider` could lose tag tracking when an entry was overwritten (the replaced entry's eviction callback
  removed the new entry's tags), leaving entries that `SaveChanges` could not invalidate. Tags are now cancellation
  tokens attached to entries.
- Query dependencies missed `DbSet`s used in joins and subqueries (EF Core query roots were never visited).
- `SaveChanges` invalidation used the runtime CLR type: lazy-loading proxies never matched, changing a derived entity
  did not invalidate queries on its base type, owned entities did not invalidate their owner, and many-to-many link
  changes invalidated nothing. Types now come from EF metadata.
- Stampede protection removed a key's semaphore while callers were still waiting on it, allowing concurrent loads;
  `FirstOrDefaultAsync`/`SingleOrDefaultAsync` had no stampede protection at all.
- A failed `SaveChanges` inside a transaction discarded pending invalidations of earlier successful `SaveChanges`
  calls in the same transaction. A failed commit/savepoint no longer discards them either.
- Pending invalidations leaked when a transaction was disposed without commit or rollback.
- `TransactionScope`: invalidation ran before the scope committed (and also on rollback). It now runs on commit.
- Redis: tag invalidation failed silently for tags with more than ~8000 entries (Lua `unpack` limit).
- Redis: a short-lived entry shortened the TTL of a shared tag set, so longer-lived entries could no longer be
  invalidated once the set expired. Tag set TTLs are now only extended.
- Redis: sliding expiration was ignored in multiplexer mode; cache hits now refresh the entry and its tag sets.
- Redis: `ClearAsync` also scanned replicas.
- `CachedQueriesConfiguration.DefaultOptions` and `AutoInvalidation` had no effect.
- Library code now uses `ConfigureAwait(false)`, avoiding deadlocks of synchronous `SaveChanges`/`Commit` under a
  `SynchronizationContext`.

### Added
- `CacheTags.ForEntity<T>()` — the tag under which queries depending on an entity type are tracked. Use it with
  `WithTags(...)` to keep automatic invalidation, or with `Cache.InvalidateByTagAsync(...)`.

### Changed
- Results served through the cache are loaded with `AsNoTracking()` (unless the query uses
  `AsNoTrackingWithIdentityResolution()`): cached instances are shared between callers and must not be tracked by,
  or modified through, one request's `DbContext`.
- `AddCacheInvalidation<TContext>()` throws when called before the DbContext is registered (it used to silently do
  nothing, leaving automatic invalidation off).
- `AddCachedQueriesWithRedis(connectionString)` shares one `IConnectionMultiplexer` with `IDistributedCache`.
- XML documentation is now complete for all public APIs, including the provider contract and known limitations.

### Deprecated
- `CachedQueriesConfiguration.EnableLogging` (never had an effect; configure log levels instead).
- `ICacheInvalidator.RegisterCacheEntry` overloads (no-ops since tracking moved to provider tags).

## [3.0.0] - 2026-03-18

### Added
- New fluent `.Cacheable()` API replacing `ToListCachedAsync`/`FirstOrDefaultCachedAsync` etc.
- `CacheableQuery<T>` with `ToListAsync`, `FirstOrDefaultAsync`, `SingleOrDefaultAsync`, `CountAsync`, `AnyAsync`
- Fluent options builder: `.Expire()`, `.SlidingExpiration()`, `.WithKey()`, `.WithTags()`, `.SkipIf()`, `.UseTarget()`, `.IgnoreContext()`
- Multi-tenant cache isolation via `ICacheContextProvider`
- `IgnoreContext()` option for global (tenant-independent) cache entries
- Context-aware invalidation: only invalidates current tenant + global entries
- `ClearContextAsync()` to clear cache for current tenant only
- `ClearAllAsync()` to clear all cache entries across all providers
- Manual invalidation: `Cache.InvalidateAsync<T>()`, `Cache.InvalidateByTagAsync()`, `Cache.InvalidateByTagsAsync()`
- Redis provider with atomic tag operations via `StackExchange.Redis`
- Circular reference handling (`ReferenceHandler.IgnoreCycles`) in Redis serialization
- Multi-provider support (different providers for single items, collections, scalars)
- `ICacheProviderFactory` for multi-provider scenarios
- Demo project with Docker Compose, PostgreSQL, and integration tests

### Changed
- Consolidated from 4 packages (`CachedQueries.Core`, `.Linq`, `.EntityFramework`, `.DependencyInjection`) into 2 (`CachedQueries`, `CachedQueries.Redis`)
- Modernized to .NET 8.0 / 9.0 with multi-targeting
- Replaced `Package.nuspec` with SDK-style `.csproj` packaging
- SourceLink, deterministic builds, symbol packages (.snupkg)
- CI: GitHub Actions with Coveralls coverage reporting

### Removed
- Legacy 4-package architecture
- `Package.nuspec` (replaced by `.csproj` metadata)

[Unreleased]: https://github.com/vposd/CachedQueries/compare/v3.0.0...HEAD
[3.0.0]: https://github.com/vposd/CachedQueries/releases/tag/v3.0.0
