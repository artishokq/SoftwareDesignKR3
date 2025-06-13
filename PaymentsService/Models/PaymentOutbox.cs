namespace PaymentsService.Models;

/// <summary>
/// Transactional Outbox: событие об успешном/неуспешном списании средств
/// </summary>
public class PaymentOutbox
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public bool IsSuccess { get; set; }
    public string Payload { get; set; } = default!;
    public bool IsSent { get; set; }
    public DateTime CreatedAt { get; set; }
}