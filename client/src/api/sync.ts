import { api } from './client'
import type { SyncRunDto, SyncStatusDto } from './types'

export const startSync = () => api<SyncRunDto>('/sync', { method: 'POST' })
export const listSyncRuns = (take = 10) => api<SyncRunDto[]>('/sync/runs', { query: { take } })
export const getSyncStatus = () => api<SyncStatusDto>('/sync/status')
