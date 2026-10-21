const moneyFormats = new Map<string, Intl.NumberFormat>()
const dateFormat = new Intl.DateTimeFormat('nl-BE', { day: '2-digit', month: '2-digit', year: 'numeric' })
const dateTimeFormat = new Intl.DateTimeFormat('nl-BE', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})
const monthFormat = new Intl.DateTimeFormat('nl-BE', { month: 'short', year: 'numeric' })
const percentFormat = new Intl.NumberFormat('nl-BE', { style: 'percent', maximumFractionDigits: 1 })

export function formatMoney(amount: number, currency = 'EUR'): string {
  let f = moneyFormats.get(currency)
  if (!f) {
    f = new Intl.NumberFormat('nl-BE', { style: 'currency', currency })
    moneyFormats.set(currency, f)
  }
  return f.format(amount)
}

/** 'YYYY-MM-DD' -> local Date (avoids the UTC shift of `new Date('YYYY-MM-DD')`). */
export function parseDate(iso: string): Date {
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number)
  return new Date(y!, m! - 1, d ?? 1)
}

/** local Date -> 'YYYY-MM-DD' */
export function toIsoDate(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

export function formatDate(iso: string | null | undefined): string {
  return iso ? dateFormat.format(parseDate(iso)) : ''
}

export function formatDateTime(iso: string | null | undefined): string {
  return iso ? dateTimeFormat.format(new Date(iso)) : ''
}

/** '2026-01' -> 'jan 2026' */
export function formatMonth(month: string): string {
  return monthFormat.format(parseDate(month))
}

/** 0.1234 -> '12,3%' */
export function formatPercent(ratio: number): string {
  return percentFormat.format(ratio)
}

/** Whole days from now until `iso`; negative when in the past. */
export function daysUntil(iso: string, now = new Date()): number {
  return Math.ceil((new Date(iso).getTime() - now.getTime()) / 86_400_000)
}

// Distinct, colour-blind-friendlier palette (Tableau 10 + 6 extras), no near-duplicates.
// 16 hues spread around the wheel with alternating lightness, so neighbours and id-modulo
// collisions never land on near-identical colours (the old list had two blues, two greens, ...).
export const PALETTE = [
  '#4e79a7', '#f28e2b', '#59a14f', '#e15759', '#edc948', '#7f3c8d', '#76b7b2', '#ff9da7',
  '#9c755f', '#1b9e77', '#e7298a', '#a6761d', '#1f3a93', '#8dd3c7', '#fdb462', '#6b6b6b',
] as const

/**
 * Colours for one chart: explicit colours are kept, the rest are handed out by rank so the
 * biggest series in that chart are as far apart as possible. Used where many series share a
 * stack and the global id-based colour would put similar hues next to each other.
 */
export function distinctColors<T>(items: T[], explicit: (item: T) => string | null | undefined): string[] {
  const used = new Set(items.map(explicit).filter((c): c is string => !!c))
  const pool = PALETTE.filter((c) => !used.has(c))
  let next = 0
  return items.map((item) => explicit(item) ?? pool[next++ % pool.length] ?? UNCATEGORIZED_COLOR)
}
export const UNCATEGORIZED_COLOR = '#8a8a8a'

export function categoryColor(cat: { id: number | null; color?: string | null } | null | undefined): string {
  if (!cat || cat.id === null || cat.id === undefined) return UNCATEGORIZED_COLOR
  if (cat.color) return cat.color.startsWith('#') ? cat.color : `#${cat.color}`
  return PALETTE[Math.abs(cat.id) % PALETTE.length]!
}

/** Class colour, else a deterministic palette entry; grey for null (Unclassed). */
export function classColor(cls: { id: number | null; color?: string | null } | null | undefined): string {
  return categoryColor(cls)
}
