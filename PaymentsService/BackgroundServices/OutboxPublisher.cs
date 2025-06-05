using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentsService.Data;
using PaymentsService.Models;

namespace PaymentsService.BackgroundServices;

/// <summary>
/// BackgroundService: читает записи из payment_outbox IsSent=false, отправляет их в Kafka topic
/// "payment-results" и помечает их IsSent=true
/// </summary>
public class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<OutboxPublisher> _logger;
    private readonly string _topic;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        IProducer<string, string> producer,
        IConfiguration configuration,
        ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _producer = producer;
        _logger = logger;
        _topic = configuration["KAFKA_PAYMENT_RESULTS_TOPIC"]
                 ?? throw new ArgumentNullException("KAFKA_PAYMENT_RESULTS_TOPIC");
        
        _logger.LogInformation("OutboxPublisher initialized with topic: {Topic}", _topic);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisher started");
        
        // Ждем немного, чтобы все сервисы успели запуститься
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

                var unsent = await context.Outbox
                    .Where(o => !o.IsSent)
                    .OrderBy(o => o.CreatedAt)
                    .Take(10)
                    .ToListAsync(stoppingToken);

                if (!unsent.Any())
                {
                    _logger.LogDebug("No unsent payment results found in outbox");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                _logger.LogInformation("Found {Count} unsent payment results in outbox", unsent.Count);

                foreach (var entry in unsent)
                {
                    try
                    {
                        _logger.LogInformation("Sending payment result for order {OrderId}, success: {IsSuccess}", 
                            entry.OrderId, entry.IsSuccess);
                        
                        var msg = new Message<string, string>
                        {
                            Key = entry.OrderId.ToString(),
                            Value = entry.Payload
                        };
                        
                        var result = await _producer.ProduceAsync(_topic, msg, stoppingToken);
                        
                        if (result.Status == PersistenceStatus.Persisted)
                        {
                            _logger.LogInformation("Payment result for order {OrderId} sent successfully to partition {Partition} at offset {Offset}", 
                                entry.OrderId, result.Partition.Value, result.Offset.Value);
                            entry.IsSent = true;
                        }
                        else
                        {
                            _logger.LogWarning("Failed to send payment result for order {OrderId}, status: {Status}", 
                                entry.OrderId, result.Status);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending payment result for order {OrderId}", entry.OrderId);
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Payment outbox entries updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OutboxPublisher");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        
        _logger.LogInformation("OutboxPublisher stopped");
    }

    public override void Dispose()
    {
        _producer?.Dispose();
        base.Dispose();
    }
}