import { toIsoDate } from './format'

export type DatePreset =
  | 'thisMonth'
  | 'lastMonth'
  | 'thisYear'
  | 'lastYear'
  | 'last3Months'
  | 'last6Months'
  | 'last12Months'
  | 'allTime'
  | 'custom'

export const PRESET_OPTIONS: { label: string; value: DatePreset }[] = [
  { label: 'This month', value: 'thisMonth' },
  { label: 'Last month', value: 'lastMonth' },
  { label: 'This year', value: 'thisYear' },
  { label: 'Last year', value: 'lastYear' },
  { label: 'Last 3 months', value: 'last3Months' },
  { label: 'Last 6 months', value: 'last6Months' },
  { label: 'Last 12 months', value: 'last12Months' },
  { label: 'All time', value: 'allTime' },
  { label: 'Custom', value: 'custom' },
]

export interface DateRange {
  from: string | null
  to: string | null
}

const lastDayOf = (y: number, m: number) => new Date(y, m + 1, 0)

/** Range for a preset, as inclusive 'YYYY-MM-DD' bounds. `custom` returns nulls (caller keeps its own). */
export function presetRange(preset: DatePreset, now = new Date()): DateRange {
  const y = now.getFullYear()
  const m = now.getMonth()
  switch (preset) {
    case 'thisMonth':
      return { from: toIsoDate(new Date(y, m, 1)), to: toIsoDate(lastDayOf(y, m)) }
    case 'lastMonth':
      return { from: toIsoDate(new Date(y, m - 1, 1)), to: toIsoDate(lastDayOf(y, m - 1)) }
    case 'thisYear':
      return { from: toIsoDate(new Date(y, 0, 1)), to: toIsoDate(new Date(y, 11, 31)) }
    case 'lastYear':
      return { from: toIsoDate(new Date(y - 1, 0, 1)), to: toIsoDate(new Date(y - 1, 11, 31)) }
    case 'last3Months':
      return { from: toIsoDate(new Date(y, m - 2, 1)), to: toIsoDate(lastDayOf(y, m)) }
    case 'last6Months':
      return { from: toIsoDate(new Date(y, m - 5, 1)), to: toIsoDate(lastDayOf(y, m)) }
    case 'last12Months':
      return { from: toIsoDate(new Date(y, m - 11, 1)), to: toIsoDate(lastDayOf(y, m)) }
    case 'allTime':
    case 'custom':
      return { from: null, to: null }
  }
}

/** Which preset a from/to pair corresponds to today; `custom` when none matches. */
export function detectPreset(range: DateRange, now = new Date()): DatePreset {
  if (!range.from && !range.to) return 'allTime'
  for (const p of ['thisMonth', 'lastMonth', 'thisYear', 'lastYear', 'last3Months', 'last6Months', 'last12Months'] as const) {
    const r = presetRange(p, now)
    if (r.from === range.from && r.to === range.to) return p
  }
  return 'custom'
}

/** Number of calendar months touched by the range (>= 1). Used for "average per month". */
export function monthsInRange(range: DateRange, fallback: DateRange): number {
  const from = range.from ?? fallback.from
  const to = range.to ?? fallback.to
  if (!from || !to) return 1
  const [fy, fm] = from.split('-').map(Number)
  const [ty, tm] = to.split('-').map(Number)
  return Math.max(1, (ty! - fy!) * 12 + (tm! - fm!) + 1)
}
