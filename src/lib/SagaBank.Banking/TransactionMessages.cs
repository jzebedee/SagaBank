using MemoryPack;
using System.Text.Json.Serialization;

namespace SagaBank.Banking;

[MemoryPackable]
[MemoryPackUnion(0, typeof(TransactionStarting))]
[MemoryPackUnion(1, typeof(TransactionStartFailed))]
[MemoryPackUnion(2, typeof(TransactionUpdateBalanceAvailable))]
[MemoryPackUnion(3, typeof(TransactionUpdateBalanceAvailableCompensation))]
[MemoryPackUnion(4, typeof(TransactionUpdateBalanceAvailableFailed))]
[MemoryPackUnion(5, typeof(TransactionUpdateBalanceAvailableSuccess))]
[MemoryPackUnion(6, typeof(TransactionUpdateCredit))]
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(ITransactionSaga), "base")]
[JsonDerivedType(typeof(TransactionStarting), "starting")]
[JsonDerivedType(typeof(TransactionStartFailed), "start-failed")]
[JsonDerivedType(typeof(TransactionUpdateBalanceAvailable), "update-bal-avail")]
[JsonDerivedType(typeof(TransactionUpdateBalanceAvailableCompensation), "update-bal-avail-compensating")]
[JsonDerivedType(typeof(TransactionUpdateBalanceAvailableFailed), "update-bal-avail-failed")]
[JsonDerivedType(typeof(TransactionUpdateBalanceAvailableSuccess), "update-bal-avail-success")]
[JsonDerivedType(typeof(TransactionUpdateCredit), "update-cred")]
public partial interface ITransactionSaga
{
    TransactionRequest Request { get; }
}

[MemoryPackable]
public partial record TransactionStarting(TransactionRequest Request) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionStartFailed(TransactionRequest Request, IDictionary<string, string[]> Errors) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionUpdateBalanceAvailable(TransactionRequest Request, decimal Amount, int AccountId) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionUpdateBalanceAvailableCompensation(TransactionRequest Request, decimal Amount, int AccountId) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionUpdateBalanceAvailableFailed(TransactionRequest Request, IDictionary<string, string[]> Errors) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionUpdateBalanceAvailableSuccess(TransactionRequest Request, decimal Amount, int AccountId) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionUpdateCredit(TransactionRequest Request, decimal Amount, int AccountId) : ITransactionSaga;

[MemoryPackable]
public partial record TransactionKey(int DebitAccountId);

[MemoryPackable]
public partial record TransactionRequest(Ulid TransactionId, decimal Amount, int DebitAccountId, int CreditAccountId);
