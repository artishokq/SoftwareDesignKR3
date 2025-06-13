using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrderService.Controllers;
using OrderService.Models;
using OrderService.Services;
using Xunit;

namespace Tests.OrderServiceTests;

public class OrdersControllerTests
{
    private readonly Mock<IOrderService> _orderServiceMock;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        _orderServiceMock = new Mock<IOrderService>();
        _controller = new OrdersController(_orderServiceMock.Object);
    }

    #region CreateOrder

    [Fact(DisplayName = "CreateOrder: возвращает 200 OK с объектом Order")]
    public async Task CreateOrder_ReturnsOk_WithOrder()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var amount = 123.45m;
        var description = "Тестовый заказ";
        var request = new CreateOrderRequest(userId, amount, description);

        var expectedOrder = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Description = description,
            Status = OrderStatus.NEW
        };

        _orderServiceMock
            .Setup(s => s.CreateOrderAsync(userId, amount, description))
            .ReturnsAsync(expectedOrder);

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult.StatusCode.Should().Be(200);
        var returnedOrder = okResult.Value as Order;
        returnedOrder.Should().NotBeNull();
        returnedOrder.Should().BeEquivalentTo(expectedOrder);
    }

    [Fact(DisplayName = "CreateOrder: когда сервис кидает исключение, возвращает 500")]
    public async Task CreateOrder_WhenServiceThrows_PropagatesException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var amount = 10m;
        var description = "Bad";
        var request = new CreateOrderRequest(userId, amount, description);

        _orderServiceMock
            .Setup(s => s.CreateOrderAsync(userId, amount, description))
            .ThrowsAsync(new InvalidOperationException("Ошибка создания"));

        // Act
        Func<Task> act = async () => { await _controller.CreateOrder(request); };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Ошибка создания");
    }

    #endregion

    #region GetOrders

    [Fact(DisplayName = "GetOrders: возвращает 200 OK со списком заказов")]
    public async Task GetOrders_ReturnsOk_WithList()
    {
        // Arrange
        var sample = new List<Order>
        {
            new Order
            {
                Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 1m, Description = "A", Status = OrderStatus.NEW
            },
            new Order
            {
                Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 2m, Description = "B", Status = OrderStatus.NEW
            }
        };

        _orderServiceMock
            .Setup(s => s.GetOrdersAsync())
            .ReturnsAsync(sample);

        // Act
        var result = await _controller.GetOrders();

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult.StatusCode.Should().Be(200);
        var returnedList = okResult.Value as IEnumerable<Order>;
        returnedList.Should().NotBeNull();
        returnedList.Should().HaveCount(2).And.BeEquivalentTo(sample);
    }

    [Fact(DisplayName = "GetOrders: когда сервис кидает Exception, пробрасывается вверх")]
    public async Task GetOrders_WhenServiceThrows_PropagatesException()
    {
        // Arrange
        _orderServiceMock
            .Setup(s => s.GetOrdersAsync())
            .ThrowsAsync(new Exception("DB error"));

        // Act
        Func<Task> act = async () => { await _controller.GetOrders(); };

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("DB error");
    }

    #endregion

    #region GetOrderById

    [Fact(DisplayName = "GetOrderById: когда заказ найден, возвращает 200 и объект")]
    public async Task GetOrderById_WhenExists_ReturnsOk()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sample = new Order
        {
            Id = id,
            UserId = Guid.NewGuid(),
            Amount = 42m,
            Description = "XYZ",
            Status = OrderStatus.NEW
        };

        _orderServiceMock
            .Setup(s => s.GetOrderByIdAsync(id))
            .ReturnsAsync(sample);

        // Act
        var result = await _controller.GetOrderById(id);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult.StatusCode.Should().Be(200);
        var returnedOrder = okResult.Value as Order;
        returnedOrder.Should().BeEquivalentTo(sample);
    }

    [Fact(DisplayName = "GetOrderById: когда заказ не найден, возвращает 404")]
    public async Task GetOrderById_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _orderServiceMock
            .Setup(s => s.GetOrderByIdAsync(id))
            .ReturnsAsync((Order?)null);

        // Act
        var result = await _controller.GetOrderById(id);

        // Assert
        var notFound = result as NotFoundResult;
        notFound.Should().NotBeNull();
        notFound.StatusCode.Should().Be(404);
    }

    [Fact(DisplayName = "GetOrderById: когда сервис кидает Exception, пробрасывается вверх")]
    public async Task GetOrderById_WhenServiceThrows_PropagatesException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _orderServiceMock
            .Setup(s => s.GetOrderByIdAsync(id))
            .ThrowsAsync(new Exception("Some error"));

        // Act
        Func<Task> act = async () => { await _controller.GetOrderById(id); };

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Some error");
    }

    #endregion
}