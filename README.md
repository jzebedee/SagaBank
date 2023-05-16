# SagaBank

## Notes

* See also: [Saga Orchestration Serverless](https://github.com/Azure-Samples/saga-orchestration-serverless) for the Azure implementation

## Same bank
```json
{
    "amount": 10,
    "debitaccountid": 0,
    "creditaccountid": 1
}
```

### Graph
```mermaid
graph TD

Request --> Validation

Validation -- "Message Sanity Checks" --> UD_BalA[- Update Debit: Balance Available]
Validation --> V_Fail[Error Only] --> Validation

UD_BalA --> UC_Bal[+ Update Credit: Balance]
UD_BalA --> UD_BalA_Fail[Compensated] --> V_Fail

UC_Bal --> UD_Bal[- Update Debit: Balance]
UC_Bal --> UC_Bal_Fail[Rollback Debit: Balance Available +] --> UD_BalA_Fail

UD_Bal --> UC_BalA[+ Update Credit: Balance Available]
UD_Bal --> UD_Bal_Fail[Rollback Credit: Balance -] --> UC_Bal_Fail

UC_BalA --> Upd_DB[Update Database]
UC_BalA --> UC_BalA_Fail[Rollback Debit: Balance +] --> UD_Bal_Fail
```

