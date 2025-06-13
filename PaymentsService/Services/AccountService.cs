using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Models;

namespace PaymentsService.Services;

public class AccountService : IAccountService
{
    private readonly PaymentDbContext _context;

    public AccountService(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task CreateAccountAsync(Guid userId)
    {
        if (await _context.Accounts.AnyAsync(a => a.UserId == userId))
        {
            throw new InvalidOperationException("Аккаунт уже существует");
        }

        var account = new Account
        {
            UserId = userId,
            Balance = 0m
        };

        await _context.Accounts.AddAsync(account);
        await _context.SaveChangesAsync();
    }

    public async Task TopUpAccountAsync(Guid userId, decimal amount)
    {
        var account = await _context.Accounts.FindAsync(userId);
        if (account == null)
        {
            throw new InvalidOperationException("Аккаунт не найден");
        }

        account.Balance += amount;
        await _context.SaveChangesAsync();
    }

    public async Task<decimal> GetBalanceAsync(Guid userId)
    {
        var account = await _context.Accounts.FindAsync(userId);
        if (account == null) throw new InvalidOperationException("Аккаунт не найден");
        return account.Balance;
    }
}