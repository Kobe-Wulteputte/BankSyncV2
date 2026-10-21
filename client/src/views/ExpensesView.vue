<script setup lang="ts">
import Column from 'primevue/column'
import ColumnGroup from 'primevue/columngroup'
import DataTable from 'primevue/datatable'
import Row from 'primevue/row'
import SelectButton from 'primevue/selectbutton'
import Skeleton from 'primevue/skeleton'
import { computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { getExpenses, getSummary } from '@/api/analytics'
import type { ExpenseCategoryRow, ExpenseClassRow } from '@/api/types'
import { VChart, type EChartsOption } from '@/charts'
import FilterBar from '@/components/FilterBar.vue'
import StatTile from '@/components/StatTile.vue'
import { GROUP_BY_OPTIONS, useAsync, useDark, useGroupBy } from '@/composables'
import { useFilterStore } from '@/stores/filter'
import { useLookupStore } from '@/stores/lookups'
import { monthsInRange } from '@/utils/dateRange'
import { categoryColor, classColor, formatMoney, formatPercent } from '@/utils/format'

const filter = useFilterStore()
const lookups = useLookupStore()
const router = useRouter()
const groupBy = useGroupBy('bs2.expenses.groupBy')
const dark = useDark()

const { data, loading, run } = useAsync(async () => {
  const expenses = await getExpenses(filter.apiFilter)
  // open-ended range: take the data's own span for the per-month average
  const span = filter.from && filter.to ? null : await getSummary(filter.apiFilter)
  return { expenses, span }
})
onMounted(run)
watch(() => filter.apiFilter, run, { deep: true })

const months = computed(() =>
  monthsInRange({ from: filter.from, to: filter.to }, { from: data.value?.span?.firstDate ?? null, to: data.value?.span?.lastDate ?? null }),
)
const categoryRows = computed(() => data.value?.expenses.categories ?? [])
const classRows = computed(() => data.value?.expenses.classes ?? [])
const byClass = computed(() => groupBy.value === 'class')
const rows = computed(() => (byClass.value ? classRows.value : categoryRows.value))

function openCategory(row: ExpenseCategoryRow) {
  router.push({ name: 'transactions', query: { ...filter.toUrlQuery(), categoryIds: String(row.categoryId ?? 0) } })
}

function openClass(row: ExpenseClassRow) {
  // classed rows: every expense category in the class; unclassed: the categories the result shows without one
  const ids =
    row.categoryClassId === null
      ? categoryRows.value.filter((c) => c.categoryClassId === null).map((c) => c.categoryId ?? 0)
      : lookups.categories.filter((c) => c.categoryClassId === row.categoryClassId && c.kind === 'Expense').map((c) => c.id)
  router.push({ name: 'transactions', query: { ...filter.toUrlQuery(), categoryIds: ids.join(',') } })
}

const classColorOf = (r: ExpenseClassRow) => classColor({ id: r.categoryClassId, color: r.color })

const categoryOption = computed<EChartsOption>(() => ({
  backgroundColor: 'transparent',
  tooltip: {
    formatter: (p) => {
      const info = p as unknown as { name: string; value: number; data: { share: number } }
      return `${info.name}<br/>${formatMoney(info.value)} · ${formatPercent(info.data.share)}`
    },
  },
  series: [
    {
      type: 'treemap',
      roam: false,
      nodeClick: false,
      breadcrumb: { show: false },
      label: { formatter: (p) => `${p.name}\n${formatMoney(p.value as number)}` },
      data: categoryRows.value.map((r) => ({
        name: r.name,
        value: r.total,
        share: r.share,
        categoryId: r.categoryId,
        itemStyle: { color: categoryColor({ id: r.categoryId, color: r.color }) },
      })),
    },
  ],
}))

// Two levels: class (parent, class colour) -> its categories (leaves, category colour).
const classOption = computed<EChartsOption>(() => ({
  backgroundColor: 'transparent',
  tooltip: categoryOption.value.tooltip,
  series: [
    {
      type: 'treemap',
      roam: false,
      nodeClick: false,
      breadcrumb: { show: false },
      label: { formatter: (p) => `${p.name}
${formatMoney(p.value as number)}` },
      // Parent labels only render inside a border band, so the class level gets a wide border in the class colour.
      upperLabel: { show: true, height: 24, color: '#fff', fontWeight: 'bold', formatter: (p) => `${p.name}  ${formatMoney(p.value as number)}` },
      levels: [
        { itemStyle: { borderWidth: 0, gapWidth: 6 } },
        { itemStyle: { borderWidth: 24, gapWidth: 2 }, upperLabel: { show: true, height: 24 } },
        { itemStyle: { borderWidth: 1, gapWidth: 1 } },
      ],
      data: classRows.value.map((c) => ({
        name: c.name,
        value: c.total,
        share: c.share,
        classId: c.categoryClassId,
        isClass: true,
        itemStyle: { color: classColorOf(c), borderColor: classColorOf(c) },
        children: categoryRows.value
          .filter((r) => r.categoryClassId === c.categoryClassId)
          .map((r) => ({
            name: r.name,
            value: r.total,
            share: r.share,
            categoryId: r.categoryId,
            itemStyle: { color: categoryColor({ id: r.categoryId, color: r.color }) },
          })),
      })),
    },
  ],
}))
const option = computed(() => (byClass.value ? classOption.value : categoryOption.value))

function onChartClick(params: { data?: unknown }) {
  const clicked = (params.data ?? {}) as { categoryId?: number | null; classId?: number | null; isClass?: boolean }
  if (clicked.isClass) {
    const row = classRows.value.find((r) => r.categoryClassId === clicked.classId)
    if (row) openClass(row)
    return
  }
  const row = categoryRows.value.find((r) => r.categoryId === clicked.categoryId)
  if (row) openCategory(row)
}

function onRowClick(row: ExpenseCategoryRow | ExpenseClassRow) {
  if ('code' in row) openCategory(row)
  else openClass(row)
}
</script>

<template>
  <div class="page">
  <h1 class="page-title">Expenses</h1>
  <FilterBar />

  <div class="tiles mb-3">
    <StatTile label="Total expenses" :value="formatMoney(data?.expenses.total ?? 0)" :loading="loading" tone="neg" />
    <StatTile label="Transactions" :value="String(data?.expenses.count ?? 0)" :loading="loading" />
    <StatTile label="Average per month" :value="formatMoney((data?.expenses.total ?? 0) / months)"
      :sub="`over ${months} month${months === 1 ? '' : 's'}`" :loading="loading" />
  </div>

  <SelectButton v-model="groupBy" :options="GROUP_BY_OPTIONS" option-label="label" option-value="value" :allow-empty="false"
    size="small" class="mb-3" style="align-self: flex-start" />

  <div class="grid">
    <div class="card" style="padding: 0">
      <DataTable :key="groupBy" :value="rows" :loading="loading" size="small" :data-key="byClass ? 'name' : 'code'" row-hover scrollable
        scroll-height="flex" selection-mode="single" @row-click="onRowClick($event.data)">
        <template #empty><div class="empty">No expenses in this range</div></template>
        <Column :header="byClass ? 'Class' : 'Category'" sortable field="name">
          <template #body="{ data: r }">
            <span class="dot" :style="{ background: byClass ? classColorOf(r) : categoryColor({ id: r.categoryId, color: r.color }) }" />{{ r.name }}
          </template>
        </Column>
        <Column header="Total" sortable field="total" body-class="right" header-class="right">
          <template #body="{ data: r }">{{ formatMoney(r.total) }}</template>
        </Column>
        <Column header="Count" sortable field="count" body-class="right" />
        <Column header="Share" sortable field="share" body-class="right">
          <template #body="{ data: r }">{{ formatPercent(r.share) }}</template>
        </Column>
        <ColumnGroup type="footer">
          <Row>
            <Column footer="Total" />
            <Column :footer="formatMoney(data?.expenses.total ?? 0)" footer-class="right" />
            <Column :footer="String(data?.expenses.count ?? 0)" footer-class="right" />
            <Column footer="100%" footer-class="right" />
          </Row>
        </ColumnGroup>
      </DataTable>
    </div>
    <div class="card chart-card">
      <Skeleton v-if="loading" class="chart" />
      <div v-else-if="!rows.length" class="empty">Nothing to chart</div>
      <VChart v-else class="chart" :option="option" :update-options="{ notMerge: true }" :theme="dark ? 'dark' : undefined" autoresize @click="onChartClick" />
    </div>
  </div>
  </div>
</template>

<style scoped>
/* The page fills the scroll area so the treemap can take all remaining height; the table scrolls inside its card. */
.page { display: flex; flex-direction: column; height: 100%; min-height: 0; }
.grid { display: grid; grid-template-columns: minmax(20rem, 1fr) minmax(20rem, 1.3fr); gap: 1rem; flex: 1; min-height: 24rem; }
.grid > .card { min-height: 0; display: flex; flex-direction: column; }
.chart-card .chart { flex: 1; height: auto; min-height: 20rem; }
@media (max-width: 1000px) {
  .page { height: auto; }
  .grid { grid-template-columns: 1fr; }
  .chart-card .chart { height: 24rem; }
}
:deep(tr) { cursor: pointer; }
</style>
