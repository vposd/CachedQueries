using CachedQueries.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CachedQueries.Extensions;

/// <summary>
/// Extension methods for configuring EF Core DbContext with CachedQueries.
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Adds cache invalidation interceptors to the DbContext.
    /// This includes SaveChanges and Transaction interceptors.
    /// </summary>
    public static DbContextOptionsBuilder AddCacheInvalidation(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
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


