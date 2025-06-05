using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PaymentsService.Data;
using PaymentsService.Models;
using PaymentsService.Services;
using Xunit;

namespace Tests.PaymentServiceTests;

public class AccountServiceTests : IDisposable
{
    private readonly PaymentDbContext _context;
    private readonly AccountService _service;

    public AccountServiceTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(b => b.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new PaymentDbContext(options);
        _service = new AccountService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact(DisplayName = "CreateAccountAsync: создаёт новый аккаунт")]
    public async Task CreateAccountAsync_CreatesNewAccount()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        await _service.CreateAccountAsync(userId);

        // Assert
        var saved = _context.Accounts.SingleOrDefault(a => a.UserId == userId);
        saved.Should().NotBeNull();
        saved!.Balance.Should().Be(0m);
    }

    [Fact(DisplayName = "CreateAccountAsync: если аккаунт уже есть, бросает InvalidOperationException")]
    public async Task CreateAccountAsync_WhenAccountExists_Throws()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _context.Accounts.Add(new Account { UserId = userId, Balance = 10m });
        await _context.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await _service.CreateAccountAsync(userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Аккаунт уже существует");
    }

    [Fact(DisplayName = "TopUpAccountAsync: увеличивает баланс")]
    public async Task TopUpAccountAsync_IncreasesBalance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _context.Accounts.Add(new Account { UserId = userId, Balance = 50m });
        await _context.SaveChangesAsync();

        // Act
        await _service.TopUpAccountAsync(userId, 25m);

        // Assert
        var acc = await _context.Accounts.FindAsync(userId);
        acc.Should().NotBeNull();
        acc!.Balance.Should().Be(75m);
    }

    [Fact(DisplayName = "TopUpAccountAsync: если аккаунта нет, бросает InvalidOperationException")]
    public async Task TopUpAccountAsync_WhenNoAccount_Throws()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _service.TopUpAccountAsync(userId, 10m);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Аккаунт не найден");
    }

    [Fact(DisplayName = "GetBalanceAsync: возвращает текущий баланс")]
    public async Task GetBalanceAsync_ReturnsCorrectBalance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _context.Accounts.Add(new Account { UserId = userId, Balance = 123.45m });
        await _context.SaveChangesAsync();

        // Act
        var balance = await _service.GetBalanceAsync(userId);

        // Assert
        balance.Should().Be(123.45m);
    }

    [Fact(DisplayName = "GetBalanceAsync: если аккаунта нет, бросает InvalidOperationException")]
    public async Task GetBalanceAsync_WhenNoAccount_Throws()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _service.GetBalanceAsync(userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Аккаунт не найден");
    }
}