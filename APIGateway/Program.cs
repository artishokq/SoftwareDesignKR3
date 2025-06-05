using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API Gateway", Version = "v1" });
});

builder.Services.AddHttpClient("orders", client =>
{
    var url = builder.Configuration["Services:OrderService"] 
              ?? builder.Configuration["ORDER_SERVICE_URL"]
              ?? "http://order-service:8080";
    Console.WriteLine($"=== [ApiGateway] OrderService URL: {url} ===");
    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("payments", client =>
{
    var url = builder.Configuration["Services:PaymentsService"] 
              ?? builder.Configuration["PAYMENTS_SERVICE_URL"]
              ?? "http://payments-service:8080";
    Console.WriteLine($"=== [ApiGateway] PaymentsService URL: {url} ===");
    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Gateway v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();