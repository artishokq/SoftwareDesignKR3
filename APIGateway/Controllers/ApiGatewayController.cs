using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/v1")]
public class ApiGatewayController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiGatewayController> _logger;

    public ApiGatewayController(IHttpClientFactory httpClientFactory, ILogger<ApiGatewayController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }
    
    // Проксирует POST /api/v1/orders, OrderService POST /api/v1/orders
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            _logger.LogInformation("Creating order for user {UserId}, amount {Amount}", request.UserId, request.Amount);
            
            var client = _httpClientFactory.CreateClient("orders");
            var response = await client.PostAsJsonAsync("/api/v1/orders", request);
            
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("OrderService response: {StatusCode}, Content: {Content}", response.StatusCode, content);
            
            if (response.IsSuccessStatusCode)
            {
                return Ok(JsonSerializer.Deserialize<object>(content));
            }
            
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
    
    // Проксирует GET /api/v1/orders, OrderService GET /api/v1/orders
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders()
    {
        try
        {
            _logger.LogInformation("Getting all orders");
            
            var client = _httpClientFactory.CreateClient("orders");
            var response = await client.GetAsync("/api/v1/orders");
            
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("OrderService response: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                return Ok(JsonSerializer.Deserialize<object>(content));
            }
            
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
    
    // Проксирует GET /api/v1/orders/{orderId}, OrderService GET /api/v1/orders/{orderId}
    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetOrderById(Guid orderId)
    {
        try
        {
            _logger.LogInformation("Getting order {OrderId}", orderId);
            
            var client = _httpClientFactory.CreateClient("orders");
            var response = await client.GetAsync($"/api/v1/orders/{orderId}");
            
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("OrderService response: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                return Ok(JsonSerializer.Deserialize<object>(content));
            }
            
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order {OrderId}", orderId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }
    
    // Проксирует POST /api/v1/accounts/{userId}, PaymentService POST /api/v1/payments/accounts/{userId}
    [HttpPost("accounts/{userId:guid}")]
    public async Task<IActionResult> CreateAccount(Guid userId)
    {
        try
        {
            _logger.LogInformation("Creating account for user {UserId}", userId);
            
            var client = _httpClientFactory.CreateClient("payments");
            var response = await client.PostAsync($"/api/v1/payments/accounts/{userId}", null);
            
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PaymentsService response: {StatusCode}, Content: {Content}", response.StatusCode, content);
            
            if (response.IsSuccessStatusCode)
            {
                return Ok();
            }
            
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating account for user {UserId}", userId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }
    
    // Проксирует POST /api/v1/accounts/{userId}/topup, PaymentService POST /api/v1/payments/accounts/{userId}/topup
    [HttpPost("accounts/{userId:guid}/topup")]
    public async Task<IActionResult> TopUp(Guid userId, [FromBody] TopUpRequest request)
    {
        try
        {
            _logger.LogInformation("Topping up account for user {UserId}, amount {Amount}", userId, request.Amount);
            
            var client = _httpClientFactory.CreateClient("payments");
            var response = await client.PostAsJsonAsync($"/api/v1/payments/accounts/{userId}/topup", request);
            
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PaymentsService response: {StatusCode}, Content: {Content}", response.StatusCode, content);
            
            if (response.IsSuccessStatusCode)
            {
                return Ok();
            }
            
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error topping up account for user {UserId}", userId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }
    
    // Проксирует GET /api/v1/accounts/{userId}/balance, PaymentService GET /api/v1/payments/accounts/{userId}/balance
    [HttpGet("accounts/{userId:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid userId)
    {
        try
        {
            _logger.LogInformation("Getting balance for user {UserId}", userId);
            
            var client = _httpClientFactory.CreateClient("payments");
            var response = await client.GetAsync($"/api/v1/payments/accounts/{userId}/balance");
            
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PaymentsService response: {StatusCode}, Content: {Content}", response.StatusCode, content);
            
            if (response.IsSuccessStatusCode)
            {
                return Ok(JsonSerializer.Deserialize<object>(content));
            }
            
            return StatusCode((int)response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting balance for user {UserId}", userId);
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

// DTO
public record CreateOrderRequest(Guid UserId, decimal Amount, string Description);
public record TopUpRequest(decimal Amount);