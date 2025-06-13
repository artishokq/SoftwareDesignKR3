namespace PaymentsService.Models;

/// <summary>
/// Transactional Inbox: поступившая задача на оплату заказа
/// Хранит все полученные из Kafka сообщения, чтобы обработать их ровно один раз
/// </summary>
public class PaymentInbox
{
    // Ключ Kafka-сообщения
    public string MessageKey { get; set; } = default!;
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public bool Processed { get; set; }
    public DateTime CreatedAt { get; set; }
}