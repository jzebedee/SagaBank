namespace SagaBank.Backend.Models;

public record TransactionRequest(decimal Amount, int DebitAccountId, int CreditAccountId);