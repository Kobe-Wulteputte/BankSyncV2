export interface ExpenseCat {
  code: string
  name: string
  categoryId: number | null
  categoryClassId: number | null
  categoryClassName: string | null
  total: number
}
export interface CatNode {
  key: string
  /** Unique sankey node name. */
  node: string
  name: string
  categoryId: number | null
  total: number
  isOther: boolean
}
export interface ClassNode {
  classId: number | null
  /** Unique sankey node name. */
  node: string
  name: string
  total: number
  children: CatNode[]
}

export const UNCLASSED = 'Unclassed'
export const OTHER_THRESHOLD = 0.01

/** Appends trailing spaces until `name` is not in `taken`, then reserves it. */
export function uniqueName(name: string, taken: Set<string>): string {
  let n = name
  while (taken.has(n)) n += ' '
  taken.add(n)
  return n
}

/**
 * Groups expense categories by class (biggest first). Inside a class, categories under `threshold` of the
 * side total fold into one "Other <class> (n)" child when there is more than one. `reserved` = node names already used.
 */
export function groupByClass(cats: ExpenseCat[], reserved: string[] = [], threshold = OTHER_THRESHOLD): ClassNode[] {
  const taken = new Set(reserved)
  const sideTotal = cats.reduce((s, c) => s + c.total, 0)
  const byClass = new Map<number | null, { name: string; cats: ExpenseCat[] }>()
  for (const c of cats) {
    if (c.total <= 0) continue
    const g = byClass.get(c.categoryClassId) ?? { name: c.categoryClassId === null ? UNCLASSED : (c.categoryClassName ?? UNCLASSED), cats: [] }
    g.cats.push(c)
    byClass.set(c.categoryClassId, g)
  }

  const groups = [...byClass.entries()].map(([classId, g]) => {
    const small = g.cats.filter((c) => c.total < sideTotal * threshold)
    const fold = small.length > 1
    const children: CatNode[] = g.cats
      .filter((c) => !fold || !small.includes(c))
      .map((c) => ({ key: c.code, node: '', name: c.name, categoryId: c.categoryId, total: c.total, isOther: false }))
    if (fold) {
      children.push({
        key: `other-${classId ?? 'none'}`,
        node: '',
        name: `Other ${g.name} (${small.length})`,
        categoryId: null,
        total: small.reduce((s, c) => s + c.total, 0),
        isOther: true,
      })
    }
    children.sort((a, b) => b.total - a.total)
    return { classId, name: g.name, children, total: children.reduce((s, c) => s + c.total, 0) }
  })
  groups.sort((a, b) => b.total - a.total)

  // Categories claim their names first so a same-named class gets the trailing space, not the category.
  for (const g of groups) for (const c of g.children) c.node = uniqueName(c.name, taken)
  return groups.map((g) => ({ ...g, node: uniqueName(g.name, taken) }))
}
