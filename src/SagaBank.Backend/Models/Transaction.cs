namespace SagaBank.Backend.Models;

public class Transaction
{
    public Ulid TransactionId { get; set; }
    public decimal Amount { get; set; }
    public int DebitAccountId { get; set; }
    public Account DebitAccount { get; set; }
    public int CreditAccountId { get; set; }
    public Account CreditAccount { get; set; }
}

public record TransactionRequest(decimal Amount, int DebitAccountId, int CreditAccountId);