namespace PaymentsService.Services;

/// <summary>
/// 1) Создать новый счет для userId
/// 2) Пополнение счета
/// 3) Получение баланса
/// </summary>
public interface IAccountService
{
    Task CreateAccountAsync(Guid userId);
    Task TopUpAccountAsync(Guid userId, decimal amount);
    Task<decimal> GetBalanceAsync(Guid userId);
}