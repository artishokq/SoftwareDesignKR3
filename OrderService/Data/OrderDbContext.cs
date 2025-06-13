using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) {
    }

    public DbSet<Order> Orders { get; set; } = default!;
    public DbSet<OrderOutbox> Outbox { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Таблица Orders
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasColumnName("user_id");
            entity.Property(e => e.Amount).IsRequired().HasColumnName("amount");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Status).IsRequired().HasColumnName("status");
        });

        // Таблица Outbox
        modelBuilder.Entity<OrderOutbox>(entity =>
        {
            entity.ToTable("order_outbox");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderId).IsRequired().HasColumnName("order_id");
            entity.Property(e => e.Payload).IsRequired().HasColumnName("payload");
            entity.Property(e => e.IsSent).IsRequired().HasColumnName("is_sent");
            entity.Property(e => e.CreatedAt).IsRequired().HasColumnName("created_at");
        });
    }
}