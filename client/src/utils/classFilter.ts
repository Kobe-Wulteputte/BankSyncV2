import type { CategoryDto } from '@/api/types'

/** Stands in for "no class" in the class filter. */
export const UNCLASSED = 0

const classOf = (c: CategoryDto) => c.categoryClassId ?? UNCLASSED

/** A class counts as selected while any of its categories is. */
export function selectedClasses(categories: CategoryDto[], includedCategoryIds: number[]): number[] {
  const included = new Set(includedCategoryIds)
  return [...new Set(categories.filter((c) => included.has(c.id)).map(classOf))]
}

/** Applies a class selection change to the category selection: a toggled class (de)selects all its categories. */
export function applyClassSelection(categories: CategoryDto[], includedCategoryIds: number[], classIds: number[]): number[] {
  const before = new Set(selectedClasses(categories, includedCategoryIds))
  const after = new Set(classIds)
  const included = new Set(includedCategoryIds)
  for (const c of categories) {
    const cls = classOf(c)
    if (after.has(cls) && !before.has(cls)) included.add(c.id)
    if (!after.has(cls) && before.has(cls)) included.delete(c.id)
  }
  return [...included]
}
