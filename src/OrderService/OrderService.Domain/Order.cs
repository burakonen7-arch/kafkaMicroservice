namespace OrderService.Domain;

public sealed class Order
{
    public Guid Id { get; set; }
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public string Payload { get; set; } = "";
    public bool Published { get; set; }
}
