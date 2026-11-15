using Microsoft.EntityFrameworkCore;
using OrderService.Domain;
namespace OrderService.Application;

public interface IOrderDbContext
{
    DbSet<Order> Orders { get; }
    DbSet<OutboxMessage> Outbox { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
