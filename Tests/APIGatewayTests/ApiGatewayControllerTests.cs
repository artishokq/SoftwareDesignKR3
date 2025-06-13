using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ApiGateway.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Tests.APIGatewayTests;

public class ApiGatewayControllerTests
{
    /// <summary>
    /// Построить IHttpClientFactory, возвращающий HttpClient с FakeHttpMessageHandler
    /// </summary>
    private static IHttpClientFactory BuildFactory(HttpResponseMessage? fakeResponse = null,
        Exception? exception = null)
    {
        var handler = new FakeHttpMessageHandler(fakeResponse, exception);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(client);

        return mockFactory.Object;
    }

    #region CreateOrder Tests

    [Fact(DisplayName = "CreateOrder: успешный ответ 200 OK → возвращает Ok(десериализованный объект)")]
    public async Task CreateOrder_ReturnsOk_WhenDownstream200()
    {
        // Arrange
        var sampleOrder = new
            { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 10m, Description = "Test", Status = "NEW" };
        var json = JsonSerializer.Serialize(sampleOrder);
        var fakeResp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        var request = new CreateOrderRequest(sampleOrder.UserId, sampleOrder.Amount, sampleOrder.Description);

        // Act
        var result = await controller.CreateOrder(request);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var returned = okResult.Value!;
        var serializedReturned =
            JsonSerializer.Serialize(returned, new JsonSerializerOptions { WriteIndented = false });
        serializedReturned.Should().Be(json);
    }

    [Fact(DisplayName = "CreateOrder: downstream 201 Created → все равно возвращает 200 Ok")]
    public async Task CreateOrder_ReturnsOk_WhenDownstream201()
    {
        // Arrange
        var sampleOrder = new
            { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 20m, Description = "X", Status = "NEW" };
        var json = JsonSerializer.Serialize(sampleOrder);
        var fakeResp = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        var request = new CreateOrderRequest(sampleOrder.UserId, sampleOrder.Amount, sampleOrder.Description);

        // Act
        var result = await controller.CreateOrder(request);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var returned = okResult.Value!;
        var serializedReturned =
            JsonSerializer.Serialize(returned, new JsonSerializerOptions { WriteIndented = false });
        serializedReturned.Should().Be(json);
    }

    [Fact(DisplayName = "CreateOrder: downstream 400 BadRequest → возвращает StatusCode(400, content)")]
    public async Task CreateOrder_Propagates400_WhenDownstream400()
    {
        // Arrange
        var errorContent = JsonSerializer.Serialize(new { Error = "Invalid data" });
        var fakeResp = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(errorContent, Encoding.UTF8, "application/json")
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        var request = new CreateOrderRequest(Guid.NewGuid(), 5m, "Bad");

        // Act
        var result = await controller.CreateOrder(request);

        // Assert
        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(400);
        (objectResult.Value as string).Should().Be(errorContent);
    }

    [Fact(DisplayName = "CreateOrder: HttpClient.SendAsync бросает → возвращает 500 с { Error = ... }")]
    public async Task CreateOrder_Returns500_WhenHttpClientThrows()
    {
        // Arrange
        var factory = BuildFactory(fakeResponse: null, exception: new HttpRequestException("Network down"));
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        var request = new CreateOrderRequest(Guid.NewGuid(), 1m, "X");

        // Act
        var result = await controller.CreateOrder(request);

        // Assert
        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(500);
        var errorObj = objectResult.Value!;
        var propError = errorObj.GetType().GetProperty("Error");
        var msg = propError?.GetValue(errorObj) as string;
        msg.Should().Be("Network down");
    }
    
    #endregion

    #region GetOrders Tests

    [Fact(DisplayName = "GetOrders: downstream 200 OK с JSON-массивом → возвращает Ok(массив)")]
    public async Task GetOrders_ReturnsOk_WhenDownstream200()
    {
        // Arrange
        var arr = new[]
        {
            new { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 1m, Description = "A", Status = "NEW" },
            new { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 2m, Description = "B", Status = "NEW" }
        };
        var json = JsonSerializer.Serialize(arr);
        var fakeResp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.GetOrders();

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var returned = okResult.Value!;
        var serializedReturned =
            JsonSerializer.Serialize(returned, new JsonSerializerOptions { WriteIndented = false });
        serializedReturned.Should().Be(json);
    }

