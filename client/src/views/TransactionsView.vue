<script setup lang="ts">
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Column from 'primevue/column'
import DataTable, { type DataTablePageEvent, type DataTableSortEvent } from 'primevue/datatable'
import MultiSelect from 'primevue/multiselect'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import ToggleSwitch from 'primevue/toggleswitch'
import { useToast } from 'primevue/usetoast'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { bulkUpdateTransactions, getTransaction, listTransactions, patchTransaction } from '@/api/transactions'
import type { PatchTransactionBody, TransactionDto, TransactionPage, TransactionSort } from '@/api/types'
import ClassificationHistory from '@/components/ClassificationHistory.vue'
import FilterBar from '@/components/FilterBar.vue'
import GroupDialog from '@/components/GroupDialog.vue'
import { useAsync } from '@/composables'
import { useFilterStore } from '@/stores/filter'
import { useLookupStore } from '@/stores/lookups'
import { categoryColor, formatDate, formatMoney, formatPercent } from '@/utils/format'

const filter = useFilterStore()
const lookups = useLookupStore()
const route = useRoute()
const router = useRouter()
const toast = useToast()

const page = ref(1)
const pageSize = ref(50)
const sort = ref<TransactionSort>('date')
const dir = ref<'asc' | 'desc'>('desc')
const unclassifiedOnly = ref(false)
const selection = ref<TransactionDto[]>([])
const expandedRows = ref<TransactionDto[]>([])

// category drill-down from the Expenses page lives in the URL only (not in the shared filter)
const categoryIds = computed(() =>
  String(route.query.categoryIds ?? '')
    .split(',')
    .filter(Boolean)
    .map(Number),
)
const clearCategoryIds = () => router.replace({ query: { ...route.query, categoryIds: undefined } })
const categoryLabel = (id: number) => (id === 0 ? 'Uncategorized' : (lookups.categoryById.get(id)?.name ?? `#${id}`))

const { data, loading, run } = useAsync<TransactionPage>(() =>
  listTransactions({
    ...filter.apiFilter,
    categoryIds: categoryIds.value.length ? categoryIds.value : undefined,
    unclassifiedOnly: unclassifiedOnly.value || undefined,
    page: page.value,
    pageSize: pageSize.value,
    sort: sort.value,
    dir: dir.value,
  }),
)
const items = computed(() => data.value?.items ?? [])

onMounted(run)
watch([() => filter.apiFilter, categoryIds, unclassifiedOnly], () => {
  selection.value = []
  if (page.value === 1) run()
  else page.value = 1 // the page watcher below refetches
}, { deep: true })
watch([page, pageSize, sort, dir], run)

function onPage(e: DataTablePageEvent) {
  page.value = e.page + 1
  pageSize.value = e.rows
}
function onSort(e: DataTableSortEvent) {
  sort.value = (e.sortField as TransactionSort) || 'date'
  dir.value = e.sortOrder === 1 ? 'asc' : 'desc'
}

function replaceRow(row: TransactionDto) {
  if (!data.value) return
  const i = data.value.items.findIndex((t) => t.id === row.id)
  if (i >= 0) data.value.items[i] = row
}
async function patch(id: string, body: PatchTransactionBody) {
  try {
    replaceRow(await patchTransaction(id, body))
  } catch {
    run() // revert the optimistic control state
  }
}
async function refreshRow(id: string) {
  try {
    replaceRow(await getTransaction(id))
  } catch {
    // toasted
  }
}

// bulk actions
const bulkCategory = ref<number | null>(null)
const bulkGroup = ref<string | null>(null)
const bulkBusy = ref(false)
async function bulk(body: Omit<Parameters<typeof bulkUpdateTransactions>[0], 'ids'>) {
  bulkBusy.value = true
  try {
    const { updated } = await bulkUpdateTransactions({ ids: selection.value.map((t) => t.id), ...body })
    toast.add({ severity: 'success', summary: `${updated} transaction(s) updated`, life: 3000 })
    selection.value = []
    await run()
  } catch {
    // toasted
  } finally {
    bulkBusy.value = false
  }
}

const groupDialog = ref(false)

const sourceBadge = (t: TransactionDto) =>
  t.classificationSource === 'Ai' ? { label: 'AI', severity: 'info' as const }
  : t.classificationSource === 'Manual' ? { label: 'manual', severity: 'secondary' as const }
  : t.classificationSource === 'Import' ? { label: 'import', severity: 'contrast' as const }
  : null
</script>

