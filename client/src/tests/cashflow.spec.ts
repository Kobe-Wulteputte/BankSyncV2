import { describe, expect, it } from 'vitest'
import { groupByClass, uniqueName, type ExpenseCat } from '@/utils/cashflow'
import { classColor, PALETTE, UNCATEGORIZED_COLOR } from '@/utils/format'

const cat = (code: string, total: number, classId: number | null, className: string | null = null): ExpenseCat => ({
  code, name: code, categoryId: 1, categoryClassId: classId, categoryClassName: className, total,
})

describe('groupByClass', () => {
  it('groups categories under their class, biggest first, and puts classless ones under Unclassed', () => {
    const r = groupByClass([cat('Rent', 600, 2, 'Housing'), cat('Beer', 100, 5, 'Fun'), cat('Misc', 300, null)])
    expect(r.map((g) => g.name)).toEqual(['Housing', 'Unclassed', 'Fun'])
    expect(r[1]!.classId).toBeNull()
    expect(r[0]!.total).toBe(600)
  })

  it('folds categories under 1% of the side total per class', () => {
    const r = groupByClass([cat('Rent', 900, 2, 'Housing'), cat('a', 2, 2, 'Housing'), cat('b', 3, 2, 'Housing'), cat('c', 4, 5, 'Fun'), cat('Big', 91, 5, 'Fun')])
    const housing = r.find((g) => g.name === 'Housing')!
    expect(housing.children.map((c) => c.name)).toEqual(['Rent', 'Other Housing (2)'])
    expect(housing.children[1]!.total).toBe(5)
    // a single small category in a class is kept, not folded
    expect(r.find((g) => g.name === 'Fun')!.children.map((c) => c.name)).toEqual(['Big', 'c'])
  })

  it('keeps node names unique when a class shares a name with a category', () => {
    const r = groupByClass([cat('Sports', 100, 7, 'Sports')], ['Income'])
    expect(r[0]!.children[0]!.node).toBe('Sports')
    expect(r[0]!.node).toBe('Sports ')
    expect(uniqueName('Income', new Set(['Income', 'Income ']))).toBe('Income  ')
  })

  it('drops zero-total categories', () => {
    expect(groupByClass([cat('Zero', 0, 2, 'Housing')])).toEqual([])
  })
})

describe('classColor', () => {
  it('uses the class colour, a deterministic palette entry, or grey', () => {
    expect(classColor({ id: 3, color: '#123456' })).toBe('#123456')
    expect(classColor({ id: 3 })).toBe(classColor({ id: 3, color: null }))
    expect(PALETTE).toContain(classColor({ id: 3 }) as (typeof PALETTE)[number])
    expect(classColor({ id: null })).toBe(UNCATEGORIZED_COLOR)
  })
})
