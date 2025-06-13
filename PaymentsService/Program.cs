using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using PaymentsService.Data;
using PaymentsService.Services;
using PaymentsService.BackgroundServices;
using Confluent.Kafka;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine($"=== [PaymentsService] Using connection string: {connectionString} ===");
    options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IAccountService, AccountService>();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? "kafka:9092",
        ClientId = "payments-service-producer",
        Acks = Acks.All,
        EnableIdempotence = true,
        MaxInFlight = 5,
        MessageSendMaxRetries = 3,
        RetryBackoffMs = 100
    };
    
    Console.WriteLine($"=== [PaymentsService] Kafka Producer configured with servers: {config.BootstrapServers} ===");
    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IConsumer<string, string>>(sp =>
{
    var config = new ConsumerConfig
    {
        GroupId = "payments-service-group",
        BootstrapServers = builder.Configuration["KAFKA_BOOTSTRAP_SERVERS"] ?? "kafka:9092",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
        ClientId = "payments-service-consumer"
    };
    
    Console.WriteLine($"=== [PaymentsService] Kafka Consumer configured with servers: {config.BootstrapServers} ===");
    return new ConsumerBuilder<string, string>(config).Build();
});

builder.Services.AddHostedService<PaymentProcessor>();
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.WriteIndented = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PaymentsService API", Version = "v1" });
});

var app = builder.Build();
try
{
    await Task.Delay(TimeSpan.FromSeconds(5)); // Даем время БД запуститься
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        
        // Повторяем попытки подключения
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
                Console.WriteLine($"=== [PaymentsService] Database connection failed, retries left: {retries}, error: {ex.Message} ===");
                if (retries == 0) throw;
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"=== [PaymentsService] Database initialization failed: {ex.Message} ===");
    Console.WriteLine($"=== [PaymentsService] Stack trace: {ex.StackTrace} ===");
    throw;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PaymentsService v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();