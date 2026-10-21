import { onUnmounted, ref, watch, type Ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { hasFilterQuery, useFilterStore } from '@/stores/filter'

/** Follows the OS colour scheme; the ECharts theme flips with it. */
export function useDark(): Ref<boolean> {
  const mq = window.matchMedia('(prefers-color-scheme: dark)')
  const dark = ref(mq.matches)
  const handler = (e: MediaQueryListEvent) => (dark.value = e.matches)
  mq.addEventListener('change', handler)
  onUnmounted(() => mq.removeEventListener('change', handler))
  return dark
}

/**
 * Two-way binding between the filter store and the URL query.
 * URL wins on arrival (shareable links); afterwards the store writes back and persists.
 */
export function useFilterUrlSync(): void {
  const store = useFilterStore()
  const route = useRoute()
  const router = useRouter()

  const applyRoute = () => {
    if (hasFilterQuery(route.query)) store.fromUrlQuery(route.query)
  }
  applyRoute()

  watch(
    () => store.$state,
    () => {
      store.persist()
      const next = { ...route.query, ...store.toUrlQuery() }
      // drop filter keys that are no longer set
      for (const k of ['from', 'to', 'excludeCategoryIds', 'accountIds', 'excludeGroupIds', 'search']) {
        if (!(k in store.toUrlQuery())) delete next[k]
      }
      if (JSON.stringify(next) !== JSON.stringify(route.query)) router.replace({ query: next })
    },
    { deep: true, immediate: true },
  )
  watch(() => route.query, applyRoute)
}

/** Tiny async-state helper so every view shows the same loading / error / empty states. */
export function useAsync<T>(fn: () => Promise<T>) {
  const data = ref<T | null>(null) as Ref<T | null>
  const loading = ref(false)
  const error = ref<string | null>(null)
  let seq = 0
  async function run() {
    const my = ++seq
    loading.value = true
    error.value = null
    try {
      const result = await fn()
      if (my === seq) data.value = result
    } catch (e) {
      if (my === seq) error.value = e instanceof Error ? e.message : String(e)
    } finally {
      if (my === seq) loading.value = false
    }
  }
  return { data, loading, error, run }
}

export type GroupBy = 'category' | 'class'
export const GROUP_BY_OPTIONS = [
  { label: 'By category', value: 'category' },
  { label: 'By class', value: 'class' },
]

/** "By category | By class" toggle remembered in localStorage under `key`. */
export function useGroupBy(key: string): Ref<GroupBy> {
  let initial: GroupBy = 'category'
  try {
    if (localStorage.getItem(key) === 'class') initial = 'class'
  } catch {
    // storage unavailable
  }
  const groupBy = ref<GroupBy>(initial)
  watch(groupBy, (v) => {
    try {
      localStorage.setItem(key, v)
    } catch {
      // storage unavailable
    }
  })
  return groupBy
}
