namespace StockService.Domain;

public sealed class Stock { public string ProductId { get; set; } = ""; public int Quantity { get; set; } }
public sealed class ProcessedEvent
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string ProductId { get; set; } = "";
    public string Outcome { get; set; } = "";
    public int Partition { get; set; }
    public long Offset { get; set; }
}
