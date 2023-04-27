namespace SagaBank.Backend.Models;

public record BackendTransactionRequest(decimal Amount, int DebitAccountId, int CreditAccountId);