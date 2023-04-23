# SagaBank

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

Validation -- "Debit/Credit Account Exists" --> UD_BalA[Update Debit: Balance Available]
Validation --> V_Fail[Failed] -- "Error Only" --> Validation

UD_BalA --> UC_Bal[Update Credit: Balance]
UD_BalA --> UD_BalA_Fail[Failed] -- "Rollback Debit Balance Available" --> UD_BalA

UC_Bal --> UD_Bal[Update Debit: Balance]
UC_Bal --> UC_Bal_Fail[Failed] -- "Rollback Credit Balance" --> UC_Bal

UD_Bal --> UC_BalA[Update Credit: Balance Available]
UD_Bal --> UD_Bal_Fail[Failed] -- "Rollback Debit Balance" --> UD_Bal

UC_BalA --> Upd_DB[Update Database]
UC_BalA --> UC_BalA_Fail[Failed] -- "Rollback Credit Balance Available" --> UC_BalA

Upd_DB --> Success
Upd_DB --> Upd_DB_Fail[Failed] -- "Rollback" --> Upd_DB
```

## Different bank
```json
{
    "amount": 10,
    "debit": {
        "bank": "debco",
        "accountid": 0
    },
    "credit": {
        "bank": "credco",
        "accountid": 1
    }
}
```
