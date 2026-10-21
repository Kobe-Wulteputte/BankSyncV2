import { defineStore } from 'pinia'
import type { LocationQuery, LocationQueryRaw } from 'vue-router'
import type { CommonFilter } from '@/api/types'
import { detectPreset, presetRange, type DatePreset } from '@/utils/dateRange'
import { toSearchParams, type QueryValue } from '@/utils/query'

const STORAGE_KEY = 'bs2.filter'
export const FILTER_QUERY_KEYS = [
  'from',
  'to',
  'excludeCategoryIds',
  'accountIds',
  'excludeGroupIds',
  'excludeReimbursed',
  'search',
] as const

export interface FilterState {
  preset: DatePreset
  from: string | null
  to: string | null
  excludeCategoryIds: number[]
  accountIds: string[]
  excludeGroupIds: string[]
  excludeReimbursed: boolean
  search: string
}

function defaults(): FilterState {
  return {
    preset: 'thisMonth',
    ...presetRange('thisMonth'),
    excludeCategoryIds: [],
    accountIds: [],
    excludeGroupIds: [],
    excludeReimbursed: true,
    search: '',
  }
}

function loadStored(): FilterState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return defaults()
    const stored = { ...defaults(), ...(JSON.parse(raw) as Partial<FilterState>) }
    // presets are relative to today; recompute instead of trusting the stored bounds
    if (stored.preset !== 'custom') Object.assign(stored, presetRange(stored.preset))
    return stored
  } catch {
    return defaults()
  }
}

const first = (v: LocationQuery[string]): string | undefined => (Array.isArray(v) ? v[0] : v) ?? undefined
const list = (v: LocationQuery[string]): string[] =>
  (Array.isArray(v) ? v : [v]).flatMap((x) => (x ?? '').split(',')).filter(Boolean)

export const useFilterStore = defineStore('filter', {
  state: (): FilterState => loadStored(),
  getters: {
    /** Common-filter object for the API modules (undefined = not applied). */
    apiFilter(state): CommonFilter {
      return {
        from: state.from ?? undefined,
        to: state.to ?? undefined,
        excludeCategoryIds: state.excludeCategoryIds.length ? state.excludeCategoryIds : undefined,
        accountIds: state.accountIds.length ? state.accountIds : undefined,
        excludeGroupIds: state.excludeGroupIds.length ? state.excludeGroupIds : undefined,
        excludeReimbursed: state.excludeReimbursed || undefined,
        search: state.search.trim() || undefined,
      }
    },
  },
  actions: {
    setPreset(preset: DatePreset) {
      this.preset = preset
      if (preset !== 'custom') Object.assign(this, presetRange(preset))
    },
    setCustomRange(from: string | null, to: string | null) {
      this.preset = 'custom'
      this.from = from
      this.to = to
    },
    reset() {
      Object.assign(this, defaults())
    },
    /** API query string per the contract (comma-joined arrays), without the leading '?'. */
    toQuery(extra: Record<string, QueryValue> = {}): string {
      return toSearchParams({ ...this.apiFilter, ...extra }).toString()
    },
    /** Router query object (shareable URL). Only non-default keys are written. */
    toUrlQuery(): LocationQueryRaw {
      const q: LocationQueryRaw = {}
      if (this.from) q.from = this.from
      if (this.to) q.to = this.to
      if (this.excludeCategoryIds.length) q.excludeCategoryIds = this.excludeCategoryIds.join(',')
      if (this.accountIds.length) q.accountIds = this.accountIds.join(',')
      if (this.excludeGroupIds.length) q.excludeGroupIds = this.excludeGroupIds.join(',')
      q.excludeReimbursed = String(this.excludeReimbursed)
      if (this.search.trim()) q.search = this.search.trim()
      return q
    },
    fromUrlQuery(q: LocationQuery) {
      const from = first(q.from) ?? null
      const to = first(q.to) ?? null
      this.from = from
      this.to = to
      this.preset = detectPreset({ from, to })
      this.excludeCategoryIds = list(q.excludeCategoryIds).map(Number).filter(Number.isInteger)
      this.accountIds = list(q.accountIds)
      this.excludeGroupIds = list(q.excludeGroupIds)
      const er = first(q.excludeReimbursed)
      this.excludeReimbursed = er === undefined ? true : er === 'true'
      this.search = first(q.search) ?? ''
    },
    persist() {
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(this.$state))
      } catch {
        // storage unavailable; nothing to do
      }
    },
  },
})

export const hasFilterQuery = (q: LocationQuery): boolean => FILTER_QUERY_KEYS.some((k) => k in q)
