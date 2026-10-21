import { api } from './client'
import type {
  BulkTransactionBody,
  ClassificationRunDto,
  Guid,
  PatchTransactionBody,
  TransactionDto,
  TransactionPage,
  TransactionQuery,
} from './types'

export const listTransactions = (query: TransactionQuery) =>
  api<TransactionPage>('/transactions', { query: { ...query } })
export const getTransaction = (id: Guid) => api<TransactionDto>(`/transactions/${id}`)
export const patchTransaction = (id: Guid, body: PatchTransactionBody) =>
  api<TransactionDto>(`/transactions/${id}`, { method: 'PATCH', body })
export const bulkUpdateTransactions = (body: BulkTransactionBody) =>
  api<{ updated: number }>('/transactions/bulk', { method: 'POST', body })
export const listClassifications = (id: Guid) => api<ClassificationRunDto[]>(`/transactions/${id}/classifications`)
export const classifyTransaction = (id: Guid, force = false) =>
  api<ClassificationRunDto>(`/transactions/${id}/classify`, { method: 'POST', query: { force: force || undefined } })
