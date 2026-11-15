using Contracts;
using StockService.Domain;
namespace StockService.Application;

public interface IStockService
{
    Task HandleAsync(OrderCreated evt, int partition, long offset, CancellationToken ct);
    Task<Stock?> GetAsync(string productId, CancellationToken ct);
    Task<Stock> SetAsync(string productId, int quantity, CancellationToken ct);
    Task<List<ProcessedEvent>> EventsAsync(CancellationToken ct);
}
