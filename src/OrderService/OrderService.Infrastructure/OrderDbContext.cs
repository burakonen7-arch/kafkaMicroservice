using OrderService.Application;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain;
namespace OrderService.Infrastructure;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options), IOrderDbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
}
