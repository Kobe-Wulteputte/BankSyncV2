<script setup lang="ts">
import Skeleton from 'primevue/skeleton'
import { computed, onMounted, watch } from 'vue'
import { getExpenses, getIncome } from '@/api/analytics'
import { VChart, type EChartsOption } from '@/charts'
import FilterBar from '@/components/FilterBar.vue'
import StatTile from '@/components/StatTile.vue'
import { useAsync, useDark } from '@/composables'
import { useFilterStore } from '@/stores/filter'
import { useLookupStore } from '@/stores/lookups'
import { groupByClass, OTHER_THRESHOLD } from '@/utils/cashflow'
import { categoryColor, formatMoney, formatPercent } from '@/utils/format'

const filter = useFilterStore()
const lookups = useLookupStore()
const dark = useDark()

const { data, loading, run } = useAsync(async () => {
  const [income, expenses] = await Promise.all([getIncome(filter.apiFilter), getExpenses(filter.apiFilter)])
  return { income, expenses }
})
onMounted(run)
watch(() => filter.apiFilter, run, { deep: true })

interface Bucket { key: string; name: string; categoryId: number | null; total: number }

// Categories under this share of their side are folded into one "Other" node so the diagram stays readable.
const fold = (buckets: Bucket[], side: string): Bucket[] => {
  const total = buckets.reduce((s, b) => s + b.total, 0)
  const big = buckets.filter((b) => b.total >= total * OTHER_THRESHOLD)
  const small = buckets.filter((b) => b.total < total * OTHER_THRESHOLD && b.total > 0)
  if (small.length <= 1) return buckets.filter((b) => b.total > 0).sort((a, b) => b.total - a.total)
  const other: Bucket = {
    key: `other-${side}`,
    name: `Other ${side} (${small.length})`,
    categoryId: null,
    total: small.reduce((s, b) => s + b.total, 0),
  }
  return [...big, other].sort((a, b) => b.total - a.total)
}

const incomeBuckets = computed<Bucket[]>(() => {
  const byKey = new Map<string, Bucket>()
  for (const m of data.value?.income.months ?? []) {
    for (const c of m.byCategory) {
      const b = byKey.get(c.code) ?? { key: c.code, name: c.name, categoryId: c.categoryId, total: 0 }
      b.total += c.total
      byKey.set(c.code, b)
    }
  }
  return fold([...byKey.values()], 'income')
})

// Income names carry a trailing space, so reserve those (and the fixed nodes) before naming expense nodes.
const expenseClasses = computed(() =>
  groupByClass(data.value?.expenses.categories ?? [], [
    'Income', 'Spent', 'Savings', 'Deficit', ...incomeBuckets.value.map((b) => `${b.name} `),
  ]),
)

const totalIncome = computed(() => data.value?.income.total ?? 0)
const totalExpenses = computed(() => data.value?.expenses.total ?? 0)
const savings = computed(() => totalIncome.value - totalExpenses.value)
const rate = computed(() => (totalIncome.value > 0 ? savings.value / totalIncome.value : 0))
const hasData = computed(() => incomeBuckets.value.length + expenseClasses.value.length > 0)

const colorOf = (b: Bucket, fallback: string) =>
  b.categoryId === null && b.key.startsWith('other-')
    ? fallback
    : categoryColor({ id: b.categoryId, color: b.categoryId === null ? undefined : lookups.categoryById.get(b.categoryId)?.color })

