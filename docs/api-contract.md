# BankSync V2 API contract

Base path `/api`. All endpoints require `Authorization: Bearer <Auth0 access token>` except `GET /api/banks/callback` and `GET /health`.
JSON, camelCase, dates as ISO strings (`2026-03-14` for dates, full ISO 8601 UTC for timestamps). Money as JSON numbers (decimal, 2 places).
Errors: RFC 7807 `application/problem+json` with `status`, `title`, `detail`. Validation errors: 400 with `errors: { field: [messages] }`. Unknown row: 404. Not allow-listed user: 403. Admin-only endpoints (category and category-class writes, Excel import) return 403 for non-admins.

## Common filter (query string)

Every list/analytics endpoint accepts these. Omitted = not applied.

| param | type | meaning |
|---|---|---|
| `from` | date | inclusive lower bound on `date` |
| `to` | date | inclusive upper bound on `date` |
| `accountIds` | guid[] (repeat or comma) | only these accounts |
| `categoryIds` | int[] | only these categories (`0` = uncategorized) |
| `excludeCategoryIds` | int[] | drop these categories |
| `groupIds` | guid[] | only transactions carrying any of these groups |
| `excludeGroupIds` | guid[] | drop transactions carrying any of these groups |
| `excludeReimbursed` | bool | drop rows with `reimbursed = true` |
| `search` | string | case-insensitive substring on counterparty name / description (applied in memory after decrypt) |

## Me

`GET /api/me` → `{ id, email, displayName, isAdmin }`

## Categories

`GET /api/categories` → `CategoryDto[]`
`CategoryDto = { id, code, name, kind: "Expense"|"Income"|"Transfer", sortOrder, isActive, color }`
`POST /api/categories` body `{ code, name, kind, color? }` → 201 `CategoryDto`
`PUT /api/categories/{id}` body `{ name, kind, color?, isActive, sortOrder }` → `CategoryDto` (code immutable)

## Groups

`GET /api/groups` → `GroupDto[]` ; `GroupDto = { id, name, color, transactionCount }`
`POST /api/groups` body `{ name, color? }` → 201 `GroupDto`
`PUT /api/groups/{id}` body `{ name, color? }` → `GroupDto`
`DELETE /api/groups/{id}` → 204 (detaches from transactions)

## Accounts

`GET /api/accounts` → `AccountDto[]`
`AccountDto = { id, bankConnectionId, bankName, identifier, displayName, currency, isActive, transactionCount }`
`PUT /api/accounts/{id}` body `{ displayName?, isActive }` → `AccountDto` (`displayName` omitted/null = keep, `""` = clear)

## Transactions

`GET /api/transactions?<common filter>&unclassifiedOnly=bool&page=1&pageSize=50&sort=date|amount|counterpartyName|category&dir=asc|desc`
→ `{ items: TransactionDto[], total, page, pageSize, sumAmount }`

```
TransactionDto = {
  id, accountId, accountName, bankName, source: "EnableBanking"|"Edenred"|"ExcelImport",
  date, bookingDate, valueDate, amount, currency,
  counterpartyName, counterpartyIban, description, notes,
  categoryId, classificationSource: "None"|"Ai"|"Manual"|"Import", classifiedAt,
  reimbursed, groupIds: guid[],
  latestClassification: { predictedCategoryId, confidence, accepted, createdAt } | null
}
```

`GET /api/transactions/{id}` → `TransactionDto`
`PATCH /api/transactions/{id}` body (all optional) `{ categoryId: int|null, reimbursed, notes: string|null, groupIds: guid[] }` → `TransactionDto`. Absent key = unchanged; `categoryId: null` clears the category (source `None`); `notes: null` clears notes. Setting `categoryId` marks `classificationSource = Manual`. `groupIds` replaces the set.
`POST /api/transactions/bulk` body `{ ids: guid[], categoryId?: int|null, reimbursed?: bool, addGroupIds?: guid[], removeGroupIds?: guid[] }` → `{ updated: int }`
`GET /api/transactions/{id}/classifications` → `ClassificationRunDto[]` newest first
`POST /api/transactions/{id}/classify` → `ClassificationRunDto` (calls the model now; applies category only if accepted AND transaction not manually classified; `force=true` query overrides manual)

```
ClassificationRunDto = {
  id, trigger: "Sync"|"Manual"|"Import"|"Backfill", model, createdAt,
  predictedCategoryId, predictedCategoryCode, confidence, threshold, accepted,
  alternatives: [{ code, logprob, probability }], promptTokens, completionTokens, latencyMs, error,
  systemPrompt, userPrompt, rawResponse
}
```

## Analytics

All accept the common filter. `Transfer`-kind categories are always excluded from income/expenses. Expense totals are returned **positive** (absolute values).

`GET /api/analytics/expenses` → `{ total, count, categories: [{ categoryId, code, name, color, total, count, share }] }` (`share` 0..1; uncategorized rows appear with `categoryId: null, code: "Uncategorized"`)