    [Fact(DisplayName = "GetOrders: downstream 404 NotFound → возвращает StatusCode(404, content)")]
    public async Task GetOrders_Propagates404_WhenDownstream404()
    {
        // Arrange
        var fakeResp = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("No orders here", Encoding.UTF8, "text/plain")
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.GetOrders();

        // Assert
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(404);
        (objResult.Value as string).Should().Be("No orders here");
    }

    [Fact(DisplayName = "GetOrders: HttpClient.SendAsync бросает → возвращает 500")]
    public async Task GetOrders_Returns500_WhenHttpClientThrows()
    {
        // Arrange
        var factory = BuildFactory(fakeResponse: null, exception: new Exception("down"));
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.GetOrders();

        // Assert
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(500);
        var errObj = objResult.Value!;
        var propError = errObj.GetType().GetProperty("Error");
        var msg = propError?.GetValue(errObj) as string;
        msg.Should().Be("down");
    }

    #endregion

    #region GetOrderById Tests

    [Fact(DisplayName = "GetOrderById: downstream 200 OK с объектом → возвращает Ok(объект)")]
    public async Task GetOrderById_ReturnsOk_WhenDownstream200()
    {
        // Arrange
        var id = Guid.NewGuid();
        var obj = new { Id = id, UserId = Guid.NewGuid(), Amount = 3m, Description = "X", Status = "NEW" };
        var json = JsonSerializer.Serialize(obj);
        var fakeResp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.GetOrderById(id);

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var returned = okResult.Value!;
        var serializedReturned =
            JsonSerializer.Serialize(returned, new JsonSerializerOptions { WriteIndented = false });
        serializedReturned.Should().Be(json);
    }

    [Fact(DisplayName = "GetOrderById: downstream 404 → возвращает StatusCode(404, content)")]
    public async Task GetOrderById_Propagates404_WhenDownstream404()
    {
        // Arrange
        var fakeResp = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Order not found", Encoding.UTF8, "text/plain")
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.GetOrderById(Guid.NewGuid());

        // Assert
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(404);
        (objResult.Value as string).Should().Be("Order not found");
    }

    [Fact(DisplayName = "GetOrderById: HttpClient.SendAsync бросает → возвращает 500")]
    public async Task GetOrderById_Returns500_WhenHttpClientThrows()
    {
        // Arrange
        var factory = BuildFactory(fakeResponse: null, exception: new Exception("fail"));
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.GetOrderById(Guid.NewGuid());

        // Assert
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(500);
        var errObj = objResult.Value!;
        var propError = errObj.GetType().GetProperty("Error");
        var msg = propError?.GetValue(errObj) as string;
        msg.Should().Be("fail");
    }

    #endregion

    #region CreateAccount Tests

    [Fact(DisplayName = "CreateAccount: downstream 200 OK → возвращает Ok()")]
    public async Task CreateAccount_ReturnsOk_WhenDownstream200()
    {
        // Arrange
        var fakeResp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.CreateAccount(Guid.NewGuid());

        // Assert
        var okResult = result as OkResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact(DisplayName = "CreateAccount: downstream 201 Created → возвращает Ok()")]
    public async Task CreateAccount_ReturnsOk_WhenDownstream201()
    {
        // Arrange
        var fakeResp = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(string.Empty)
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.CreateAccount(Guid.NewGuid());

        // Assert
        var okResult = result as OkResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact(DisplayName = "CreateAccount: downstream 400 BadRequest → возвращает StatusCode(400, content)")]
    public async Task CreateAccount_Propagates400_WhenDownstream400()
    {
        // Arrange
        var fakeResp = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Bad request", Encoding.UTF8, "text/plain")
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.CreateAccount(Guid.NewGuid());

        // Assert
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(400);
        (objResult.Value as string).Should().Be("Bad request");
    }

