using CachedQueries.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CachedQueries.Extensions;

/// <summary>
///     Extension methods for configuring EF Core DbContext with CachedQueries.
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    ///     Adds cache invalidation interceptors to the DbContext.
    ///     This includes SaveChanges and Transaction interceptors.
    ///     Does nothing when <see cref="CachedQueriesConfiguration.AutoInvalidation" /> is false
    ///     or when CachedQueries services are not registered.
    /// </summary>
    /// <example>
    ///     services.AddDbContext&lt;AppDbContext&gt;((sp, options) => options
    ///     .UseNpgsql(connectionString)
    ///     .AddCacheInvalidation(sp));
    /// </example>
    public static DbContextOptionsBuilder AddCacheInvalidation(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService<CachedQueriesConfiguration>() is { AutoInvalidation: false })
        {
            return optionsBuilder;
        }

        var saveChangesInterceptor = serviceProvider.GetService<CacheInvalidationInterceptor>();
        var transactionInterceptor = serviceProvider.GetService<TransactionCacheInvalidationInterceptor>();

        if (saveChangesInterceptor is not null)
        {
            optionsBuilder.AddInterceptors(saveChangesInterceptor);
        }

        if (transactionInterceptor is not null)
        {
            optionsBuilder.AddInterceptors(transactionInterceptor);
        }

        return optionsBuilder;
    }
}
