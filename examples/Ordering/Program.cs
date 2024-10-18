using CachedQueries.Core.Cache;
using CachedQueries.DependencyInjection;
using CachedQueries.EntityFramework.Extensions;
using Ordering.Api;
using Ordering.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddApplicationServices();

builder.Services.AddLogging(b => b.AddConsole());
builder.Services.AddMemoryCache();
builder.Services.AddCachedQueries(
    options => options
        .UseCacheStore<MemoryCache>()
        .UseEntityFramework());

var app = builder.Build();

app.Services.MigrateDatabase();
app.UseCachedQueries();

app.MapOrders();
app.MapCustomers();
app.MapProducts();

app.Run();