<template>
  <h1 class="page-title">Transactions</h1>
  <FilterBar show-search />

  <div class="card flex wrap center gap-3 mb-3">
    <label class="flex center gap-2 small nowrap"><ToggleSwitch v-model="unclassifiedOnly" /> Unclassified only</label>
    <Tag v-for="id in categoryIds" :key="id" severity="info" class="flex center gap-1">
      Category: {{ categoryLabel(id) }}
      <i class="pi pi-times" style="cursor: pointer; font-size: .7rem" @click="clearCategoryIds" />
    </Tag>
    <span class="grow" />
    <template v-if="selection.length">
      <span class="small muted">{{ selection.length }} selected</span>
      <Select v-model="bulkCategory" :options="lookups.activeCategories" option-label="name" option-value="id"
        placeholder="Category" filter size="small" style="width: 11rem" />
      <Button label="Set category" size="small" :disabled="bulkCategory === null" :loading="bulkBusy"
        @click="bulk({ categoryId: bulkCategory })" />
      <Button label="Mark reimbursed" size="small" severity="secondary" :loading="bulkBusy" @click="bulk({ reimbursed: true })" />
      <Select v-model="bulkGroup" :options="lookups.groups" option-label="name" option-value="id" placeholder="Group"
        size="small" style="width: 10rem" />
      <Button label="Add to group" size="small" severity="secondary" :disabled="!bulkGroup" :loading="bulkBusy"
        @click="bulk({ addGroupIds: [bulkGroup!] })" />
    </template>
    <Button label="Create group" icon="pi pi-plus" size="small" outlined @click="groupDialog = true" />
  </div>

  <div class="card" style="padding: 0">
    <DataTable
      v-model:selection="selection"
      v-model:expanded-rows="expandedRows"
      :value="items"
      :loading="loading"
      lazy
      paginator
      :rows="pageSize"
      :first="(page - 1) * pageSize"
      :total-records="data?.total ?? 0"
      :rows-per-page-options="[50, 100, 200]"
      :sort-field="sort"
      :sort-order="dir === 'asc' ? 1 : -1"
      data-key="id"
      size="small"
      row-hover
      scrollable
      @page="onPage"
      @sort="onSort"
    >
      <template #empty><div class="empty">No transactions in this range</div></template>
      <template #footer>
        <div class="flex between small">
          <span>{{ data?.total ?? 0 }} transactions</span>
          <span>Sum: <strong :class="(data?.sumAmount ?? 0) < 0 ? 'neg' : 'pos'">{{ formatMoney(data?.sumAmount ?? 0) }}</strong></span>
        </div>
      </template>

      <Column selection-mode="multiple" header-style="width: 3rem" />
      <Column expander header-style="width: 3rem" />
      <Column field="date" header="Date" sortable body-class="nowrap">
        <template #body="{ data: t }">{{ formatDate(t.date) }}</template>
      </Column>
      <Column header="Account">
        <template #body="{ data: t }">
          <div class="small">{{ t.bankName }}</div>
          <div class="muted small">{{ t.accountName }}</div>
        </template>
      </Column>
      <Column field="counterpartyName" header="Counterparty" sortable>
        <template #body="{ data: t }"><span class="truncate" style="max-width: 14rem" v-tooltip.top="t.counterpartyIban">{{ t.counterpartyName }}</span></template>
      </Column>
      <Column header="Description">
        <template #body="{ data: t }"><span class="truncate" v-tooltip.top="t.description">{{ t.description }}</span></template>
      </Column>
      <Column field="amount" header="Amount" sortable body-class="right nowrap" header-class="right">
        <template #body="{ data: t }"><span :class="t.amount < 0 ? 'neg' : 'pos'">{{ formatMoney(t.amount, t.currency) }}</span></template>
      </Column>
      <Column field="categoryId" sort-field="category" header="Category" sortable header-style="min-width: 16rem">
        <template #body="{ data: t }">
          <div class="flex center gap-1">
            <Select :model-value="t.categoryId" :options="lookups.activeCategories" option-label="name" option-value="id"
              placeholder="—" filter show-clear size="small" style="width: 11rem"
              @update:model-value="patch(t.id, { categoryId: $event ?? null })">
              <template #option="{ option }">
                <span class="dot" :style="{ background: categoryColor(option) }" />{{ option.name }}
              </template>
            </Select>
            <Tag v-if="sourceBadge(t)" :value="sourceBadge(t)!.label" :severity="sourceBadge(t)!.severity" />
            <Tag v-if="t.latestClassification" :value="formatPercent(t.latestClassification.confidence)"
              :severity="t.latestClassification.accepted ? 'success' : 'warn'" v-tooltip.top="'AI confidence'" />
          </div>
        </template>
      </Column>
      <Column header="Groups" header-style="min-width: 12rem">
        <template #body="{ data: t }">
          <MultiSelect :model-value="t.groupIds" :options="lookups.groups" option-label="name" option-value="id"
            display="chip" placeholder="—" size="small" style="width: 11rem" :max-selected-labels="2"
            @update:model-value="patch(t.id, { groupIds: $event })" />
        </template>
      </Column>
      <Column header="Reimb." header-style="width: 5rem" body-class="center">
        <template #body="{ data: t }">
          <Checkbox :model-value="t.reimbursed" binary @update:model-value="patch(t.id, { reimbursed: $event })" />
        </template>
      </Column>

      <template #expansion="{ data: t }">
        <ClassificationHistory :transaction-id="t.id" @reclassified="refreshRow(t.id)" />
      </template>
    </DataTable>
  </div>

  <GroupDialog v-model:visible="groupDialog" />
</template>
