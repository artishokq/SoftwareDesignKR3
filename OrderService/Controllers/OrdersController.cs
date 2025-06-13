using Microsoft.AspNetCore.Mvc;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }
    
    // Создать новый заказ
    // В запросе надо передать userId, amount, description
    // В ответе – весь объект Order с полем Status=NEW
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var order = await _orderService.CreateOrderAsync(request.UserId, request.Amount, request.Description);
        return Ok(order);
    }
    
    // Вернуть список заказов
    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
        var orders = await _orderService.GetOrdersAsync();
        return Ok(orders);
    }
    
    // Вернуть конкретный заказ по orderId
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetOrderById(Guid orderId)
    {
        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order == null) return NotFound();
        return Ok(order);
    }
}

/// <summary>
/// DTO для создания заказа
/// </summary>
public record CreateOrderRequest(Guid UserId, decimal Amount, string Description);