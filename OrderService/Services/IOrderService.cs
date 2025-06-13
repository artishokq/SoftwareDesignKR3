using OrderService.Models;

namespace OrderService.Services;

/// <summary>
/// Сервис для работы с заказами: Create, List, GetById, UpdateStatus
/// При создании заказа автоматически пишем запись в Outbox для Kafka
/// </summary>
public interface IOrderService
{
    Task<Order> CreateOrderAsync(Guid userId, decimal amount, string description);
    Task<IEnumerable<Order>> GetOrdersAsync();
    Task<Order?> GetOrderByIdAsync(Guid orderId);
    Task UpdateOrderStatusAsync(Guid orderId, OrderStatus status);
}