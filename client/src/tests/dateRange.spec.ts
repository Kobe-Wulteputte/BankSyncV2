import { describe, expect, it } from 'vitest'
import { detectPreset, monthsInRange, presetRange } from '@/utils/dateRange'

const now = new Date(2026, 2, 14) // 14 March 2026

describe('presetRange', () => {
  it('this / last month', () => {
    expect(presetRange('thisMonth', now)).toEqual({ from: '2026-03-01', to: '2026-03-31' })
    expect(presetRange('lastMonth', now)).toEqual({ from: '2026-02-01', to: '2026-02-28' })
  })
  it('this / last year', () => {
    expect(presetRange('thisYear', now)).toEqual({ from: '2026-01-01', to: '2026-12-31' })
    expect(presetRange('lastYear', now)).toEqual({ from: '2025-01-01', to: '2025-12-31' })
  })
  it('last 12 months spans 12 whole months ending this month', () => {
    expect(presetRange('last12Months', now)).toEqual({ from: '2025-04-01', to: '2026-03-31' })
    expect(presetRange('last3Months', now)).toEqual({ from: '2026-01-01', to: '2026-03-31' })
    expect(presetRange('last6Months', now)).toEqual({ from: '2025-10-01', to: '2026-03-31' })
  })
  it('crosses the year boundary for last month in January', () => {
    expect(presetRange('lastMonth', new Date(2026, 0, 10))).toEqual({ from: '2025-12-01', to: '2025-12-31' })
  })
  it('all time / custom are open', () => {
    expect(presetRange('allTime', now)).toEqual({ from: null, to: null })
    expect(presetRange('custom', now)).toEqual({ from: null, to: null })
  })
})

describe('detectPreset', () => {
  it('round-trips every closed preset', () => {
    for (const p of ['thisMonth', 'lastMonth', 'thisYear', 'lastYear', 'last3Months', 'last6Months', 'last12Months'] as const) {
      expect(detectPreset(presetRange(p, now), now)).toBe(p)
    }
  })
  it('falls back to custom / allTime', () => {
    expect(detectPreset({ from: '2026-03-02', to: '2026-03-31' }, now)).toBe('custom')
    expect(detectPreset({ from: null, to: null }, now)).toBe('allTime')
  })
})

describe('monthsInRange', () => {
  it('counts touched calendar months', () => {
    expect(monthsInRange({ from: '2026-01-01', to: '2026-03-31' }, { from: null, to: null })).toBe(3)
    expect(monthsInRange({ from: '2025-11-15', to: '2026-01-02' }, { from: null, to: null })).toBe(3)
    expect(monthsInRange({ from: null, to: null }, { from: '2024-01-01', to: '2024-12-31' })).toBe(12)
    expect(monthsInRange({ from: null, to: null }, { from: null, to: null })).toBe(1)
  })
})
