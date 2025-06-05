using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PaymentsService.Controllers;
using PaymentsService.Services;
using Xunit;

namespace Tests.PaymentServiceTests;

public class PaymentsControllerTests
{
    private readonly Mock<IAccountService> _accountServiceMock;
    private readonly PaymentsController _controller;

    public PaymentsControllerTests()
    {
        _accountServiceMock = new Mock<IAccountService>();
        _controller = new PaymentsController(_accountServiceMock.Object);
    }

    #region CreateAccount

    [Fact(DisplayName = "CreateAccount: возвращает 200 OK при успешном создании")]
    public async Task CreateAccount_ReturnsOk_WhenServiceSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _accountServiceMock
            .Setup(s => s.CreateAccountAsync(userId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.CreateAccount(userId);

        // Assert
        var okResult = result as OkResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact(DisplayName = "CreateAccount: возвращает 400 BadRequest с { Error = ... } при исключении сервиса")]
    public async Task CreateAccount_ReturnsBadRequest_WhenServiceThrows()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _accountServiceMock
            .Setup(s => s.CreateAccountAsync(userId))
            .ThrowsAsync(new Exception("Аккаунт уже существует"));

        // Act
        var result = await _controller.CreateAccount(userId);

        // Assert
        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
        var errorObj = badRequest.Value!;
        var errorProp = errorObj.GetType().GetProperty("Error", BindingFlags.Public | BindingFlags.Instance);
        var errorValue = errorProp?.GetValue(errorObj) as string;
        errorValue.Should().Be("Аккаунт уже существует");
    }

    #endregion

    #region TopUp

    [Fact(DisplayName = "TopUp: возвращает 200 OK при успешном пополнении")]
    public async Task TopUp_ReturnsOk_WhenServiceSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var amount = 150m;
        var request = new TopUpRequest(amount);

        _accountServiceMock
            .Setup(s => s.TopUpAccountAsync(userId, amount))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.TopUp(userId, request);

        // Assert
        var okResult = result as OkResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact(DisplayName = "TopUp: возвращает 400 BadRequest с { Error = ... } при ошибке сервиса")]
    public async Task TopUp_ReturnsBadRequest_WhenServiceThrows()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var amount = 0m;
        var request = new TopUpRequest(amount);

        _accountServiceMock
            .Setup(s => s.TopUpAccountAsync(userId, amount))
            .ThrowsAsync(new Exception("Счёт не найден"));

        // Act
        var result = await _controller.TopUp(userId, request);

        // Assert
        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
        var errorObj = badRequest.Value!;
        var errorProp = errorObj.GetType().GetProperty("Error", BindingFlags.Public | BindingFlags.Instance);
        var errorValue = errorProp?.GetValue(errorObj) as string;
        errorValue.Should().Be("Счёт не найден");
    }

    #endregion

    #region GetBalance

    [Fact(DisplayName = "GetBalance: возвращает 200 OK с { Balance = ... } при существующем аккаунте")]
    public async Task GetBalance_ReturnsOk_WithBalance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var balance = 500m;
        _accountServiceMock
            .Setup(s => s.GetBalanceAsync(userId))
            .ReturnsAsync(balance);

        // Act
        var result = await _controller.GetBalance(userId);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var valueObj = okResult.Value!;
        var balanceProp = valueObj.GetType().GetProperty("Balance", BindingFlags.Public | BindingFlags.Instance);
        var actualBalance = balanceProp?.GetValue(valueObj);
        actualBalance.Should().BeOfType<decimal>();
        ((decimal)actualBalance!).Should().Be(500m);
    }

    [Fact(DisplayName = "GetBalance: возвращает 400 BadRequest с { Error = ... } при ошибке сервиса")]
    public async Task GetBalance_ReturnsBadRequest_WhenServiceThrows()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _accountServiceMock
            .Setup(s => s.GetBalanceAsync(userId))
            .ThrowsAsync(new Exception("Счёт не найден"));

        // Act
        var result = await _controller.GetBalance(userId);

        // Assert
        var badRequest = result as BadRequestObjectResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(400);
        var errorObj = badRequest.Value!;
        var errorProp = errorObj.GetType().GetProperty("Error", BindingFlags.Public | BindingFlags.Instance);
        var errorValue = errorProp?.GetValue(errorObj) as string;
        errorValue.Should().Be("Счёт не найден");
    }

    #endregion
}