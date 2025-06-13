using Microsoft.EntityFrameworkCore;
using PaymentsService.Models;

namespace PaymentsService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) {
    }

    public DbSet<Account> Accounts { get; set; } = default!;
    public DbSet<PaymentInbox> Inbox { get; set; } = default!;
    public DbSet<PaymentOutbox> Outbox { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Таблица accounts
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Balance).HasColumnName("balance").IsRequired();
        });

        // Таблица payment_inbox
        modelBuilder.Entity<PaymentInbox>(entity =>
        {
            entity.ToTable("payment_inbox");
            entity.HasKey(e => e.MessageKey);
            entity.Property(e => e.MessageKey).HasColumnName("message_key");
            entity.Property(e => e.OrderId).HasColumnName("order_id").IsRequired();
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount").IsRequired();
            entity.Property(e => e.Processed).HasColumnName("processed").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        });

        // Таблица payment_outbox
        modelBuilder.Entity<PaymentOutbox>(entity =>
        {
            entity.ToTable("payment_outbox");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id").IsRequired();
            entity.Property(e => e.IsSuccess).HasColumnName("is_success").IsRequired();
            entity.Property(e => e.Payload).HasColumnName("payload").IsRequired();
            entity.Property(e => e.IsSent).HasColumnName("is_sent").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        });
    }
}