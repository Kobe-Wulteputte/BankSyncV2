<script setup lang="ts">
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Column from 'primevue/column'
import DataTable, { type DataTableSortEvent } from 'primevue/datatable'
import Drawer from 'primevue/drawer'
import MultiSelect from 'primevue/multiselect'
import Paginator from 'primevue/paginator'
import Select from 'primevue/select'
import Skeleton from 'primevue/skeleton'
import Tag from 'primevue/tag'
import ToggleSwitch from 'primevue/toggleswitch'
import { useToast } from 'primevue/usetoast'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { bulkUpdateTransactions, confirmTransaction, getTransaction, listTransactions, patchTransaction } from '@/api/transactions'
import type { PatchTransactionBody, TransactionDto, TransactionPage, TransactionSort } from '@/api/types'
import ClassificationHistory from '@/components/ClassificationHistory.vue'
import FilterBar from '@/components/FilterBar.vue'
import GroupDialog from '@/components/GroupDialog.vue'
import TransactionDialog from '@/components/TransactionDialog.vue'
import { useAsync, useMobile } from '@/composables'
import { useFilterStore } from '@/stores/filter'
import { useLookupStore } from '@/stores/lookups'
import { categoryColor, formatDate, formatMoney } from '@/utils/format'

const filter = useFilterStore()
const lookups = useLookupStore()
const route = useRoute()
const router = useRouter()
const toast = useToast()
const mobile = useMobile()

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

const listEl = ref<HTMLElement | null>(null)
function onPage(e: { page: number; rows: number }) {
  page.value = e.page + 1
  pageSize.value = e.rows
  listEl.value?.scrollIntoView({ block: 'start' })
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
// Optimistic: a confirmed row no longer matches "unclassified or unconfirmed", so it leaves the list at once.
async function confirm(t: TransactionDto) {
  const list = data.value
  if (list && unclassifiedOnly.value) {
    list.items = list.items.filter((r) => r.id !== t.id)
    list.total--
    list.sumAmount -= t.amount
    selection.value = selection.value.filter((r) => r.id !== t.id)
  } else {
    replaceRow({ ...t, classificationSource: 'Manual' })
  }
  try {
    const row = await confirmTransaction(t.id)
    if (!unclassifiedOnly.value) replaceRow(row)
  } catch {
    run() // toasted; reload to undo the optimistic change
  }
}
const txDialog = ref(false)
const editing = ref<TransactionDto | null>(null)
function openTxDialog(t: TransactionDto | null) {
  editing.value = t
  txDialog.value = true
}
// A new or edited entry may fall outside the current filter or sort position, so reload the page.
const onTxSaved = () => run()

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
    bulkSheet.value = false
    selecting.value = false
    await run()
  } catch {
    // toasted
  } finally {
    bulkBusy.value = false
  }
}

// Phone: cards instead of the table. Sorting moves to a select, row selection behind a "Select" mode,
// the expander becomes tapping a card, and the bulk actions open in a bottom sheet.
const SORT_OPTIONS = [
  { label: 'Newest first', value: 'date:desc' },
  { label: 'Oldest first', value: 'date:asc' },
  { label: 'Amount: low to high', value: 'amount:asc' },
  { label: 'Amount: high to low', value: 'amount:desc' },
  { label: 'Counterparty A–Z', value: 'counterpartyName:asc' },
  { label: 'Counterparty Z–A', value: 'counterpartyName:desc' },
  { label: 'Category A–Z', value: 'category:asc' },
  { label: 'Category Z–A', value: 'category:desc' },
]
const sortKey = computed({
  get: () => `${sort.value}:${dir.value}`,
  set: (v: string) => {
    const [s, d] = v.split(':')
    sort.value = s as TransactionSort
    dir.value = d as 'asc' | 'desc'
  },
})
const selecting = ref(false)
const bulkSheet = ref(false)
watch(selecting, (on) => {
  if (!on) selection.value = []
})
const isSelected = (t: TransactionDto) => selection.value.some((s) => s.id === t.id)
const isExpanded = (t: TransactionDto) => expandedRows.value.some((r) => r.id === t.id)
function tapCard(t: TransactionDto) {
  const list = selecting.value ? selection : expandedRows
  list.value = list.value.some((r) => r.id === t.id) ? list.value.filter((r) => r.id !== t.id) : [...list.value, t]
}
const groupName = (id: string) => lookups.groupById.get(id)?.name ?? '?'
const groupColor = (id: string) => lookups.groupById.get(id)?.color ?? 'var(--p-surface-400)'
/** The model's group suggestion, unless the transaction already has it or the group is gone. */
function suggestedGroup(t: TransactionDto) {
  const id = t.latestClassification?.suggestedGroupId
  return id && !t.groupIds.includes(id) && lookups.groupById.has(id) ? id : null
}
const addSuggestedGroup = (t: TransactionDto) => patch(t.id, { groupIds: [...t.groupIds, suggestedGroup(t)!] })

