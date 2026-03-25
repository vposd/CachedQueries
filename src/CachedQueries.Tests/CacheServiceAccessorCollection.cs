using Xunit;

namespace CachedQueries.Tests;

/// <summary>
///     Collection definition for tests that use CacheServiceAccessor.
///     These tests must run sequentially to avoid race conditions.
/// </summary>
[CollectionDefinition("CacheServiceAccessor", DisableParallelization = true)]
public class CacheServiceAccessorCollection : ICollectionFixture<CacheServiceAccessorFixture>
{
}

public class CacheServiceAccessorFixture
{
}

/// <summary>
///     Collection definition for tests that use static PendingInvalidations.
///     These tests must run sequentially to avoid race conditions.
/// </summary>
[CollectionDefinition("Interceptors", DisableParallelization = true)]
public class InterceptorCollection : ICollectionFixture<InterceptorFixture>
{
}

public class InterceptorFixture
{
}
