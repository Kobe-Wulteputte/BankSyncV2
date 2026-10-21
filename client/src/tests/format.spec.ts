import { describe, expect, it } from 'vitest'
import { categoryColor, formatDate, formatMoney, formatMonth, formatPercent, PALETTE, toIsoDate } from '@/utils/format'

describe('format', () => {
  it('formats money in nl-BE', () => {
    expect(formatMoney(1234.5).replace(/ /g, ' ')).toBe('€ 1.234,50')
    expect(formatMoney(-12).replace(/ /g, ' ')).toBe('€ -12,00')
    expect(formatMoney(5, 'USD')).toContain('5,00')
  })

  it('formats dates and months', () => {
    expect(formatDate('2026-03-14')).toBe('14/03/2026')
    expect(formatMonth('2026-01')).toBe('jan 2026')
    expect(toIsoDate(new Date(2026, 0, 5))).toBe('2026-01-05')
  })

  it('formats percentages', () => {
    expect(formatPercent(0.1234)).toBe('12,3%')
  })

  it('picks category colours deterministically', () => {
    expect(categoryColor({ id: 3, color: '#123456' })).toBe('#123456')
    expect(categoryColor({ id: 3, color: '123456' })).toBe('#123456')
    expect(categoryColor({ id: 3 })).toBe(categoryColor({ id: 3 }))
    expect(categoryColor({ id: 3 })).toBe(PALETTE[3])
    expect(categoryColor({ id: null })).not.toBe(categoryColor({ id: 0 }))
    expect(new Set(PALETTE).size).toBe(PALETTE.length)
  })
})
