using CachedQueries.Abstractions;

namespace CachedQueries.Internal;

/// <summary>
///     Default cache provider factory that routes cache operations to appropriate providers.
/// </summary>
internal sealed class CacheProviderFactory : ICacheProviderFactory
{
    private readonly ICacheProvider? _collectionProvider;
    private readonly ICacheProvider _defaultProvider;
    private readonly ICacheProvider? _scalarProvider;
    private readonly ICacheProvider? _singleProvider;

    public CacheProviderFactory(
        ICacheProvider defaultProvider,
        ICacheProvider? singleProvider = null,
        ICacheProvider? collectionProvider = null,
        ICacheProvider? scalarProvider = null)
    {
        _defaultProvider = defaultProvider;
        _singleProvider = singleProvider;
        _collectionProvider = collectionProvider;
        _scalarProvider = scalarProvider;
    }

    public ICacheProvider GetProvider(CacheTarget target)
    {
        return target switch
        {
            CacheTarget.Single => _singleProvider ?? _defaultProvider,
            CacheTarget.Collection => _collectionProvider ?? _defaultProvider,
            CacheTarget.Scalar => _scalarProvider ?? _defaultProvider,
            _ => _defaultProvider
        };
    }

    public IEnumerable<ICacheProvider> GetAllProviders()
    {
        var providers = new HashSet<ICacheProvider> { _defaultProvider };

        if (_singleProvider is not null)
        {
            providers.Add(_singleProvider);
        }

        if (_collectionProvider is not null)
        {
            providers.Add(_collectionProvider);
        }

        if (_scalarProvider is not null)
        {
            providers.Add(_scalarProvider);
        }

        return providers;
    }
}
