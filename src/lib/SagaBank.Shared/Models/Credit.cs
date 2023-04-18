using MemoryPack;

namespace SagaBank.Shared.Models;

[MemoryPackable]
public partial class Credit
{
    public Ulid TransactionId { get; set; }
    public int CreditAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}