namespace SagaBank.Backend.Models;

public class Account
{
    public int AccountId { get; set; }
    public decimal Balance { get; set; }
    public bool Frozen { get; set; }
}