using Microsoft.EntityFrameworkCore;
using StockService.Domain;
namespace StockService.Application;

public interface IStockDbContext
{
    DbSet<Stock> Stocks { get; }
    DbSet<ProcessedEvent> ProcessedEvents { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