const groupDialog = ref(false)
</script>

<template>
  <div class="fill">
  <h1 class="page-title">Transactions</h1>
  <FilterBar show-search />

  <template v-if="mobile">
    <div class="flex center gap-2 mb-2">
      <Select v-model="sortKey" :options="SORT_OPTIONS" option-label="label" option-value="value" class="grow" aria-label="Sort" />
      <Button :label="selecting ? 'Done' : 'Select'" :icon="selecting ? undefined : 'pi pi-check-square'" severity="secondary"
        :outlined="!selecting" @click="selecting = !selecting" />
    </div>
    <div class="flex wrap center gap-2 mb-3">
      <label class="flex center gap-2 small nowrap"><ToggleSwitch v-model="unclassifiedOnly" /> Unclassified or unconfirmed</label>
      <Tag v-for="id in categoryIds" :key="id" severity="info" class="flex center gap-1">
        Category: {{ categoryLabel(id) }}
        <button type="button" class="tag-x" aria-label="Clear category filter" @click="clearCategoryIds"><i class="pi pi-times" /></button>
      </Tag>
      <span class="grow" />
      <Button label="Group" icon="pi pi-plus" text size="small" @click="groupDialog = true" />
    </div>

    <div ref="listEl" class="card list tx-list">
      <div class="list-item small muted between">
        <span>{{ data?.total ?? 0 }} transactions</span>
        <span>Sum: <strong class="amount" :class="(data?.sumAmount ?? 0) < 0 ? 'neg' : 'pos'">{{ formatMoney(data?.sumAmount ?? 0) }}</strong></span>
      </div>
      <template v-if="loading && !items.length">
        <div v-for="i in 6" :key="i" class="list-item"><Skeleton height="3rem" /></div>
      </template>
      <div v-else-if="!items.length" class="empty">No transactions in this range</div>
      <article v-for="t in items" :key="t.id" class="tx" :class="{ selected: isSelected(t), dim: loading }">
        <div class="tx-main tappable" role="button" tabindex="0" :aria-expanded="selecting ? undefined : isExpanded(t)"
          :aria-pressed="selecting ? isSelected(t) : undefined" @click="tapCard(t)" @keydown.enter.prevent="tapCard(t)">
          <Checkbox v-if="selecting" :model-value="isSelected(t)" binary :tabindex="-1" style="pointer-events: none" />
          <div class="grow col" style="min-width: 0; gap: .15rem">
            <div class="flex between gap-2">
              <strong class="truncate">{{ t.counterpartyName || t.description || '—' }}</strong>
              <span class="amount" :class="t.amount < 0 ? 'neg' : 'pos'">{{ formatMoney(t.amount, t.currency) }}</span>
            </div>
            <div class="muted small truncate">{{ formatDate(t.date) }} · {{ t.bankName }} {{ t.accountName }}</div>
          </div>
        </div>
        <div class="tx-meta">
          <Select :model-value="t.categoryId" :options="lookups.activeCategories" option-label="name" option-value="id"
            placeholder="No category" filter show-clear size="small" class="cat-select" aria-label="Category"
            @update:model-value="patch(t.id, { categoryId: $event ?? null })">
            <template #value="{ value, placeholder }">
              <span v-if="value !== null && value !== undefined" class="nowrap">
                <span class="dot" :style="{ background: categoryColor(lookups.categoryById.get(value)) }" />{{ lookups.categoryById.get(value)?.name }}
              </span>
              <span v-else>{{ placeholder }}</span>
            </template>
            <template #option="{ option }">
              <span class="dot" :style="{ background: categoryColor(option) }" />{{ option.name }}
            </template>
          </Select>
          <Button v-if="t.classificationSource === 'Ai'" icon="pi pi-check" label="Confirm" severity="success" outlined size="small"
            @click="confirm(t)" />
          <Tag v-for="g in t.groupIds" :key="g" severity="secondary"><span class="dot" :style="{ background: groupColor(g) }" />{{ groupName(g) }}</Tag>
          <Tag v-if="suggestedGroup(t)" severity="info" icon="pi pi-plus" :value="`${groupName(suggestedGroup(t)!)}?`" style="cursor: pointer"
            @click.stop="addSuggestedGroup(t)" />
          <Tag v-if="t.reimbursed" severity="secondary" icon="pi pi-replay" value="Reimbursed" />
        </div>
        <div v-if="isExpanded(t) && !selecting" class="tx-details col gap-3">
          <dl class="small">
            <template v-if="t.counterpartyIban"><dt class="muted">IBAN</dt><dd>{{ t.counterpartyIban }}</dd></template>
            <template v-if="t.description"><dt class="muted">Description</dt><dd>{{ t.description }}</dd></template>
            <template v-if="t.notes"><dt class="muted">Notes</dt><dd>{{ t.notes }}</dd></template>
          </dl>
          <MultiSelect :model-value="t.groupIds" :options="lookups.groups" option-label="name" option-value="id"
            display="chip" placeholder="Groups" fluid @update:model-value="patch(t.id, { groupIds: $event })" />
          <div class="flex center between gap-2">
            <label class="flex center gap-2">
              <Checkbox :model-value="t.reimbursed" binary @update:model-value="patch(t.id, { reimbursed: $event })" /> Reimbursed
            </label>
            <Button label="Edit" icon="pi pi-pencil" severity="secondary" outlined @click="openTxDialog(t)" />
          </div>
          <ClassificationHistory :transaction-id="t.id" @reclassified="refreshRow(t.id)" />
        </div>
      </article>
    </div>
    <Paginator v-if="(data?.total ?? 0) > 50" :rows="pageSize" :first="(page - 1) * pageSize" :total-records="data?.total ?? 0"
      :rows-per-page-options="[50, 100, 200]" template="PrevPageLink CurrentPageReport NextPageLink RowsPerPageDropdown"
      current-page-report-template="{currentPage} / {totalPages}" class="mt-3" @page="onPage" />

    <div v-if="selecting" class="bulk-bar">
      <span class="grow">{{ selection.length }} selected</span>
      <Button label="Edit selected" icon="pi pi-pencil" :disabled="!selection.length" @click="bulkSheet = true" />
    </div>
    <Button v-else icon="pi pi-plus" rounded aria-label="Add transaction" class="fab" @click="openTxDialog(null)" />
    <Drawer v-model:visible="bulkSheet" position="bottom" :header="`Edit ${selection.length} selected`" class="sheet">
      <div class="col gap-3">
        <div class="flex gap-2">
          <Select v-model="bulkCategory" :options="lookups.activeCategories" option-label="name" option-value="id"
            placeholder="Category" filter class="grow" style="min-width: 0" />
          <Button label="Set" :disabled="bulkCategory === null" :loading="bulkBusy" @click="bulk({ categoryId: bulkCategory })" />
        </div>
        <div class="flex gap-2">
          <Select v-model="bulkGroup" :options="lookups.groups" option-label="name" option-value="id" placeholder="Group"
            class="grow" style="min-width: 0" />
          <Button label="Add" severity="secondary" :disabled="!bulkGroup" :loading="bulkBusy" @click="bulk({ addGroupIds: [bulkGroup!] })" />
        </div>
        <Button label="Mark reimbursed" severity="secondary" :loading="bulkBusy" @click="bulk({ reimbursed: true })" />
      </div>
    </Drawer>
  </template>

  <template v-else>
  <div class="card flex wrap center gap-3 mb-3">
    <label class="flex center gap-2 small nowrap"><ToggleSwitch v-model="unclassifiedOnly" /> Unclassified or unconfirmed</label>
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
    <Button label="Add transaction" icon="pi pi-plus" size="small" @click="openTxDialog(null)" />
  </div>

  <div class="card table-card" style="padding: 0">
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
      scroll-height="flex"
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
            <Button v-if="t.classificationSource === 'Ai'" icon="pi pi-check" severity="success" text rounded size="small"
              aria-label="Confirm category" v-tooltip.top="'Confirm AI category'" @click="confirm(t)" />
          </div>
        </template>
      </Column>
      <Column header="Groups" header-style="min-width: 12rem">
        <template #body="{ data: t }">
          <div class="flex center gap-1">
            <MultiSelect :model-value="t.groupIds" :options="lookups.groups" option-label="name" option-value="id"
              display="chip" placeholder="—" size="small" style="width: 11rem" :max-selected-labels="2"
              @update:model-value="patch(t.id, { groupIds: $event })" />
            <Tag v-if="suggestedGroup(t)" severity="info" icon="pi pi-plus" :value="`${groupName(suggestedGroup(t)!)}?`" style="cursor: pointer"
              v-tooltip.top="'Suggested group: click to add'" @click="addSuggestedGroup(t)" />
          </div>
        </template>
      </Column>
      <Column header="Reimb." header-style="width: 5rem" body-class="center">
        <template #body="{ data: t }">
          <Checkbox :model-value="t.reimbursed" binary @update:model-value="patch(t.id, { reimbursed: $event })" />
        </template>
      </Column>

      <Column header-style="width: 3rem" body-class="center">
        <template #body="{ data: t }">
          <Button icon="pi pi-pencil" text rounded size="small" aria-label="Edit" v-tooltip.top="'Edit'" @click="openTxDialog(t)" />
        </template>
      </Column>

      <template #expansion="{ data: t }">
        <ClassificationHistory :transaction-id="t.id" @reclassified="refreshRow(t.id)" />
      </template>
    </DataTable>
  </div>
  </template>

  <GroupDialog v-model:visible="groupDialog" />
  <TransactionDialog v-model:visible="txDialog" :transaction="editing" @saved="onTxSaved" />
  </div>
