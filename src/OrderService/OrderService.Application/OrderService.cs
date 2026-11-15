using System.Text.Json;
using Contracts;
using Microsoft.EntityFrameworkCore;
using OrderService.Application;
using OrderService.Domain;
namespace OrderService.Application;

public sealed class OrderService(IOrderDbContext db) : IOrderService
{
    public async Task<Order> CreateAsync(string productId, int quantity, CancellationToken ct)
    {
        var order = new Order { Id = Guid.NewGuid(), ProductId = productId, Quantity = quantity, CreatedAt = DateTimeOffset.UtcNow };
        var evt = new OrderCreated(Guid.NewGuid(), order.Id, productId, quantity, order.CreatedAt);
        db.Orders.Add(order);
        db.Outbox.Add(new OutboxMessage { Id = evt.EventId, Key = productId, Payload = JsonSerializer.Serialize(evt) });
        await db.SaveChangesAsync(ct);
        return order;
    }
    public Task<Order?> GetAsync(Guid id, CancellationToken ct) => db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
}
