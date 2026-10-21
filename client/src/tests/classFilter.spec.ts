import { describe, expect, it } from 'vitest'
import type { CategoryDto } from '@/api/types'
import { applyClassSelection, selectedClasses, UNCLASSED } from '@/utils/classFilter'

const cat = (id: number, categoryClassId: number | null): CategoryDto =>
  ({ id, code: `c${id}`, name: `C${id}`, kind: 'Expense', sortOrder: id, isActive: true, color: null, categoryClassId })

// Class 1: categories 1, 2. Class 2: category 3. No class: category 4.
const categories = [cat(1, 1), cat(2, 1), cat(3, 2), cat(4, null)]

describe('class filter', () => {
  it('marks a class selected while any of its categories is', () => {
    expect(selectedClasses(categories, [2, 4]).sort()).toEqual([UNCLASSED, 1])
  })

  it('deselecting a class removes all its categories and leaves the rest', () => {
    expect(applyClassSelection(categories, [1, 2, 3, 4], [2, UNCLASSED]).sort()).toEqual([3, 4])
  })

  it('selecting a class adds all its categories, also after a partial pick', () => {
    expect(applyClassSelection(categories, [4], [1, UNCLASSED]).sort()).toEqual([1, 2, 4])
  })

  it('leaves an unchanged partially selected class alone', () => {
    expect(applyClassSelection(categories, [1, 3], [1]).sort()).toEqual([1])
  })
})
