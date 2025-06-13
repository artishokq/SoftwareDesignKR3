using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Models;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PaymentsService.BackgroundServices;

/// <summary>
/// BackgroundService: слушает Kafka topic "order-payment-tasks", сохраняет задачу в таблицу Inbox, обрабатывает
/// списание денег, складывает событие исхода в Outbox, и коммитит Kafka-offset
/// </summary>
public class PaymentProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<PaymentProcessor> _logger;
    private readonly string _topic;

    public PaymentProcessor(
        IServiceScopeFactory scopeFactory,
        IConsumer<string, string> consumer,
        IConfiguration configuration,
        ILogger<PaymentProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _consumer = consumer;
        _logger = logger;
        _topic = configuration["KAFKA_ORDER_TASKS_TOPIC"]
                 ?? throw new ArgumentNullException("KAFKA_ORDER_TASKS_TOPIC");
        
        _logger.LogInformation("PaymentProcessor initialized with topic: {Topic}", _topic);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PaymentProcessor starting, subscribing to topic: {Topic}", _topic);
        
        // Ждем немного, чтобы все сервисы успели запуститься
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        _consumer.Subscribe(_topic);
        _logger.LogInformation("Successfully subscribed to topic: {Topic}", _topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Polling for messages...");
                var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));
                
                if (consumeResult == null)
                {
                    continue;
                }

                _logger.LogInformation("Received message with key: {Key} from partition {Partition} at offset {Offset}", 
                    consumeResult.Message.Key, consumeResult.Partition.Value, consumeResult.Offset.Value);

                var messageKey = consumeResult.Message.Key!;
                var payload = consumeResult.Message.Value!;
                
                _logger.LogDebug("Message payload: {Payload}", payload);
                
                var task = JsonSerializer.Deserialize<OrderPaymentTask>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (task == null)
                {
                    _logger.LogWarning("Invalid payment task: {Payload}", payload);
                    _consumer.Commit(consumeResult);
                    continue;
                }

                _logger.LogInformation("Processing payment for order {OrderId}, user {UserId}, amount {Amount}", 
                    task.OrderId, task.UserId, task.Amount);

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

                    // Проверяем на дубликат
                    bool already = await context.Inbox.AnyAsync(i => i.MessageKey == messageKey, stoppingToken);
                    if (already)
                    {
                        _logger.LogWarning("Message with key {MessageKey} already processed, skipping", messageKey);
                        _consumer.Commit(consumeResult);
                        continue;
                    }
                    
                    await using var tx = await context.Database.BeginTransactionAsync(stoppingToken);
                    
                    try
                    {
                        var inbox = new PaymentInbox
                        {
                            MessageKey = messageKey,
                            OrderId = task.OrderId,
                            UserId = task.UserId,
                            Amount = task.Amount,
                            Processed = false,
                            CreatedAt = DateTime.UtcNow
                        };
                        await context.Inbox.AddAsync(inbox, stoppingToken);
                        _logger.LogDebug("Added message to inbox");
                        
                        var account = await context.Accounts.FindAsync(new object[] { task.UserId }, stoppingToken);
                        bool success = false;
                        string failureReason = "";
                        
                        if (account == null)
                        {
                            failureReason = "Account not found";
                            _logger.LogWarning("Account not found for user {UserId}", task.UserId);
                        }
                        else if (account.Balance < task.Amount)
                        {
                            failureReason = $"Insufficient balance. Required: {task.Amount}, Available: {account.Balance}";
                            _logger.LogWarning("Insufficient balance for user {UserId}. Required: {Amount}, Available: {Balance}", 
                                task.UserId, task.Amount, account.Balance);
                        }
                        else
                        {
                            account.Balance -= task.Amount;
                            success = true;
                            _logger.LogInformation("Successfully deducted {Amount} from user {UserId}. New balance: {Balance}", 
                                task.Amount, task.UserId, account.Balance);
                        }

                        // Записываем в Outbox сообщение с исходом оплаты
                        var paymentResult = new
                        {
                            OrderId = task.OrderId,
                            IsSuccess = success,
                            FailureReason = failureReason
                        };
                        
                        var outbox = new PaymentOutbox
                        {
                            Id = Guid.NewGuid(),
                            OrderId = task.OrderId,
                            IsSuccess = success,
                            Payload = JsonSerializer.Serialize(paymentResult, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = null,
                                WriteIndented = false
                            }),
                            IsSent = false,
                            CreatedAt = DateTime.UtcNow
                        };
                        await context.Outbox.AddAsync(outbox, stoppingToken);
                        _logger.LogDebug("Added payment result to outbox");
                        
                        inbox.Processed = true;
                        
                        await context.SaveChangesAsync(stoppingToken);
                        await tx.CommitAsync(stoppingToken);
                        
                        _logger.LogInformation("Payment processing completed for order {OrderId}. Success: {Success}", 
                            task.OrderId, success);
                    }
                    catch (Exception ex)
                    {
                        await tx.RollbackAsync(stoppingToken);
                        _logger.LogError(ex, "Error processing payment for order {OrderId}", task.OrderId);
                        throw;
                    }
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
                _logger.LogError(ex, "Error in PaymentProcessor");
            }
        }
        
        _logger.LogInformation("PaymentProcessor stopped");
    }

    public override void Dispose()
    {
        _consumer?.Close();
        _consumer?.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Модель, десериализуемая из Kafka topic "order-payment-tasks"
    /// </summary>
    public record OrderPaymentTask(Guid OrderId, Guid UserId, decimal Amount)
    {
        // Дополнительный конструктор для десериализации
        public OrderPaymentTask() : this(Guid.Empty, Guid.Empty, 0m) { }
        
        // Свойства для десериализации
        public Guid OrderId { get; set; } = OrderId;
        public Guid UserId { get; set; } = UserId;
        public decimal Amount { get; set; } = Amount;
    }
}