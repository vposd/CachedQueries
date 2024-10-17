using Microsoft.EntityFrameworkCore;
using Ordering.Application.Common.Behaviours;

namespace Ordering.Infrastructure;

public static class Extensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddDbContext<OrderingContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("DB"));
        });

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(Program));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });
    }

    public static void MigrateDatabase(this IServiceProvider provider)
    {
        using var serviceScope = provider.GetService<IServiceScopeFactory>()!.CreateScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<OrderingContext>();
        context.Database.Migrate();
    }
}
