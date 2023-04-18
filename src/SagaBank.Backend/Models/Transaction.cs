namespace SagaBank.Backend.Models;

public record Transaction(decimal Amount, int DebitAccountId, int CreditAccountId);