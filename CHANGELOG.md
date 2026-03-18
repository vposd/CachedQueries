# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-01-15

### Added
- Initial release of CachedQueries library
- `ToListCachedAsync` - cache query results as a list
- `FirstOrDefaultCachedAsync` - cache first result or null
- `SingleOrDefaultCachedAsync` - cache single result or null
- `CountCachedAsync` - cache count results
- `AnyCachedAsync` - cache existence check results
- Automatic cache invalidation on `SaveChangesAsync`
- Transaction-aware invalidation (only invalidates on commit)
- Support for custom cache expiration (absolute and sliding)
- Tag-based cache invalidation
- In-memory cache provider (default)
- Redis cache provider (`CachedQueries.Redis`)
- Multi-provider support (different providers for single items, collections, and scalars)
- SourceLink support for debugging
- Multi-targeting: .NET 8.0 and .NET 9.0

### Technical Details
- Entity type extraction from LINQ expression trees
- Cache key generation from query expressions
- `ICacheProvider` abstraction for custom providers
- `ICacheProviderFactory` for multi-provider scenarios
- EF Core interceptors for automatic invalidation:
  - `CacheInvalidationInterceptor` (SaveChanges)
  - `TransactionCacheInvalidationInterceptor` (transactions)

[Unreleased]: https://github.com/valery-pozdnyakov/CachedQueries/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/valery-pozdnyakov/CachedQueries/releases/tag/v1.0.0
