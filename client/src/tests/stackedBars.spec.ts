import { describe, expect, it } from 'vitest'
import type { MonthlyMonth } from '@/api/types'
import { buildSeries, toYears } from '@/utils/stackedBars'

const month = (m: string, groceries: number, rent: number): MonthlyMonth => ({
  month: `${m}-01`,
  total: groceries + rent,
  byCategory: [
    { categoryId: 1, code: 'GROCERIES', name: 'Groceries', total: groceries, categoryClassId: 1 },
    { categoryId: 2, code: 'RENT', name: 'Rent', total: rent, categoryClassId: 1 },
  ],
  byClass: [{ categoryClassId: 1, name: 'Living', total: groceries + rent }],
})

describe('toYears', () => {
  it('sums months per calendar year and zero-fills missing years', () => {
    const years = toYears([month('2024-11', 10, 100), month('2024-12', 20, 100), month('2026-01', 5, 0)], ['2024', '2025', '2026'])
    expect(years.map((y) => [y.month, y.total])).toEqual([['2024', 230], ['2025', 0], ['2026', 5]])
    expect(years[0]!.byCategory.map((c) => [c.code, c.total])).toEqual([['GROCERIES', 30], ['RENT', 200]])
    expect(years[0]!.byClass).toEqual([{ categoryClassId: 1, name: 'Living', total: 230 }])
    expect(years[1]!.byCategory).toEqual([])
  })

  it('does not mutate the monthly input', () => {
    const input = [month('2024-11', 10, 100), month('2024-12', 20, 100)]
    toYears(input, ['2024'])
    expect(input[0]!.byCategory[0]!.total).toBe(10)
  })
})

describe('buildSeries', () => {
  it('builds one zero-filled series per category, biggest first', () => {
    const series = buildSeries(toYears([month('2024-05', 10, 100), month('2026-05', 1, 0)], ['2024', '2025', '2026']), 'category', new Map(), new Map())
    expect(series.map((s) => [s.name, s.data])).toEqual([['Rent', [100, 0, 0]], ['Groceries', [10, 0, 1]]])
  })
})
