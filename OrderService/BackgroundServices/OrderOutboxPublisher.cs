using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Data;
using OrderService.Models;

namespace OrderService.BackgroundServices;

/// <summary>
/// BackgroundService, который раз в N секунд читает таблицу Outbox,
/// берёт все записи IsSent = false, отправляет их в Kafka topic "order-payment-tasks", и помечает их IsSent = true
/// </summary>
public class OrderOutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<OrderOutboxPublisher> _logger;

    public OrderOutboxPublisher(
        IServiceScopeFactory scopeFactory,
        IProducer<string, string> producer,
        IConfiguration configuration,
        ILogger<OrderOutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _producer = producer;
        _logger = logger;
        _topic = configuration["KAFKA_ORDER_TASKS_TOPIC"] ?? throw new ArgumentNullException("KAFKA_ORDER_TASKS_TOPIC");
        
        _logger.LogInformation("OrderOutboxPublisher initialized with topic: {Topic}", _topic);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderOutboxPublisher started");
        
        // Ждем немного, чтобы все сервисы успели запуститься
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                
                var unsent = await context.Outbox
                    .Where(o => !o.IsSent)
                    .OrderBy(o => o.CreatedAt)
                    .Take(10)
                    .ToListAsync(stoppingToken);

                if (!unsent.Any())
                {
                    _logger.LogDebug("No unsent messages found in outbox");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                _logger.LogInformation("Found {Count} unsent messages in outbox", unsent.Count);

                foreach (var entry in unsent)
                {
                    try
                    {
                        _logger.LogInformation("Sending message for order {OrderId} to Kafka", entry.OrderId);
                        
                        var msg = new Message<string, string>
                        {
                            Key = entry.OrderId.ToString(),
                            Value = entry.Payload
                        };

                        var result = await _producer.ProduceAsync(_topic, msg, stoppingToken);
                        
                        if (result.Status == PersistenceStatus.Persisted)
                        {
                            _logger.LogInformation("Message for order {OrderId} sent successfully to partition {Partition} at offset {Offset}", 
                                entry.OrderId, result.Partition.Value, result.Offset.Value);
                            entry.IsSent = true;
                        }
                        else
                        {
                            _logger.LogWarning("Failed to send message for order {OrderId}, status: {Status}", 
                                entry.OrderId, result.Status);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending message for order {OrderId}", entry.OrderId);
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Outbox entries updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OrderOutboxPublisher");
            }

            // Ждём немного перед следующей попыткой
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        
        _logger.LogInformation("OrderOutboxPublisher stopped");
    }

    public override void Dispose()
    {
        _producer?.Dispose();
        base.Dispose();
    }
}