# BankSync V2 architecture

```
v2/
  src/BS2.Domain          entities + enums + CategorySeed. No dependencies.
  src/BS2.Application     abstractions (IAppDbContext, IBankingProvider, ITransactionClassifier, ...),
                          options, pure rules (TransactionRules, Iban), use-case services per feature folder.
  src/BS2.Infrastructure  EF Core (Npgsql, snake_case, schema "banksync"), AES-GCM field encryption,
                          Enable Banking client, OpenAI classifier, SMTP mail, Excel import.
  src/BS2.Api             minimal API endpoints per feature, Auth0 JWT, allow-list, background sync worker,
                          serves the built Vue app from wwwroot.
  tests/BS2.Tests         xUnit. Pure unit tests; EF InMemory where a DbContext is unavoidable. No Testcontainers.
  client/                 Vue 3 + Vite + TS + Pinia + Router + PrimeVue + ECharts + @auth0/auth0-vue.
```

## Conventions

- One `DependencyInjection`-style extension method per feature folder (`AddEnableBanking`, `AddClassification`, `AddSync`, ...). `Program.cs` composes them.
- Application services are plain classes registered scoped. No MediatR.
- Every query filters on `ICurrentUser.UserId`. Background work uses `StaticCurrentUser`.
- Timestamps are UTC `DateTime`; transaction dates are `DateOnly`.
- `[Encrypted]` string properties are encrypted by a model-wide EF value converter. Never filter on them in SQL; look up via a hash column (`Account.IdentifierHash`) or filter in memory.
- Enum columns are stored as strings.
- Tests live in `tests/BS2.Tests/<Feature>/`. Hand-written fakes, no mocking library.

## Encryption at rest

`AesGcmFieldEncryptor`: AES-256-GCM, random nonce per value, format `enc:v1:<base64>`. Key from `Encryption:MasterKey` (base64, 32 bytes). `Hash` = HMAC-SHA256 under an HKDF-derived subkey.
Plain columns: amount, currency, dates, category, reimbursed, external ids, bank names. Encrypted: counterparty, description, raw payloads, IBANs, session/account ids, prompts, URLs.

## Income / expense rules

`TransactionRules.IsExpense`: amount < 0 and category kind != Transfer. `IsIncome`: amount > 0 and kind != Transfer. Uncategorized rows count by sign. Reimbursed rows are included unless the filter excludes them.
