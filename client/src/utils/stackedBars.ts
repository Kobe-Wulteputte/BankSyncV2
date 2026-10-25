import type { CategoryClassDto, CategoryDto, MonthlyMonth } from '@/api/types'
import type { EChartsOption } from '@/charts'
import type { GroupBy } from '@/composables'
import { distinctColors, formatMoney, formatMoneyCompact, UNCATEGORIZED_COLOR } from './format'

export interface BarSeries {
  name: string
  color: string
  data: number[]
}

/** One stacked series per category (or per class), zero-filled per period. */
export function buildSeries(
  periods: MonthlyMonth[],
  groupBy: GroupBy,
  categoryById: Map<number, CategoryDto>,
  classById: Map<number, CategoryClassDto>,
): BarSeries[] {
  const byKey = new Map<string, { name: string; explicit: string | null; data: number[] }>()
  const add = (key: string, name: string, explicit: string | null, i: number, total: number) => {
    let s = byKey.get(key)
    if (!s) {
      s = { name, explicit, data: new Array<number>(periods.length).fill(0) }
      byKey.set(key, s)
    }
    s.data[i] = total
  }
  periods.forEach((m, i) => {
    if (groupBy === 'class') {
      for (const c of m.byClass) {
        const explicit = c.categoryClassId === null ? UNCATEGORIZED_COLOR : (classById.get(c.categoryClassId)?.color ?? null)
        add(String(c.categoryClassId), c.name, explicit, i, c.total)
      }
    } else {
      for (const c of m.byCategory) {
        const explicit = c.categoryId === null ? UNCATEGORIZED_COLOR : (categoryById.get(c.categoryId)?.color ?? null)
        add(c.code, c.name, explicit, i, c.total)
      }
    }
  })
  // Biggest series first, then colours by rank: a category with a colour set in Settings keeps it,
  // everything else gets the most distinct remaining hue.
  const sorted = [...byKey.values()].sort((a, b) => b.data.reduce((x, y) => x + y, 0) - a.data.reduce((x, y) => x + y, 0))
  const colors = distinctColors(sorted, (s) => s.explicit)
  return sorted.map((s, i) => ({ name: s.name, color: colors[i]!, data: s.data }))
}

/** Sums months into calendar years, one entry per year in `years` ('YYYY'), so several charts share one axis. */
export function toYears(months: MonthlyMonth[], years: string[]): MonthlyMonth[] {
  return years.map((year) => {
    const byCategory = new Map<string, MonthlyMonth['byCategory'][number]>()
    const byClass = new Map<number | null, MonthlyMonth['byClass'][number]>()
    let total = 0
    for (const m of months.filter((x) => x.month.startsWith(year))) {
      total += m.total
      for (const c of m.byCategory) {
        const acc = byCategory.get(c.code)
        if (acc) acc.total += c.total
        else byCategory.set(c.code, { ...c })
      }
      for (const c of m.byClass) {
        const acc = byClass.get(c.categoryClassId)
        if (acc) acc.total += c.total
        else byClass.set(c.categoryClassId, { ...c })
      }
    }
    return { month: year, total, byCategory: [...byCategory.values()], byClass: [...byClass.values()] }
  })
}

/**
 * Stacked bar chart: one bar per period, one segment per series; the tooltip lists the non-zero ones and their total.
 * `horizontal` puts the periods on the y axis (first on top) and the amounts on the x axis.
 */
export function stackedBarOption(o: { labels: string[]; series: BarSeries[]; stack: string; mobile: boolean; horizontal?: boolean }): EChartsOption {
  const periodAxis = { type: 'category' as const, data: o.labels }
  // Full amounts crowd a horizontal value axis, so it always uses the compact form.
  const valueAxis = { type: 'value' as const, axisLabel: { formatter: (v: number) => (o.mobile || o.horizontal ? formatMoneyCompact(v) : formatMoney(v)) } }
  return {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'axis',
      confine: o.mobile,
      // Stacked bars report every series per period; hide the zero ones and add the period total.
      formatter: (params) => {
        const items = (Array.isArray(params) ? params : [params]) as { seriesName?: string; value?: unknown; marker?: string; axisValueLabel?: string }[]
        const rows = items.filter((p) => Number(p.value) !== 0)
        if (!rows.length) return ''
        const total = rows.reduce((sum, p) => sum + Number(p.value), 0)
        const lines = rows.map((p) => `${p.marker ?? ''}${p.seriesName} <span style="float:right;margin-left:1.5rem">${formatMoney(Number(p.value))}</span>`)
        return `<b>${rows[0]?.axisValueLabel ?? ''}</b><br/>${lines.join('<br/>')}<br/><b>Total <span style="float:right;margin-left:1.5rem">${formatMoney(total)}</span></b>`
      },
    },
    legend: { type: 'scroll', bottom: 0 },
    grid: o.horizontal
      ? { left: 48, right: o.mobile ? 16 : 24, top: 16, bottom: 60 }
      : o.mobile ? { left: 52, right: 8, top: 16, bottom: 50 } : { left: 80, right: 20, top: 20, bottom: 50 },
    xAxis: o.horizontal ? valueAxis : periodAxis,
    yAxis: o.horizontal ? { ...periodAxis, inverse: true } : valueAxis,
    series: o.series.map((s) => ({ type: 'bar', stack: o.stack, name: s.name, data: s.data, itemStyle: { color: s.color } })),
  }
}
