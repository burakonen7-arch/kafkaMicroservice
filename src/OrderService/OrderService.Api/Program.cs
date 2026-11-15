using Microsoft.EntityFrameworkCore;
using OrderService.Application;
using OrderService.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
Directory.CreateDirectory("data");
builder.Services.AddDbContext<OrderDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Database") ?? "Data Source=data/orders.db"));
builder.Services.AddScoped<IOrderDbContext>(sp => sp.GetRequiredService<OrderDbContext>());
builder.Services.AddScoped<IOrderService, OrderService.Application.OrderService>();
builder.Services.AddHostedService<OutboxPublisher>();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}
app.MapControllers();
app.Run();
