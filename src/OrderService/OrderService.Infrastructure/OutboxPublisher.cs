using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace OrderService.Infrastructure;

public sealed class OutboxPublisher(IServiceScopeFactory scopes, IConfiguration config, ILogger<OutboxPublisher> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092",
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageTimeoutMs = 10000
        }).Build();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                // SQLite rowid preserves insertion order, including when timestamps are equal.
                var messages = await db.Outbox.FromSqlRaw("SELECT * FROM Outbox WHERE Published = 0 ORDER BY rowid LIMIT 100").ToListAsync(ct);
                foreach (var message in messages)
                {
                    var result = await producer.ProduceAsync(config["Kafka:Topic"] ?? "orders.created.v1", new Message<string, string> { Key = message.Key, Value = message.Payload }, ct);
                    message.Published = true;
                    await db.SaveChangesAsync(ct);
                    log.LogInformation("Published {EventId}: partition={Partition}, offset={Offset}", message.Id, result.Partition.Value, result.Offset.Value);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogError(ex, "Publish failed; pending outbox messages will be retried"); }
            await Task.Delay(500, ct);
        }
    }
}
