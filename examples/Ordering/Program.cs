using Ordering.Api;
using Ordering.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddApplicationServices();

var app = builder.Build();

app.Services.MigrateDatabase();

app.MapOrders();
app.MapCustomers();
app.MapProducts();

app.Run();
