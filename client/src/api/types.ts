// Mirrors v2/docs/api-contract.md. Dates are 'YYYY-MM-DD', timestamps full ISO 8601 UTC.
export type Guid = string
export type DateString = string
export type Timestamp = string

export type CategoryKind = 'Expense' | 'Income' | 'Transfer'
export type ClassificationSource = 'None' | 'Ai' | 'Manual' | 'Import'
export type TransactionSource = 'EnableBanking' | 'Edenred' | 'ExcelImport'
export type ClassificationTrigger = 'Sync' | 'Manual' | 'Import' | 'Backfill'
export type BankProvider = 'EnableBanking' | 'Edenred' | 'Import'
export type ConnectionStatus = 'NotAuthorized' | 'Active' | 'Expired' | 'Revoked'
export type SyncStatus = 'Running' | 'Succeeded' | 'PartiallySucceeded' | 'Failed'

export interface CommonFilter {
  from?: DateString
  to?: DateString
  accountIds?: Guid[]
  categoryIds?: number[]
  excludeCategoryIds?: number[]
  groupIds?: Guid[]
  excludeGroupIds?: Guid[]
  excludeReimbursed?: boolean
  search?: string
}

export interface MeDto {
  id: Guid
  email: string
  displayName: string
  isAdmin: boolean
}

export interface CategoryDto {
  id: number
  code: string
  name: string
  kind: CategoryKind
  sortOrder: number
  isActive: boolean
  color: string | null
  categoryClassId: number | null
}
export interface CreateCategoryBody {
  code: string
  name: string
  kind: CategoryKind
  color?: string | null
  categoryClassId?: number | null
}
export interface UpdateCategoryBody {
  name: string
  kind: CategoryKind
  color?: string | null
  isActive: boolean
  sortOrder: number
  categoryClassId: number | null
}

export interface CategoryClassDto {
  id: number
  name: string
  color: string | null
  sortOrder: number
  categoryCount: number
}
export interface CategoryClassBody {
  name: string
  color?: string | null
  sortOrder?: number
}

export interface GroupDto {
  id: Guid
  name: string
  color: string | null
  transactionCount: number
}
export interface GroupBody {
  name: string
  color?: string | null
}

export interface AccountDto {
  id: Guid
  bankConnectionId: Guid
  bankName: string
  identifier: string
  displayName: string | null
  currency: string
  isActive: boolean
  transactionCount: number
}
export interface UpdateAccountBody {
  displayName?: string | null
  isActive: boolean
}

export interface LatestClassification {
  predictedCategoryId: number | null
  confidence: number
  accepted: boolean
  createdAt: Timestamp
}

export interface TransactionDto {
  id: Guid
  accountId: Guid
  accountName: string
  bankName: string
  source: TransactionSource
  date: DateString
  bookingDate: DateString | null
  valueDate: DateString | null
  amount: number
  currency: string
  counterpartyName: string | null
  counterpartyIban: string | null
  description: string | null
  notes: string | null
  categoryId: number | null
  classificationSource: ClassificationSource
  classifiedAt: Timestamp | null
  reimbursed: boolean
  groupIds: Guid[]
  latestClassification: LatestClassification | null
}

export type TransactionSort = 'date' | 'amount' | 'counterpartyName' | 'category'
export interface TransactionQuery extends CommonFilter {
  unclassifiedOnly?: boolean
  page?: number
  pageSize?: number
  sort?: TransactionSort
  dir?: 'asc' | 'desc'
}
export interface TransactionPage {
  items: TransactionDto[]
  total: number
  page: number
  pageSize: number
  sumAmount: number
}
export interface PatchTransactionBody {
  categoryId?: number | null
  reimbursed?: boolean
  notes?: string | null
  groupIds?: Guid[]
}
export interface BulkTransactionBody {
  ids: Guid[]
  categoryId?: number | null
  reimbursed?: boolean
  addGroupIds?: Guid[]
  removeGroupIds?: Guid[]
}

