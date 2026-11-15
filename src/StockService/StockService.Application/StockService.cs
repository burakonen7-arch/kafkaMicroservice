using Microsoft.EntityFrameworkCore;
using StockService.Application;
using StockService.Domain;
namespace StockService.Application;

public sealed class StockService(IStockDbContext db) : IStockService
{
    public async Task HandleAsync(Contracts.OrderCreated evt, int partition, long offset, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evt.ProductId) || evt.Quantity <= 0) throw new InvalidOperationException("Invalid order data");
        if (await db.ProcessedEvents.AnyAsync(x => x.Id == evt.EventId, ct)) return;
        var stock = await db.Stocks.FindAsync([evt.ProductId], ct);
        var accepted = stock is not null && stock.Quantity >= evt.Quantity;
        if (accepted) stock!.Quantity -= evt.Quantity;
        db.ProcessedEvents.Add(new ProcessedEvent
        {
            Id = evt.EventId,
            OrderId = evt.OrderId,
            ProductId = evt.ProductId,
            Outcome = accepted ? "StockDeducted" : "InsufficientStock",
            Partition = partition,
            Offset = offset
        });
        await db.SaveChangesAsync(ct);
    }
    public Task<Stock?> GetAsync(string productId, CancellationToken ct) => db.Stocks.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId, ct);
    public async Task<Stock> SetAsync(string productId, int quantity, CancellationToken ct)
    {
        var stock = await db.Stocks.FindAsync([productId], ct);
        if (stock is null) { stock = new Stock { ProductId = productId }; db.Stocks.Add(stock); }
        stock.Quantity = quantity; await db.SaveChangesAsync(ct); return stock;
    }
    public Task<List<ProcessedEvent>> EventsAsync(CancellationToken ct) => db.ProcessedEvents.AsNoTracking().ToListAsync(ct);
}
