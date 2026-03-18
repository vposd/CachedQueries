# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
- Circular reference handling (`ReferenceHandler.Preserve`) in Redis serialization
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
