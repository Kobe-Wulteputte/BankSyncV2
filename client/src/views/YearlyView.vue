<script setup lang="ts">
import SelectButton from 'primevue/selectbutton'
import Skeleton from 'primevue/skeleton'
import { computed, onMounted, watch } from 'vue'
import { getIncome, getMonthlyExpenses } from '@/api/analytics'
import { VChart } from '@/charts'
import FilterBar from '@/components/FilterBar.vue'
import StatTile from '@/components/StatTile.vue'
import { GROUP_BY_OPTIONS, useAsync, useDark, useGroupBy, useMobile } from '@/composables'
import { useFilterStore } from '@/stores/filter'
import { useLookupStore } from '@/stores/lookups'
import { formatMoney } from '@/utils/format'
import { buildSeries, stackedBarOption, toYears } from '@/utils/stackedBars'

/** Expenses and income per calendar year, summed client-side from the monthly analytics. */
const filter = useFilterStore()
const lookups = useLookupStore()
const dark = useDark()
const mobile = useMobile()
const groupBy = useGroupBy('bs2.yearly.groupBy')

const { data, loading, run } = useAsync(async () => {
  const [expenses, income] = await Promise.all([getMonthlyExpenses(filter.apiFilter), getIncome(filter.apiFilter)])
  return { expenses, income }
})
onMounted(run)
watch(() => filter.apiFilter, run, { deep: true })

// Both charts get the same years, so a year sits at the same spot in each.
const years = computed(() =>
  [...new Set([...(data.value?.expenses.months ?? []), ...(data.value?.income.months ?? [])].map((m) => m.month.slice(0, 4)))].sort(),
)

const charts = computed(() =>
  (['expenses', 'income'] as const).map((kind) => {
    const series = buildSeries(toYears(data.value?.[kind].months ?? [], years.value), groupBy.value, lookups.categoryById, lookups.classById)
    return {
      kind,
      title: kind === 'income' ? 'Income' : 'Expenses',
      empty: !series.length,
      option: stackedBarOption({ labels: years.value, series, stack: kind, mobile: mobile.value }),
    }
  }),
)
</script>

<template>
  <div class="page">
    <h1 class="page-title">Yearly</h1>
    <FilterBar />

    <div class="tiles mb-3">
      <StatTile label="Total expenses" :value="formatMoney(data?.expenses.total ?? 0)" :loading="loading" tone="neg" />
      <StatTile label="Total income" :value="formatMoney(data?.income.total ?? 0)" :loading="loading" tone="pos" />
    </div>

    <SelectButton v-model="groupBy" :options="GROUP_BY_OPTIONS" option-label="label" option-value="value" :allow-empty="false"
      size="small" class="mb-3" style="align-self: flex-start" />

    <div class="charts">
      <section v-for="c in charts" :key="c.kind" class="card col gap-2">
        <h2 class="chart-title">{{ c.title }}</h2>
        <Skeleton v-if="loading" class="chart" />
        <div v-else-if="c.empty" class="empty">No {{ c.kind }} in this range</div>
        <VChart v-else class="chart" :option="c.option" :update-options="{ notMerge: true }" :theme="dark ? 'dark' : undefined" autoresize />
      </section>
    </div>
  </div>
</template>

<style scoped>
.page { display: flex; flex-direction: column; }
.charts { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(28rem, 100%), 1fr)); gap: 1rem; }
.chart-title { margin: 0; font-size: 1.1rem; }
</style>