const option = computed<EChartsOption>(() => {
  const nodes: { name: string; itemStyle?: { color: string }; depth?: number }[] = []
  const links: { source: string; target: string; value: number }[] = []
  // Five columns: income categories -> Income (+ Deficit beside it) -> Spent / Savings -> classes -> expense categories.
  // Income shows what came in; Spent shows what went out, fed by Income and, when short, by Deficit.
  const INCOME = 'Income'
  const SPENT = 'Spent'

  for (const b of incomeBuckets.value) {
    nodes.push({ name: `${b.name} `, itemStyle: { color: colorOf(b, '#9aa0a6') }, depth: 0 }) // trailing space: income side name must differ from an expense category with the same label
    links.push({ source: `${b.name} `, target: INCOME, value: b.total })
  }
  nodes.push({ name: INCOME, itemStyle: { color: '#59a14f' }, depth: 1 })
  nodes.push({ name: SPENT, itemStyle: { color: '#e15759' }, depth: 2 })

  if (savings.value >= 0) {
    if (totalExpenses.value > 0) links.push({ source: INCOME, target: SPENT, value: totalExpenses.value })
    if (savings.value > 0) {
      nodes.push({ name: 'Savings', itemStyle: { color: '#4e79a7' }, depth: 2 })
      links.push({ source: INCOME, target: 'Savings', value: savings.value })
    }
  } else {
    if (totalIncome.value > 0) links.push({ source: INCOME, target: SPENT, value: totalIncome.value })
    nodes.push({ name: 'Deficit', itemStyle: { color: '#b07aa1' }, depth: 1 })
    links.push({ source: 'Deficit', target: SPENT, value: -savings.value })
  }

  for (const g of expenseClasses.value) {
    nodes.push({ name: g.node, itemStyle: { color: lookups.classColor(g.classId) }, depth: 3 })
    links.push({ source: SPENT, target: g.node, value: g.total })
    for (const b of g.children) {
      nodes.push({ name: b.node, itemStyle: { color: colorOf(b, '#9aa0a6') }, depth: 4 })
      links.push({ source: g.node, target: b.node, value: b.total })
    }
  }

  return {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'item',
      formatter: (p) => {
        const param = p as { dataType?: string; name?: string; value?: number; data?: { source?: string; target?: string } }
        if (param.dataType === 'edge') {
          const share = totalIncome.value > 0 ? ` · ${formatPercent((param.value ?? 0) / totalIncome.value)} of income` : ''
          return `${param.data?.source?.trim()} → ${param.data?.target}<br/><b>${formatMoney(param.value ?? 0)}</b>${share}`
        }
        return `${param.name?.trim()}<br/><b>${formatMoney(param.value ?? 0)}</b>`
      },
    },
    series: [{
      type: 'sankey',
      layout: 'none',
      nodeAlign: 'justify',
      nodeGap: 10,
      nodeWidth: 18,
      left: 10, right: 130, top: 10, bottom: 10,
      emphasis: { focus: 'adjacency' },
      lineStyle: { color: 'gradient', curveness: 0.5, opacity: 0.35 },
      label: { formatter: (p) => `${String((p as { name: string }).name).trim()}  ${formatMoney(Number((p as { value?: number }).value ?? 0))}` },
      data: nodes,
      links,
    }],
  }
})
</script>

<template>
  <div class="page">
    <h1 class="page-title">Cash flow</h1>
    <FilterBar />

    <div class="tiles mb-3">
      <StatTile label="Income" :value="formatMoney(totalIncome)" :loading="loading" tone="pos" />
      <StatTile label="Expenses" :value="formatMoney(totalExpenses)" :loading="loading" tone="neg" />
      <StatTile label="Savings" :value="formatMoney(savings)" :loading="loading" :tone="savings < 0 ? 'neg' : 'pos'" />
      <StatTile label="Savings rate" :value="formatPercent(rate)" :loading="loading" />
    </div>

    <div class="card chart-card">
      <Skeleton v-if="loading" class="chart" />
      <div v-else-if="!hasData" class="empty">No transactions in this range</div>
      <VChart v-else class="chart" :option="option" :theme="dark ? 'dark' : undefined" autoresize />
    </div>
  </div>
</template>

<style scoped>
.page { display: flex; flex-direction: column; height: 100%; min-height: 0; }
.chart-card { flex: 1; min-height: 24rem; display: flex; flex-direction: column; }
.chart-card .chart { flex: 1; height: auto; min-height: 20rem; }
</style>
