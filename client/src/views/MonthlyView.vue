<script setup lang="ts">
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import SelectButton from 'primevue/selectbutton'
import Skeleton from 'primevue/skeleton'
import { computed, onMounted, watch } from 'vue'
import { getIncome } from '@/api/analytics'
import { VChart, type EChartsOption } from '@/charts'
import FilterBar from '@/components/FilterBar.vue'
import StatTile from '@/components/StatTile.vue'
import { GROUP_BY_OPTIONS, useAsync, useDark, useGroupBy } from '@/composables'
import { useFilterStore } from '@/stores/filter'
import { useLookupStore } from '@/stores/lookups'
import { distinctColors, formatMoney, formatMonth, UNCATEGORIZED_COLOR } from '@/utils/format'

const filter = useFilterStore()
const lookups = useLookupStore()
const dark = useDark()
const groupBy = useGroupBy('bs2.income.groupBy')

const { data, loading, run } = useAsync(() => getIncome(filter.apiFilter))
onMounted(run)
watch(() => filter.apiFilter, run, { deep: true })

const months = computed(() => data.value?.months ?? [])
const monthCount = computed(() => Math.max(1, months.value.length))

// One stacked series per category (or per class), zero-filled per month.
const series = computed(() => {
  const byKey = new Map<string, { name: string; explicit: string | null; data: number[] }>()
  const add = (key: string, name: string, explicit: string | null, i: number, total: number) => {
    let s = byKey.get(key)
    if (!s) {
      s = { name, explicit, data: new Array<number>(months.value.length).fill(0) }
      byKey.set(key, s)
    }
    s.data[i] = total
  }
  months.value.forEach((m, i) => {
    if (groupBy.value === 'class') {
      for (const c of m.byClass) {
        const explicit = c.categoryClassId === null ? UNCATEGORIZED_COLOR : (lookups.classById.get(c.categoryClassId)?.color ?? null)
        add(String(c.categoryClassId), c.name, explicit, i, c.total)
      }
    } else {
      for (const c of m.byCategory) {
        const explicit = c.categoryId === null ? UNCATEGORIZED_COLOR : (lookups.categoryById.get(c.categoryId)?.color ?? null)
        add(c.code, c.name, explicit, i, c.total)
      }
    }
  })
  // Biggest series first, then colours by rank: a category with a colour set in Settings keeps it,
  // everything else gets the most distinct remaining hue.
  const sorted = [...byKey.values()].sort((a, b) => b.data.reduce((x, y) => x + y, 0) - a.data.reduce((x, y) => x + y, 0))
  const colors = distinctColors(sorted, (s) => s.explicit)
  return sorted.map((s, i) => ({ name: s.name, color: colors[i]!, data: s.data }))
})
const breakdown = (m: { byCategory: { name: string; total: number }[]; byClass: { name: string; total: number }[] }) =>
  (groupBy.value === 'class' ? m.byClass : m.byCategory).map((c) => `${c.name} ${formatMoney(c.total)}`).join(' · ')

const option = computed<EChartsOption>(() => ({
  backgroundColor: 'transparent',
  tooltip: {
    trigger: 'axis',
    // Stacked bars report every series per month; hide the zero ones and add the month total.
    formatter: (params) => {
      const items = (Array.isArray(params) ? params : [params]) as { seriesName?: string; value?: unknown; marker?: string; axisValueLabel?: string }[]
      const rows = items.filter((p) => Number(p.value) !== 0)
      if (!rows.length) return ''
      const total = rows.reduce((sum, p) => sum + Number(p.value), 0)
      const lines = rows.map((p) => `${p.marker ?? ''}${p.seriesName} <span style="float:right;margin-left:1.5rem">${formatMoney(Number(p.value))}</span>`)
      return `<b>${rows[0]?.axisValueLabel ?? ''}</b><br/>${lines.join('<br/>')}<br/><b>Total <span style="float:right;margin-left:1.5rem">${formatMoney(total)}</span></b>`
    },
  },
  legend: { type: 'scroll', bottom: 0 },
  grid: { left: 80, right: 20, top: 20, bottom: 50 },
  xAxis: { type: 'category', data: months.value.map((m) => formatMonth(m.month)) },
  yAxis: { type: 'value', axisLabel: { formatter: (v: number) => formatMoney(v) } },
  series: series.value.map((s) => ({
    type: 'bar',
    stack: 'income',
    name: s.name,
    data: s.data,
    itemStyle: { color: s.color },
  })),
}))
</script>

<template>
  <div class="page">
  <h1 class="page-title">Income</h1>
  <FilterBar />

  <div class="tiles mb-3">
    <StatTile label="Total income" :value="formatMoney(data?.total ?? 0)" :loading="loading" tone="pos" />
    <StatTile label="Average per month" :value="formatMoney((data?.total ?? 0) / monthCount)"
      :sub="`over ${monthCount} month${monthCount === 1 ? '' : 's'}`" :loading="loading" />
  </div>

  <SelectButton v-model="groupBy" :options="GROUP_BY_OPTIONS" option-label="label" option-value="value" :allow-empty="false"
    size="small" class="mb-3" />

  <div class="card chart-card mb-3">
    <Skeleton v-if="loading" class="chart" />
    <div v-else-if="!series.length" class="empty">No income in this range</div>
    <VChart v-else class="chart" :option="option" :theme="dark ? 'dark' : undefined" autoresize />
  </div>

  <div class="card" style="padding: 0">
    <DataTable :value="months" :loading="loading" size="small" data-key="month">
      <template #empty><div class="empty">No income in this range</div></template>
      <Column header="Month"><template #body="{ data: m }">{{ formatMonth(m.month) }}</template></Column>
      <Column header="Total" body-class="right" header-class="right">
        <template #body="{ data: m }">{{ formatMoney(m.total) }}</template>
      </Column>
      <Column header="Breakdown">
        <template #body="{ data: m }">
          <span class="muted small">{{ breakdown(m) }}</span>
        </template>
      </Column>
    </DataTable>
  </div>
  </div>
</template>

<style scoped>
/* Chart grows to fill the viewport below the tiles; the month table sits under it and scrolls into view. */
.page { display: flex; flex-direction: column; min-height: 100%; }
/* The card's height comes from flex (not definite for its children), so the chart is absolutely
   positioned to fill it instead of relying on a percentage or flex height, which resolves to 0. */
.chart-card { flex: 1; min-height: 24rem; position: relative; }
.chart-card .chart { position: absolute; inset: 1rem; height: auto; width: auto; }
.chart-card .empty { position: absolute; inset: 1rem; display: flex; align-items: center; justify-content: center; }
</style>
