namespace OrderService.Models;

public class OrderOutbox
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Payload { get; set; } = default!;
    public bool IsSent { get; set; }
    public DateTime CreatedAt { get; set; }
}