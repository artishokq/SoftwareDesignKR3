using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using OrderService.Data;
using OrderService.Models;
using OrderService.Services;
using Xunit;

namespace Tests.OrderServiceTests;

public class OrderServiceTests : IDisposable
{
    private readonly OrderDbContext _context;
    private readonly OrderService.Services.OrderService _service;

    public OrderServiceTests()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new OrderDbContext(options);
        _service = new OrderService.Services.OrderService(_context, new NullLogger<OrderService.Services.OrderService>());
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact(DisplayName = "CreateOrderAsync: добавляет запись в Orders и Outbox")]
    public async Task CreateOrderAsync_CreatesOrderAndOutbox()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var amount = 100m;
        var description = "Test";

        // Act
        var created = await _service.CreateOrderAsync(userId, amount, description);

        // Assert
        var orders = _context.Orders.ToList();
        orders.Should().HaveCount(1);
        orders.First().Id.Should().Be(created.Id);
        orders.First().UserId.Should().Be(userId);
        orders.First().Amount.Should().Be(amount);
        orders.First().Description.Should().Be(description);
        orders.First().Status.Should().Be(OrderStatus.NEW);
        var outbox = _context.Outbox.ToList();
        outbox.Should().HaveCount(1);
        outbox.First().OrderId.Should().Be(created.Id);
        outbox.First().IsSent.Should().BeFalse();
        outbox.First().Payload.Should().Contain(created.Id.ToString());
        outbox.First().CreatedAt.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact(DisplayName = "GetOrdersAsync: возвращает все заказы")]
    public async Task GetOrdersAsync_ReturnsAllOrders()
    {
        // Arrange
        _context.Orders.Add(new Order
            { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 1m, Description = "A", Status = OrderStatus.NEW });
        _context.Orders.Add(new Order
            { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 2m, Description = "B", Status = OrderStatus.NEW });
        await _context.SaveChangesAsync();

        // Act
        var all = await _service.GetOrdersAsync();

        // Assert
        all.Should().HaveCount(2);
    }

    [Fact(DisplayName = "GetOrderByIdAsync: возвращает заказ по его Id")]
    public async Task GetOrderByIdAsync_ReturnsCorrectOrder()
    {
        // Arrange
        var id = Guid.NewGuid();
        var order = new Order
            { Id = id, UserId = Guid.NewGuid(), Amount = 50m, Description = "X", Status = OrderStatus.NEW };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetOrderByIdAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
    }

    [Fact(DisplayName = "GetOrderByIdAsync: возвращает null, если заказа нет")]
    public async Task GetOrderByIdAsync_ReturnsNullIfNotExists()
    {
        // Act
        var result = await _service.GetOrderByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "UpdateOrderStatusAsync: изменяет статус заказа")]
    public async Task UpdateOrderStatusAsync_UpdatesStatus()
    {
        // Arrange
        var id = Guid.NewGuid();
        var order = new Order
            { Id = id, UserId = Guid.NewGuid(), Amount = 10m, Description = "Y", Status = OrderStatus.NEW };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateOrderStatusAsync(id, OrderStatus.FINISHED);

        // Assert
        var updated = await _context.Orders.FindAsync(id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(OrderStatus.FINISHED);
    }

    [Fact(DisplayName = "UpdateOrderStatusAsync: если заказа нет — не падает и не добавляет ничего")]
    public async Task UpdateOrderStatusAsync_NoSuchOrder_NoException()
    {
        // Act, Assert
        var nonexistentId = Guid.NewGuid();
        var beforeCount = _context.Orders.Count();
        await _service.UpdateOrderStatusAsync(nonexistentId, OrderStatus.CANCELLED);
        
        _context.Orders.Count().Should().Be(beforeCount);
    }
}