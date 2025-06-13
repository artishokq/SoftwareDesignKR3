using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Data;
using OrderService.Models;

namespace OrderService.BackgroundServices;

/// <summary>
/// BackgroundService, который слушает Kafka topic "payment-results"
/// На каждое сообщение десериализует PaymentResultMessage и обновляет Order.Status
/// </summary>
public class PaymentResultConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topic;
    private readonly ILogger<PaymentResultConsumer> _logger;

    public PaymentResultConsumer(
        IServiceScopeFactory scopeFactory,
        IConsumer<string, string> consumer,
        IConfiguration configuration,
        ILogger<PaymentResultConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _consumer = consumer;
        _logger = logger;
        _topic = configuration["KAFKA_PAYMENT_RESULTS_TOPIC"]
                 ?? throw new ArgumentNullException("KAFKA_PAYMENT_RESULTS_TOPIC");
        
        _logger.LogInformation("PaymentResultConsumer initialized with topic: {Topic}", _topic);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentResultConsumer starting, subscribing to topic: {Topic}", _topic);
        
        // Ждем немного, чтобы все сервисы успели запуститься
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        _consumer.Subscribe(_topic);
        _logger.LogInformation("Successfully subscribed to topic: {Topic}", _topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Polling for payment results...");
                var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));
                
                if (consumeResult == null) continue;

                _logger.LogInformation("Received payment result from partition {Partition} at offset {Offset}", 
                    consumeResult.Partition.Value, consumeResult.Offset.Value);

                var payload = consumeResult.Message.Value;
                _logger.LogDebug("Payment result payload: {Payload}", payload);
                
                var msg = JsonSerializer.Deserialize<PaymentResultMessage>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (msg != null)
                {
                    _logger.LogInformation("Processing payment result for order {OrderId}, success: {IsSuccess}", 
                        msg.OrderId, msg.IsSuccess);
                    
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

                    var order = await context.Orders.FindAsync(new object[] { msg.OrderId }, stoppingToken);
                    if (order != null)
                    {
                        var oldStatus = order.Status;
                        order.Status = msg.IsSuccess
                            ? OrderStatus.FINISHED
                            : OrderStatus.CANCELLED;
                        
                        await context.SaveChangesAsync(stoppingToken);
                        
                        _logger.LogInformation("Updated order {OrderId} status from {OldStatus} to {NewStatus}", 
                            msg.OrderId, oldStatus, order.Status);
                    }
                    else
                    {
                        _logger.LogWarning("Order {OrderId} not found", msg.OrderId);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize payment result: {Payload}", payload);
                }
                
                _consumer.Commit(consumeResult);
                _logger.LogDebug("Committed offset {Offset} for partition {Partition}", 
                    consumeResult.Offset.Value, consumeResult.Partition.Value);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PaymentResultConsumer");
            }
        }
        
        _logger.LogInformation("PaymentResultConsumer stopped");
    }

    public override void Dispose()
    {
        _consumer?.Close();
        _consumer?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Класс-модель, который десериализуем из Kafka сообщения "payment-results"
/// </summary>
public record PaymentResultMessage
{
    public Guid OrderId { get; set; }
    public bool IsSuccess { get; set; }
    public string? FailureReason { get; set; }
}