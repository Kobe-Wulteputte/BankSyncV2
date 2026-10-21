<script setup lang="ts">
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Skeleton from 'primevue/skeleton'
import { computed, onMounted, watch } from 'vue'
import { getSavings } from '@/api/analytics'
import { VChart, type EChartsOption } from '@/charts'
import FilterBar from '@/components/FilterBar.vue'
import StatTile from '@/components/StatTile.vue'
import { useAsync, useDark } from '@/composables'
import { useFilterStore } from '@/stores/filter'
import { formatMoney, formatMonth, formatPercent } from '@/utils/format'

const filter = useFilterStore()
const dark = useDark()

const { data, loading, run } = useAsync(() => getSavings(filter.apiFilter))
onMounted(run)
watch(() => filter.apiFilter, run, { deep: true })

const months = computed(() => data.value?.months ?? [])
const rate = computed(() => (data.value && data.value.totalIncome > 0 ? data.value.totalSavings / data.value.totalIncome : 0))

const option = computed<EChartsOption>(() => ({
  backgroundColor: 'transparent',
  tooltip: { trigger: 'axis', valueFormatter: (v) => formatMoney(Number(v)) },
  legend: { bottom: 0 },
  grid: { left: 80, right: 20, top: 20, bottom: 50 },
  xAxis: { type: 'category', data: months.value.map((m) => formatMonth(m.month)) },
  yAxis: { type: 'value', axisLabel: { formatter: (v: number) => formatMoney(v) } },
  series: [
    { type: 'bar', name: 'Income', stack: 'flow', data: months.value.map((m) => m.income), itemStyle: { color: '#59a14f' } },
    { type: 'bar', name: 'Expenses', stack: 'flow', data: months.value.map((m) => -m.expenses), itemStyle: { color: '#e15759' } },
    { type: 'line', name: 'Savings', data: months.value.map((m) => m.savings), itemStyle: { color: '#4e79a7' }, smooth: true },
    { type: 'line', name: 'Cumulative', data: months.value.map((m) => m.cumulative), itemStyle: { color: '#f28e2b' }, lineStyle: { type: 'dashed' } },
  ],
}))

const tone = (v: number) => (v < 0 ? 'neg' : 'pos')
</script>

<template>
  <div class="page">
  <h1 class="page-title">Savings</h1>
  <FilterBar />

  <div class="tiles mb-3">
    <StatTile label="Total income" :value="formatMoney(data?.totalIncome ?? 0)" :loading="loading" tone="pos" />
    <StatTile label="Total expenses" :value="formatMoney(data?.totalExpenses ?? 0)" :loading="loading" tone="neg" />
    <StatTile label="Net savings" :value="formatMoney(data?.totalSavings ?? 0)" :loading="loading" :tone="tone(data?.totalSavings ?? 0)" />
    <StatTile label="Savings rate" :value="formatPercent(rate)" :loading="loading" :tone="tone(rate)" />
  </div>

  <div class="card chart-card mb-3">
    <Skeleton v-if="loading" class="chart" />
    <div v-else-if="!months.length" class="empty">No data in this range</div>
    <VChart v-else class="chart" :option="option" :theme="dark ? 'dark' : undefined" autoresize />
  </div>

  <div class="card" style="padding: 0">
    <DataTable :value="months" :loading="loading" size="small" data-key="month">
      <template #empty><div class="empty">No data in this range</div></template>
      <Column header="Month"><template #body="{ data: m }">{{ formatMonth(m.month) }}</template></Column>
      <Column header="Income" body-class="right" header-class="right"><template #body="{ data: m }">{{ formatMoney(m.income) }}</template></Column>
      <Column header="Expenses" body-class="right" header-class="right"><template #body="{ data: m }">{{ formatMoney(m.expenses) }}</template></Column>
      <Column header="Savings" body-class="right" header-class="right">
        <template #body="{ data: m }"><span :class="tone(m.savings)">{{ formatMoney(m.savings) }}</span></template>
      </Column>
      <Column header="Cumulative" body-class="right" header-class="right">
        <template #body="{ data: m }"><span :class="tone(m.cumulative)">{{ formatMoney(m.cumulative) }}</span></template>
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
