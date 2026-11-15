using StockService.Application;
using Microsoft.EntityFrameworkCore;
using StockService.Domain;
namespace StockService.Infrastructure;

public sealed class StockDbContext(DbContextOptions<StockDbContext> options) : DbContext(options), IStockDbContext
{
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    protected override void OnModelCreating(ModelBuilder model) { model.Entity<Stock>().HasKey(x => x.ProductId); }
}
