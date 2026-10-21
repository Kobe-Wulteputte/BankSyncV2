<script setup lang="ts">
import Checkbox from 'primevue/checkbox'
import DatePicker from 'primevue/datepicker'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import InputText from 'primevue/inputtext'
import MultiSelect from 'primevue/multiselect'
import Select from 'primevue/select'
import { computed, ref, watch } from 'vue'
import { useFilterStore } from '@/stores/filter'
import { useLookupStore } from '@/stores/lookups'
import { PRESET_OPTIONS } from '@/utils/dateRange'
import { parseDate, toIsoDate } from '@/utils/format'

defineProps<{ showSearch?: boolean }>()

const filter = useFilterStore()
const lookups = useLookupStore()

const from = computed({
  get: () => (filter.from ? parseDate(filter.from) : null),
  set: (d: Date | null) => filter.setCustomRange(d ? toIsoDate(d) : null, filter.to),
})
const to = computed({
  get: () => (filter.to ? parseDate(filter.to) : null),
  set: (d: Date | null) => filter.setCustomRange(filter.from, d ? toIsoDate(d) : null),
})

// The UI shows what is INCLUDED (everything checked by default); the store and the API keep the
// exclude lists, so a category or group created later is included without touching saved filters.
const allCategoryIds = computed(() => lookups.activeCategories.map((c) => c.id))
const includedCategoryIds = computed<number[]>({
  get: () => allCategoryIds.value.filter((id) => !filter.excludeCategoryIds.includes(id)),
  set: (ids) => (filter.excludeCategoryIds = allCategoryIds.value.filter((id) => !ids.includes(id))),
})

const allGroupIds = computed(() => lookups.groups.map((g) => g.id))
const includedGroupIds = computed<string[]>({
  get: () => allGroupIds.value.filter((id) => !filter.excludeGroupIds.includes(id)),
  set: (ids) => (filter.excludeGroupIds = allGroupIds.value.filter((id) => !ids.includes(id))),
})

const accountOptions = computed(() =>
  lookups.accounts.map((a) => ({ id: a.id, label: `${a.bankName} · ${a.displayName || a.identifier}` })),
)
// Accounts are an include list in the API; an empty list means all, so "all checked" maps to [].
const includedAccountIds = computed<string[]>({
  get: () => (filter.accountIds.length ? filter.accountIds : accountOptions.value.map((a) => a.id)),
  set: (ids) => (filter.accountIds = ids.length === 0 || ids.length === accountOptions.value.length ? [] : ids),
})

const includeReimbursed = computed<boolean>({
  get: () => !filter.excludeReimbursed,
  set: (v) => (filter.excludeReimbursed = !v),
})

const countLabel = (selected: number, total: number, noun: string) =>
  selected === total ? `All ${noun}` : `{0} of ${total} ${noun}`

// search is committed on enter / blur so each keystroke does not hit the API
const search = ref(filter.search)
watch(() => filter.search, (v) => (search.value = v))
const commitSearch = () => (filter.search = search.value)
</script>

<template>
  <div class="card flex wrap center gap-2 mb-3">
    <Select v-model="filter.preset" :options="PRESET_OPTIONS" option-label="label" option-value="value" size="small"
      style="width: 11rem" @update:model-value="filter.setPreset($event)" />
    <template v-if="filter.preset === 'custom'">
      <DatePicker v-model="from" date-format="dd/mm/yy" placeholder="From" show-icon size="small" style="width: 10rem" />
      <DatePicker v-model="to" date-format="dd/mm/yy" placeholder="To" show-icon size="small" style="width: 10rem" />
    </template>
    <MultiSelect v-model="includedCategoryIds" :options="lookups.activeCategories" option-label="name" option-value="id"
      placeholder="Categories" filter :max-selected-labels="1"
      :selected-items-label="countLabel(includedCategoryIds.length, allCategoryIds.length, 'categories')"
      size="small" style="width: 13rem" />
    <MultiSelect v-model="includedAccountIds" :options="accountOptions" option-label="label" option-value="id"
      placeholder="Accounts" filter :max-selected-labels="1"
      :selected-items-label="countLabel(includedAccountIds.length, accountOptions.length, 'accounts')"
      size="small" style="width: 13rem" />
    <MultiSelect v-model="includedGroupIds" :options="lookups.groups" option-label="name" option-value="id"
      placeholder="Groups" filter :max-selected-labels="1"
      :selected-items-label="countLabel(includedGroupIds.length, allGroupIds.length, 'groups')"
      size="small" style="width: 12rem" />
    <label class="flex center gap-2 small nowrap">
      <Checkbox v-model="includeReimbursed" binary /> Reimbursed
    </label>
    <IconField v-if="showSearch" class="grow" style="min-width: 12rem">
      <InputIcon class="pi pi-search" />
      <InputText v-model="search" placeholder="Search counterparty / description" size="small" fluid
        @keyup.enter="commitSearch" @blur="commitSearch" />
    </IconField>
  </div>
</template>