    [Fact(DisplayName = "CreateAccount: HttpClient.SendAsync бросает → возвращает 500")]
    public async Task CreateAccount_Returns500_WhenHttpClientThrows()
    {
        // Arrange
        var factory = BuildFactory(fakeResponse: null, exception: new Exception("net fail"));
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.CreateAccount(Guid.NewGuid());

        // Assert
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(500);
        var errObj = objResult.Value!;
        var propError = errObj.GetType().GetProperty("Error");
        var msg = propError?.GetValue(errObj) as string;
        msg.Should().Be("net fail");
    }

    #endregion

    #region TopUp Tests

    [Fact(DisplayName = "TopUp: downstream 200 OK → возвращает Ok()")]
    public async Task TopUp_ReturnsOk_WhenDownstream200()
    {
        // Arrange
        var fakeResp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.TopUp(Guid.NewGuid(), new TopUpRequest(10m));

        // Assert
        var okResult = result as OkResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact(DisplayName = "TopUp: downstream 201 Created → возвращает Ok()")]
    public async Task TopUp_ReturnsOk_WhenDownstream201()
    {
        // Arrange
        var fakeResp = new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent(string.Empty)
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.TopUp(Guid.NewGuid(), new TopUpRequest(10m));

        // Assert
        var okResult = result as OkResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
    }

    [Fact(DisplayName = "TopUp: downstream 400 BadRequest → возвращает StatusCode(400, content)")]
    public async Task TopUp_Propagates400_WhenDownstream400()
    {
        // Arrange
        var fakeResp = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Invalid amount", Encoding.UTF8, "text/plain")
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.TopUp(Guid.NewGuid(), new TopUpRequest(0m));

        // Assert
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(400);
        (objResult.Value as string).Should().Be("Invalid amount");
    }

    [Fact(DisplayName = "TopUp: HttpClient.SendAsync бросает → возвращает 500")]
    public async Task TopUp_Returns500_WhenHttpClientThrows()
    {
        // Arrange
        var factory = BuildFactory(fakeResponse: null, exception: new Exception("oops"));
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.TopUp(Guid.NewGuid(), new TopUpRequest(10m));

        // Assert
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(500);

        var errObj = objResult.Value!;
        var propError = errObj.GetType().GetProperty("Error");
        var msg = propError?.GetValue(errObj) as string;
        msg.Should().Be("oops");
    }

    #endregion

    #region GetBalance Tests

    [Fact(DisplayName = "GetBalance: downstream 200 OK с JSON → возвращает Ok(десериализованный объект)")]
    public async Task GetBalance_ReturnsOk_WhenDownstream200()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new { Balance = 250m });
        var fakeResp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.GetBalance(Guid.NewGuid());

        // Assert
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(200);
        var returned = okResult.Value!;
        var serializedReturned =
            JsonSerializer.Serialize(returned, new JsonSerializerOptions { WriteIndented = false });
        serializedReturned.Should().Be(payload);
    }

    [Fact(DisplayName = "GetBalance: downstream 404 NotFound → возвращает StatusCode(404, content)")]
    public async Task GetBalance_Propagates404_WhenDownstream404()
    {
        // Arrange
        var fakeResp = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("No account", Encoding.UTF8, "text/plain")
        };
        var factory = BuildFactory(fakeResp);
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.GetBalance(Guid.NewGuid());

        // Assert
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(404);
        (objResult.Value as string).Should().Be("No account");
    }

    [Fact(DisplayName = "GetBalance: HttpClient.SendAsync бросает → возвращает 500")]
    public async Task GetBalance_Returns500_WhenHttpClientThrows()
    {
        // Arrange
        var factory = BuildFactory(fakeResponse: null, exception: new Exception("network fail"));
        var controller = new ApiGatewayController(factory, new NullLogger<ApiGatewayController>());

        // Act
        var result = await controller.GetBalance(Guid.NewGuid());

        // Assert
        var objResult = result as ObjectResult;
        objResult.Should().NotBeNull();
        objResult!.StatusCode.Should().Be(500);
        var errObj = objResult.Value!;
        var propError = errObj.GetType().GetProperty("Error");
        var msg = propError?.GetValue(errObj) as string;
        msg.Should().Be("network fail");
    }
    #endregion
}