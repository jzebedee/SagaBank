namespace SagaBank.Backend.Models;

public class InternalAccount : Account
{
    public decimal Balance { get; set; }
    public decimal BalanceAvailable { get; set; }
    public bool Frozen { get; set; }
}