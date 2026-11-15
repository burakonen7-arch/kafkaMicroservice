using System.Text.Json;
using Contracts;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockService.Domain;
namespace StockService.Infrastructure;

public sealed class OrderCreatedConsumer(IServiceScopeFactory scopes, IConfiguration config, ILogger<OrderCreatedConsumer> log) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken ct) => Task.Run(async () =>
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = config["Kafka:GroupId"] ?? "stock-service",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        }).SetPartitionsAssignedHandler((_, partitions) => log.LogInformation("Assigned partitions: {Partitions}", string.Join(",", partitions))).Build();
        consumer.Subscribe(config["Kafka:Topic"] ?? "orders.created.v1");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                ConsumeResult<string, string> result;
                try { result = consumer.Consume(ct); }
                catch (ConsumeException ex)
                {
                    log.LogError(ex, "Kafka consume failed; retrying");
                    await Task.Delay(1000, ct);
                    continue;
                }
                // Retry this record before reading another: failures must never be skipped by later commits.
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var evt = JsonSerializer.Deserialize<OrderCreated>(result.Message.Value) ?? throw new InvalidOperationException("Invalid event");
                        using var scope = scopes.CreateScope();
                        var service = scope.ServiceProvider.GetRequiredService<global::StockService.Application.IStockService>();
                        await service.HandleAsync(evt, result.Partition.Value, result.Offset.Value, ct);
                        consumer.Commit(result);
                        log.LogInformation("Consumed {EventId}: partition={Partition}, offset={Offset}", evt.EventId, result.Partition.Value, result.Offset.Value);
                        break;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex) { log.LogError(ex, "Record failed; retrying same offset"); await Task.Delay(1000, ct); }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { consumer.Close(); }
    }, ct);
}
