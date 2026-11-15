using OrderService.Domain;
namespace OrderService.Application;

public interface IOrderService
{
    Task<Order> CreateAsync(string productId, int quantity, CancellationToken ct);
    Task<Order?> GetAsync(Guid id, CancellationToken ct);
}
