namespace SagaBank.Backend.Models;

public class Account
{
    public int AccountId { get; set; }

    public ICollection<Transaction> Debits { get; set; }
    public ICollection<Transaction> Credits { get; set; }
}
