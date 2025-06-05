using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OrderService.Data;
using OrderService.Services;
using OrderService.BackgroundServices;
using Confluent.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrderDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine($"=== [OrderService] Using connection string: {connectionString} ===");
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IOrderService, OrderService.Services.OrderService>();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? "kafka:9092",
        ClientId = "order-service-producer",
        Acks = Acks.All,
        EnableIdempotence = true,
        MaxInFlight = 5,
        MessageSendMaxRetries = 3,
        RetryBackoffMs = 100
    };
    
    Console.WriteLine($"=== [OrderService] Kafka Producer configured with servers: {config.BootstrapServers} ===");
    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IConsumer<string, string>>(sp =>
{
    var config = new ConsumerConfig
    {
        GroupId = "order-service-group",
        BootstrapServers = builder.Configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? "kafka:9092",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
        ClientId = "order-service-consumer"
    };
    
    Console.WriteLine($"=== [OrderService] Kafka Consumer configured with servers: {config.BootstrapServers} ===");
    return new ConsumerBuilder<string, string>(config).Build();
});

builder.Services.AddHostedService<OrderOutboxPublisher>();
builder.Services.AddHostedService<PaymentResultConsumer>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.WriteIndented = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "OrderService API", Version = "v1" });
});

var app = builder.Build();
try
{
    await Task.Delay(TimeSpan.FromSeconds(5)); // Даем время БД запуститься
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var retries = 5;
        while (retries > 0)
        {
            try
            {
                await db.Database.EnsureCreatedAsync();
                break;
            }
            catch (Exception ex)
            {
                retries--;
                Console.WriteLine($"=== [OrderService] Database connection failed, retries left: {retries}, error: {ex.Message} ===");
                if (retries == 0) throw;
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"=== [OrderService] Database initialization failed: {ex.Message} ===");
    Console.WriteLine($"=== [OrderService] Stack trace: {ex.StackTrace} ===");
    throw;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderService v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();