</template>

<style scoped>
/* The page fills the content area and only the table body scrolls; header, paginator and sum stay put. */
.fill { display: flex; flex-direction: column; height: 100%; }
.table-card { flex: 1; min-height: 20rem; display: flex; flex-direction: column; }
@media (max-width: 800px) {
  .fill { height: auto; padding-bottom: 4.5rem; }
}
.tx + .tx, .tx-list > .list-item + .tx { border-top: 1px solid var(--p-content-border-color); }
.tx.selected { background: var(--p-highlight-background); }
.tx.dim { opacity: .6; }
.tx-main { display: flex; align-items: center; gap: .75rem; padding: .75rem 1rem .4rem; }
.tx-meta { display: flex; flex-wrap: wrap; align-items: center; gap: .375rem; padding: 0 1rem .75rem; }
.tx-meta :deep(.p-tag) { font-weight: 500; }
.cat-select { border-radius: 2rem; max-width: 100%; }
.tx-details { padding: .25rem 1rem 1rem; }
.tx-details dl { display: grid; grid-template-columns: auto 1fr; gap: .25rem .75rem; margin: 0; }
.tx-details dd { margin: 0; overflow-wrap: anywhere; }
.tag-x { display: inline-grid; place-items: center; padding: .25rem; margin: -.25rem; font-size: .7rem; color: inherit; background: none; border: 0; cursor: pointer; }
.bulk-bar {
  position: sticky; bottom: 0; z-index: 1; display: flex; align-items: center; gap: .75rem; margin: 1rem -1rem -1rem;
  padding: .75rem 1rem; background: var(--p-content-background); border-top: 1px solid var(--p-content-border-color);
  box-shadow: 0 -4px 12px rgb(0 0 0 / .06);
}
.fab {
  position: fixed; right: 1rem; bottom: calc(5rem + env(safe-area-inset-bottom)); z-index: 2;
  width: 3.5rem; height: 3.5rem; box-shadow: 0 4px 14px rgb(0 0 0 / .2);
}
.fab :deep(.p-button-icon) { font-size: 1.2rem; }
</style>
