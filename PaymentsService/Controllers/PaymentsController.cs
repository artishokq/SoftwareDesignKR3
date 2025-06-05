using Microsoft.AspNetCore.Mvc;
using PaymentsService.Services;

namespace PaymentsService.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public PaymentsController(IAccountService accountService)
    {
        _accountService = accountService;
    }
    
    // Создать счёт для userId
    // Запрос: POST /api/v1/payments/accounts/{userId}
    [HttpPost("accounts/{userId:guid}")]
    public async Task<IActionResult> CreateAccount(Guid userId)
    {
        try
        {
            await _accountService.CreateAccountAsync(userId);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
    
    // Пополнить счёт: POST /api/v1/payments/accounts/{userId}/topup
    // Body: { "amount": 100.5 }
    [HttpPost("accounts/{userId:guid}/topup")]
    public async Task<IActionResult> TopUp(Guid userId, [FromBody] TopUpRequest request)
    {
        try
        {
            await _accountService.TopUpAccountAsync(userId, request.Amount);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
    
    // Получить баланс: GET /api/v1/payments/accounts/{userId}/balance
    [HttpGet("accounts/{userId:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid userId)
    {
        try
        {
            var balance = await _accountService.GetBalanceAsync(userId);
            return Ok(new { Balance = balance });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}

public record TopUpRequest(decimal Amount);