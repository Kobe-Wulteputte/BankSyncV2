<script setup lang="ts">
import Column from 'primevue/column'
import ColumnGroup from 'primevue/columngroup'
import DataTable from 'primevue/datatable'
import Row from 'primevue/row'
import SelectButton from 'primevue/selectbutton'
import Skeleton from 'primevue/skeleton'
import { computed, onMounted, ref, watch } from 'vue'
import { getIncome, getMonthlyExpenses } from '@/api/analytics'
import { VChart } from '@/charts'
import FilterBar from '@/components/FilterBar.vue'
import StatTile from '@/components/StatTile.vue'
import { GROUP_BY_OPTIONS, useAsync, useDark, useGroupBy, useMobile, type GroupBy } from '@/composables'
import { useFilterStore } from '@/stores/filter'
import { useLookupStore } from '@/stores/lookups'
import { formatMoney, formatMonth } from '@/utils/format'
import { buildSeries, stackedBarOption } from '@/utils/stackedBars'

/** One page for both directions: the income bars and the monthly expense bars share everything but data and labels. */
const props = defineProps<{ kind: 'income' | 'expenses' }>()
const income = props.kind === 'income'
const label = income ? 'income' : 'expenses'

const filter = useFilterStore()
const lookups = useLookupStore()
const dark = useDark()
const mobile = useMobile()
// Income is always split by category; only the expenses page offers the class toggle.
const groupBy = income ? ref<GroupBy>('category') : useGroupBy(`bs2.${props.kind}.monthly.groupBy`)

const { data, loading, run } = useAsync(() => (income ? getIncome : getMonthlyExpenses)(filter.apiFilter))
onMounted(run)
watch(() => filter.apiFilter, run, { deep: true })

const months = computed(() => data.value?.months ?? [])
const monthCount = computed(() => Math.max(1, months.value.length))

const series = computed(() => buildSeries(months.value, groupBy.value, lookups.categoryById, lookups.classById))
// Each series summed over the whole range, biggest first like the stack (shown on the income page only).
const totals = computed(() => series.value.map((s) => ({ name: s.name, color: s.color, total: s.data.reduce((a, b) => a + b, 0) })))

const option = computed(() =>
  stackedBarOption({ labels: months.value.map((m) => formatMonth(m.month)), series: series.value, stack: label, mobile: mobile.value }),
)
</script>

<template>
  <div class="page">
  <h1 class="page-title">{{ income ? 'Income' : 'Monthly expenses' }}</h1>
  <FilterBar />

  <div class="tiles mb-3">
    <StatTile :label="`Total ${label}`" :value="formatMoney(data?.total ?? 0)" :loading="loading" :tone="income ? 'pos' : 'neg'" />
    <StatTile label="Average per month" :value="formatMoney((data?.total ?? 0) / monthCount)"
      :sub="`over ${monthCount} month${monthCount === 1 ? '' : 's'}`" :loading="loading" />
  </div>

  <SelectButton v-if="!income" v-model="groupBy" :options="GROUP_BY_OPTIONS" option-label="label" option-value="value" :allow-empty="false"
    size="small" class="mb-3" />

  <div class="card chart-card mb-3">
    <Skeleton v-if="loading" class="chart" />
    <div v-else-if="!series.length" class="empty">No {{ label }} in this range</div>
    <VChart v-else class="chart" :option="option" :theme="dark ? 'dark' : undefined" autoresize />
  </div>

  <!-- Income only: the expenses page already lists these totals. -->
  <template v-if="income">
  <div v-if="mobile" class="card list">
    <div v-if="!totals.length" class="empty">{{ loading ? 'Loading…' : `No ${label} in this range` }}</div>
    <template v-else>
      <div v-for="t in totals" :key="t.name" class="list-item">
        <span class="dot" :style="{ background: t.color }" />
        <span class="grow truncate">{{ t.name }}</span>
        <span class="amount">{{ formatMoney(t.total) }}</span>
      </div>
      <div class="list-item between"><strong>Total</strong><strong class="amount">{{ formatMoney(data?.total ?? 0) }}</strong></div>
    </template>
  </div>
  <div v-else class="card" style="padding: 0">
    <DataTable :value="totals" :loading="loading" size="small" data-key="name">
      <template #empty><div class="empty">No {{ label }} in this range</div></template>
      <Column :header="groupBy === 'class' ? 'Class' : 'Category'">
        <template #body="{ data: t }"><span class="dot" :style="{ background: t.color }" />{{ t.name }}</template>
      </Column>
      <Column header="Total" body-class="right" header-class="right">
        <template #body="{ data: t }">{{ formatMoney(t.total) }}</template>
      </Column>
      <ColumnGroup type="footer">
        <Row>
          <Column footer="Total" />
          <Column :footer="formatMoney(data?.total ?? 0)" footer-class="right" />
        </Row>
      </ColumnGroup>
    </DataTable>
  </div>
  </template>
  </div>
</template>

<style scoped>
/* Chart grows to fill the viewport below the tiles; the totals table sits under it and scrolls into view. */
.page { display: flex; flex-direction: column; min-height: 100%; }
/* The card's height comes from flex (not definite for its children), so the chart is absolutely
   positioned to fill it instead of relying on a percentage or flex height, which resolves to 0. */
.chart-card { flex: 1; min-height: 24rem; position: relative; }
.chart-card .chart { position: absolute; inset: 1rem; height: auto; width: auto; }
.chart-card .empty { position: absolute; inset: 1rem; display: flex; align-items: center; justify-content: center; }
@media (max-width: 800px) {
  .chart-card { flex: none; min-height: 22rem; }
  .chart-card .chart { inset: .5rem; }
}
</style>
