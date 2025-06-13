namespace PaymentsService.Models;

public class Account
{
    public Guid UserId { get; set; }
    public decimal Balance { get; set; }
}