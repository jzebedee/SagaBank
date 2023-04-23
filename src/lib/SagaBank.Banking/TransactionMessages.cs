using MemoryPack;
using System.Text.Json.Serialization;

namespace SagaBank.Banking;

[MemoryPackable]
[MemoryPackUnion(0, typeof(TransactionStarting))]
[MemoryPackUnion(1, typeof(TransactionStartFailed))]
[MemoryPackUnion(2, typeof(TransactionUpdateBalanceAvailable))]
[MemoryPackUnion(3, typeof(TransactionUpdateBalanceAvailableCompensation))]
[MemoryPackUnion(4, typeof(TransactionUpdateBalanceAvailableFailed))]
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(ITransactionSaga), "base")]
[JsonDerivedType(typeof(TransactionStarting), "starting")]
[JsonDerivedType(typeof(TransactionStartFailed), "start-failed")]
[JsonDerivedType(typeof(TransactionUpdateBalanceAvailable), "update-bal-avail")]
[JsonDerivedType(typeof(TransactionUpdateBalanceAvailableCompensation), "update-bal-avail-compensating")]
[JsonDerivedType(typeof(TransactionUpdateBalanceAvailableFailed), "update-bal-avail-failed")]
public partial interface ITransactionSaga { }

[MemoryPackable]
public partial record TransactionStarting(Ulid TransactionId, decimal Amount, int DebitAccountId, int CreditAccountId) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionStartFailed(Ulid TransactionId, IDictionary<string, string[]> Errors) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionUpdateBalanceAvailable(Ulid TransactionId, decimal Amount, int AccountId) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionUpdateBalanceAvailableCompensation(Ulid TransactionId, decimal Amount, int AccountId) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionUpdateBalanceAvailableFailed(Ulid TransactionId, IDictionary<string, string[]> Errors) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionKey(int DebitAccountId, int CreditAccountId);