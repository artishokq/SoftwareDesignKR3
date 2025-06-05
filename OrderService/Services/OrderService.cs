using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OrderService.Services;

/// <summary>
/// При создании заказа – в рамках одной транзакции INSERT в Orders и INSERT в Outbox
/// </summary>
public class OrderService : IOrderService
{
    private readonly OrderDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrderDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<Order> CreateOrderAsync(Guid userId, decimal amount, string description)
    {
        _logger.LogInformation("Creating order for user {UserId}, amount {Amount}", userId, amount);
        
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Description = description,
            Status = OrderStatus.NEW
        };
        
        var paymentTask = new
        {
            OrderId = order.Id,
            UserId = order.UserId,
            Amount = order.Amount
        };
        
        var outboxEntry = new OrderOutbox
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Payload = JsonSerializer.Serialize(paymentTask, new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                WriteIndented = false
            }),
            IsSent = false,
            CreatedAt = DateTime.UtcNow
        };
        
        _logger.LogDebug("Outbox payload: {Payload}", outboxEntry.Payload);
        
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.Orders.AddAsync(order);
            await _context.Outbox.AddAsync(outboxEntry);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            
            _logger.LogInformation("Order {OrderId} created successfully with outbox entry", order.Id);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Failed to create order for user {UserId}", userId);
            throw;
        }

        return order;
    }
    
    public async Task<IEnumerable<Order>> GetOrdersAsync()
    {
        _logger.LogDebug("Getting all orders");
        return await _context.Orders.AsNoTracking().ToListAsync();
    }
    
    public async Task<Order?> GetOrderByIdAsync(Guid orderId)
    {
        _logger.LogDebug("Getting order {OrderId}", orderId);
        return await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
    }
    
    public async Task UpdateOrderStatusAsync(Guid orderId, OrderStatus status)
    {
        _logger.LogInformation("Updating order {OrderId} status to {Status}", orderId, status);
        
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", orderId);
            return;
        }
        
        var oldStatus = order.Status;
        order.Status = status;
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Order {OrderId} status updated from {OldStatus} to {NewStatus}", 
            orderId, oldStatus, status);
    }
}