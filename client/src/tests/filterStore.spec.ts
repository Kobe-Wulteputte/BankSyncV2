import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'
import { useFilterStore } from '@/stores/filter'
import { presetRange } from '@/utils/dateRange'

describe('useFilterStore', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
  })

  it('defaults to this month, reimbursed excluded, nothing else', () => {
    const s = useFilterStore()
    expect(s.preset).toBe('thisMonth')
    expect({ from: s.from, to: s.to }).toEqual(presetRange('thisMonth'))
    expect(s.excludeReimbursed).toBe(true)
    expect(s.excludeCategoryIds).toEqual([])
  })

  it('round-trips through the URL query', () => {
    const s = useFilterStore()
    s.setCustomRange('2026-01-01', '2026-02-15')
    s.excludeCategoryIds = [3, 7]
    s.accountIds = ['a1', 'a2']
    s.excludeGroupIds = ['g1']
    s.excludeReimbursed = false
    s.search = 'colruyt'

    const q = s.toUrlQuery()
    expect(q).toEqual({
      from: '2026-01-01',
      to: '2026-02-15',
      excludeCategoryIds: '3,7',
      accountIds: 'a1,a2',
      excludeGroupIds: 'g1',
      excludeReimbursed: 'false',
      search: 'colruyt',
    })

    const other = useFilterStore()
    other.reset()
    other.fromUrlQuery(q as Record<string, string>)
    expect(other.$state).toEqual(s.$state)
    expect(other.preset).toBe('custom')
  })

  it('reads repeated and comma-joined array params and recognises presets', () => {
    const s = useFilterStore()
    const r = presetRange('lastMonth')
    s.fromUrlQuery({ from: r.from, to: r.to, excludeCategoryIds: ['1,2', '3'], accountIds: 'x' })
    expect(s.preset).toBe('lastMonth')
    expect(s.excludeCategoryIds).toEqual([1, 2, 3])
    expect(s.accountIds).toEqual(['x'])
    expect(s.excludeReimbursed).toBe(true) // missing => default ON
  })

  it('toQuery() produces the API query string per the contract', () => {
    const s = useFilterStore()
    s.setCustomRange('2026-01-01', null)
    s.excludeCategoryIds = [5]
    s.accountIds = ['acc']
    s.search = ' pizza '
    const params = new URLSearchParams(s.toQuery({ page: 2, pageSize: 50 }))
    expect(params.get('from')).toBe('2026-01-01')
    expect(params.has('to')).toBe(false)
    expect(params.get('excludeCategoryIds')).toBe('5')
    expect(params.get('accountIds')).toBe('acc')
    expect(params.get('excludeReimbursed')).toBe('true')
    expect(params.get('search')).toBe('pizza')
    expect(params.get('page')).toBe('2')

    s.excludeReimbursed = false
    expect(new URLSearchParams(s.toQuery()).has('excludeReimbursed')).toBe(false)
  })

  it('persists to localStorage and recomputes relative presets on load', () => {
    const s = useFilterStore()
    s.setPreset('lastYear')
    s.excludeCategoryIds = [9]
    s.persist()
    const stored = JSON.parse(localStorage.getItem('bs2.filter')!)
    expect(stored.preset).toBe('lastYear')

    setActivePinia(createPinia())
    const again = useFilterStore()
    expect(again.preset).toBe('lastYear')
    expect(again.excludeCategoryIds).toEqual([9])
    expect({ from: again.from, to: again.to }).toEqual(presetRange('lastYear'))
  })
})