export interface ClassificationAlternative {
  code: string
  logprob: number
  probability: number
}
export interface ClassificationRunDto {
  id: Guid
  trigger: ClassificationTrigger
  model: string
  createdAt: Timestamp
  predictedCategoryId: number | null
  predictedCategoryCode: string | null
  confidence: number
  threshold: number
  accepted: boolean
  alternatives: ClassificationAlternative[]
  promptTokens: number
  completionTokens: number
  latencyMs: number
  error: string | null
  systemPrompt: string | null
  userPrompt: string | null
  rawResponse: string | null
}

export interface ExpenseCategoryRow {
  categoryId: number | null
  code: string
  name: string
  color: string | null
  total: number
  count: number
  share: number
  categoryClassId: number | null
  categoryClassName: string | null
}
export interface ExpenseClassRow {
  categoryClassId: number | null
  name: string
  color: string | null
  total: number
  count: number
  share: number
}
export interface ExpensesAnalytics {
  total: number
  count: number
  categories: ExpenseCategoryRow[]
  classes: ExpenseClassRow[]
}

export interface IncomeByCategory {
  categoryId: number | null
  code: string
  name: string
  total: number
  categoryClassId: number | null
}
export interface IncomeByClass {
  categoryClassId: number | null
  name: string
  total: number
}
export interface IncomeMonth {
  month: string
  total: number
  byCategory: IncomeByCategory[]
  byClass: IncomeByClass[]
}
export interface IncomeAnalytics {
  total: number
  months: IncomeMonth[]
}

export interface SavingsMonth {
  month: string
  income: number
  expenses: number
  savings: number
  cumulative: number
}
export interface SavingsAnalytics {
  totalIncome: number
  totalExpenses: number
  totalSavings: number
  months: SavingsMonth[]
}

export interface SummaryAnalytics {
  transactionCount: number
  unclassifiedCount: number
  firstDate: DateString | null
  lastDate: DateString | null
  lastSyncAt: Timestamp | null
}

export interface AspspDto {
  name: string
  country: string
  logoUrl: string | null
  psuTypes: string[]
  maxConsentValidityDays: number
  supportsAccountPreselection: boolean
}

export interface PendingAuthorization {
  url: string
  createdAt: Timestamp
  expiresAt: Timestamp
}
export interface BankConnectionDto {
  id: Guid
  provider: BankProvider
  bankName: string
  country: string
  psuType: string | null
  selectAccountsAtBank: boolean
  consentValidityDays: number | null
  configuredIbans: string[]
  status: ConnectionStatus
  validUntil: Timestamp | null
  lastAuthorizedAt: Timestamp | null
  lastSyncedAt: Timestamp | null
  lastSyncError: string | null
  accounts: AccountDto[]
  pendingAuthorization: PendingAuthorization | null
}
export interface BankConnectionBody {
  bankName: string
  country: string
  psuType?: string | null
  selectAccountsAtBank?: boolean
  consentValidityDays?: number | null
  ibans?: string[]
}
export interface AuthorizeResult {
  url: string
  state: string
  expiresAt: Timestamp
}

export interface SyncRunDetail {
  bankName: string
  status: string
  message: string | null
  fetched: number
  new: number
}
export interface SyncRunDto {
  id: Guid
  trigger: string
  status: SyncStatus
  startedAt: Timestamp
  finishedAt: Timestamp | null
  connectionsSynced: number
  connectionsSkipped: number
  transactionsFetched: number
  transactionsNew: number
  transactionsClassified: number
  error: string | null
  details: SyncRunDetail[]
}
export interface SyncStatusDto {
  running: boolean
  lastRun: SyncRunDto | null
  nextScheduledAt: Timestamp | null
}

export interface ImportResult {
  rows: number
  imported: number
  updated: number
  skippedDuplicates: number
  categoriesCreated: number
  groupsCreated: number
  accountsCreated: number
  errors: string[]
}

export interface CompleteAuthorizationResult {
  success: boolean
  bank: string
  message: string
}
