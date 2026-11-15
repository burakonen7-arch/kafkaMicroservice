using Microsoft.EntityFrameworkCore;
using StockService.Application;
using StockService.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
Directory.CreateDirectory("data");
builder.Services.AddDbContext<StockDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Database") ?? "Data Source=data/stock.db"));
builder.Services.AddScoped<IStockDbContext>(sp => sp.GetRequiredService<StockDbContext>());
builder.Services.AddScoped<IStockService, StockService.Application.StockService>();
builder.Services.AddHostedService<OrderCreatedConsumer>();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}
app.MapControllers();
app.Run();
