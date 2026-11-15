namespace Contracts;

public sealed record OrderCreated(Guid EventId, Guid OrderId, string ProductId, int Quantity, DateTimeOffset CreatedAt);