`GET /api/analytics/income` → `{ total, months: [{ month: "2026-01", total, byCategory: [{ categoryId, code, name, total }] }] }` (months contiguous from `from` to `to`, zero-filled)

`GET /api/analytics/savings` → `{ totalIncome, totalExpenses, totalSavings, months: [{ month, income, expenses, savings, cumulative }] }` (zero-filled, contiguous)

`GET /api/analytics/summary` → `{ transactionCount, unclassifiedCount, firstDate, lastDate, lastSyncAt }` (counts/dates honour the common filter; `lastSyncAt` is user-wide)

## Banks

`GET /api/banks/aspsps?country=BE` → `[{ name, country, logoUrl, psuTypes: string[], maxConsentValidityDays, supportsAccountPreselection }]`

`GET /api/banks/connections` → `BankConnectionDto[]`
```
BankConnectionDto = {
  id, provider: "EnableBanking"|"Edenred"|"Import", bankName, country, psuType, selectAccountsAtBank,
  consentValidityDays, configuredIbans: string[],
  status: "NotAuthorized"|"Active"|"Expired"|"Revoked", validUntil, lastAuthorizedAt, lastSyncedAt, lastSyncError,
  accounts: AccountDto[],
  pendingAuthorization: { url, createdAt, expiresAt } | null
}
```
`POST /api/banks/connections` body `{ bankName, country, psuType?, selectAccountsAtBank?, consentValidityDays?, ibans?: string[] }` → 201 `BankConnectionDto`
`PUT /api/banks/connections/{id}` same body → `BankConnectionDto`
`DELETE /api/banks/connections/{id}` → 204 (deletes provider session; accounts/transactions kept, connection status Revoked if it has transactions, else hard delete)
`POST /api/banks/connections/{id}/authorize` → `{ url, state, expiresAt }` — starts (or returns the still-valid outstanding) authorization. Front end does `window.location = url`.
`POST /api/banks/connections/{id}/refresh` → `BankConnectionDto` — re-reads session from provider, updates status/validUntil/accounts.
`GET /api/banks/callback?code&state&error` — **anonymous**. Completes nothing: 302-redirects to `{clientOrigin}/banks?bankCode=<code>&bankState=<state>`, or on a bank error to `{clientOrigin}/banks?result=error&message=<fixed text>`. The params are not named `code`/`state` because the Auth0 SDK claims those as its own callback.
`POST /api/banks/callback` body `{ code, state }` → `{ success, bank, message }` — completes the authorization only if the signed-in user started it; otherwise it answers like an unknown state.

## Sync

`POST /api/sync` → 202 `SyncRunDto` (rejects with 409 if a run is already in progress for this user)
`GET /api/sync/runs?take=10` → `SyncRunDto[]` newest first
`GET /api/sync/status` → `{ running: bool, lastRun: SyncRunDto | null, nextScheduledAt }`
```
SyncRunDto = { id, trigger, status: "Running"|"Succeeded"|"PartiallySucceeded"|"Failed", startedAt, finishedAt,
  connectionsSynced, connectionsSkipped, transactionsFetched, transactionsNew, transactionsClassified, error,
  details: [{ bankName, status, message, fetched, new }] }
```

## Import (admin only)

`POST /api/import/excel` multipart `file` → `{ rows, imported, skippedDuplicates, groupsCreated, accountsCreated, errors: string[] }`

## Auth0 (frontend)

SPA uses `@auth0/auth0-vue` with `authorizationParams: { audience }`. Token sent as Bearer. Config read from `VITE_AUTH0_DOMAIN`, `VITE_AUTH0_CLIENT_ID`, `VITE_AUTH0_AUDIENCE`, `VITE_API_BASE` (default `/api`). Dev: Vite proxies `/api` to `https://localhost:8080` (self-signed, `secure: false`).

## Category classes (added 2026-10-04)

A class groups categories for reporting (Food, Housing, Fun, ...). Global, like categories. A category has at most one class.

`CategoryClassDto = { id, name, color, sortOrder, categoryCount }`
`GET /api/category-classes` → `CategoryClassDto[]`
`POST /api/category-classes` body `{ name, color? }` → 201
`PUT /api/category-classes/{id}` body `{ name, color?, sortOrder }`
`DELETE /api/category-classes/{id}` → 204 (categories in it become unclassed)

`CategoryDto` gains `categoryClassId: int|null`. `POST /api/categories` body gains optional `categoryClassId`; `PUT /api/categories/{id}` body gains `categoryClassId: int|null`.

Analytics:
- `GET /api/analytics/expenses` → `categories[]` items gain `categoryClassId, categoryClassName`; response gains `classes: [{ categoryClassId, name, color, total, count, share }]` (unclassed rows: `categoryClassId: null, name: "Unclassed"`), sorted by total desc.
- `GET /api/analytics/income` → `months[].byCategory[]` items gain `categoryClassId`; `months[]` gains `byClass: [{ categoryClassId, name, total }]`.

Seed (ids fixed): 1 Income, 2 Housing, 3 Cost of living, 4 Food, 5 Fun, 6 Social, 7 Sports, 8 Unknown.
