import { api } from './client'
import type { SyncRunDto, SyncStatusDto } from './types'

/** `from` (yyyy-MM-dd) fetches from that date instead of the default retrieval window. */
export const startSync = (from?: string) => api<SyncRunDto>('/sync', { method: 'POST', query: from ? { from } : undefined })
export const listSyncRuns = (take = 10) => api<SyncRunDto[]>('/sync/runs', { query: { take } })
export const getSyncStatus = () => api<SyncStatusDto>('/sync/status')
