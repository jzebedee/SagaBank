using MemoryPack;

namespace SagaBank.Shared.Models;

[MemoryPackable]
public partial class Debit
{
    public Ulid TransactionId { get; set; }
    public int DebitAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}