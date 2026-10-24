<script setup lang="ts">
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import DatePicker from 'primevue/datepicker'
import Drawer from 'primevue/drawer'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import InputText from 'primevue/inputtext'
import MultiSelect from 'primevue/multiselect'
import Select from 'primevue/select'
import { computed, ref, watch, type FunctionalComponent } from 'vue'
import { useMobile } from '@/composables'
import { useFilterStore } from '@/stores/filter'
import { useLookupStore } from '@/stores/lookups'
import { applyClassSelection, selectedClasses, UNCLASSED } from '@/utils/classFilter'
import { PRESET_OPTIONS } from '@/utils/dateRange'
import { parseDate, toIsoDate } from '@/utils/format'

defineProps<{ showSearch?: boolean }>()

const filter = useFilterStore()
const lookups = useLookupStore()
const mobile = useMobile()
// phones get regular-size controls: bigger touch targets
const size = computed(() => (mobile.value ? undefined : 'small'))

// On a phone the secondary filters move into a bottom sheet; on desktop they render inline as before.
const Inline: FunctionalComponent = (_, { slots }) => slots.default?.()
const sheetOpen = ref(false)
const sheetProps = computed(() =>
  mobile.value
    ? { visible: sheetOpen.value, 'onUpdate:visible': (v: boolean) => (sheetOpen.value = v), position: 'bottom', header: 'Filters', class: 'sheet' }
    : {},
)
const activeFilters = computed(
  () => [filter.excludeCategoryIds.length, filter.accountIds.length, filter.excludeGroupIds.length, !filter.excludeReimbursed].filter(Boolean).length,
)

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

// Shortcut over the category filter, so the API and saved URLs need nothing new.
const classOptions = computed(() => {
  const used = new Set(lookups.activeCategories.map((c) => c.categoryClassId ?? UNCLASSED))
  return [
    ...lookups.classes.filter((c) => used.has(c.id)).map((c) => ({ id: c.id, name: c.name })),
    ...(used.has(UNCLASSED) ? [{ id: UNCLASSED, name: 'Unclassed' }] : []),
  ]
})
const includedClassIds = computed<number[]>({
  get: () => selectedClasses(lookups.activeCategories, includedCategoryIds.value),
  set: (ids) => (includedCategoryIds.value = applyClassSelection(lookups.activeCategories, includedCategoryIds.value, ids)),
})

// The empty GUID is a sentinel: the API reads it as "no group" in the group filter (like category 0
// for uncategorized) and it matches no account in the account filter.
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'
const groupOptions = computed(() => [{ id: EMPTY_GUID, name: 'Empty group' }, ...lookups.groups])
const allGroupIds = computed(() => groupOptions.value.map((g) => g.id))
const includedGroupIds = computed<string[]>({
  get: () => allGroupIds.value.filter((id) => !filter.excludeGroupIds.includes(id)),
  set: (ids) => (filter.excludeGroupIds = allGroupIds.value.filter((id) => !ids.includes(id))),
})

const accountOptions = computed(() =>
  lookups.accounts.map((a) => ({ id: a.id, label: `${a.bankName} · ${a.displayName || a.identifier}` })),
)
// Accounts are an include list in the API where an empty list means all: "all checked" maps to [] and
// "none checked" to the empty GUID, which matches no account (an empty list would read as all again).
const includedAccountIds = computed<string[]>({
  get: () => (filter.accountIds.length ? filter.accountIds.filter((id) => id !== EMPTY_GUID) : accountOptions.value.map((a) => a.id)),
  set: (ids) => (filter.accountIds = ids.length === accountOptions.value.length ? [] : ids.length === 0 ? [EMPTY_GUID] : ids),
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
  <div class="card flex wrap center gap-2 mb-3 filters">
    <Select v-model="filter.preset" :options="PRESET_OPTIONS" option-label="label" option-value="value" :size="size"
      class="f-preset" @update:model-value="filter.setPreset($event)" />
    <Button v-if="mobile" icon="pi pi-sliders-h" :label="activeFilters ? `Filters · ${activeFilters}` : 'Filters'"
      :severity="activeFilters ? undefined : 'secondary'" outlined aria-haspopup="dialog" @click="sheetOpen = true" />
    <template v-if="filter.preset === 'custom'">
      <DatePicker v-model="from" date-format="dd/mm/yy" placeholder="From" show-icon :size="size" class="f-date" />
      <DatePicker v-model="to" date-format="dd/mm/yy" placeholder="To" show-icon :size="size" class="f-date" />
    </template>
    <component :is="mobile ? Drawer : Inline" v-bind="sheetProps">
      <div class="f-more">
        <MultiSelect v-model="includedClassIds" :options="classOptions" option-label="name" option-value="id"
          placeholder="Classes" :max-selected-labels="1"
          :selected-items-label="countLabel(includedClassIds.length, classOptions.length, 'classes')"
          :size="size" class="f-class" />
        <MultiSelect v-model="includedCategoryIds" :options="lookups.activeCategories" option-label="name" option-value="id"
          placeholder="Categories" filter :max-selected-labels="1"
          :selected-items-label="countLabel(includedCategoryIds.length, allCategoryIds.length, 'categories')"
          :size="size" class="f-category" />
        <MultiSelect v-model="includedAccountIds" :options="accountOptions" option-label="label" option-value="id"
          placeholder="Accounts" filter :max-selected-labels="1"
          :selected-items-label="countLabel(includedAccountIds.length, accountOptions.length, 'accounts')"
          :size="size" class="f-account" />
        <MultiSelect v-model="includedGroupIds" :options="groupOptions" option-label="name" option-value="id"
          placeholder="Groups" filter :max-selected-labels="1"
          :selected-items-label="countLabel(includedGroupIds.length, allGroupIds.length, 'groups')"
          :size="size" class="f-group" />
        <label class="flex center gap-2 small nowrap f-reimbursed">
          <Checkbox v-model="includeReimbursed" binary /> Reimbursed
        </label>
        <Button v-if="mobile" label="Done" @click="sheetOpen = false" />
      </div>
    </component>
    <IconField v-if="showSearch" class="grow f-search" style="min-width: 12rem">
      <InputIcon class="pi pi-search" />
      <InputText v-model="search" placeholder="Search counterparty / description" :size="size" fluid
        @keyup.enter="commitSearch" @blur="commitSearch" />
    </IconField>
  </div>
</template>

<style scoped>
.f-preset, .f-class { width: 11rem; }
.f-date { width: 10rem; }
.f-category, .f-account { width: 13rem; }
.f-group { width: 12rem; }
.f-more { display: contents; }
@media (max-width: 800px) {
  .filters { padding: 0; background: none; border: 0; }
  .f-preset { flex: 1 1 0; width: auto; min-width: 0; }
  .f-date { flex: 1 1 0; width: auto; min-width: 0; }
  .f-search { flex-basis: 100%; }
  .f-more { display: flex; flex-direction: column; gap: .75rem; }
  .f-more > * { width: 100%; }
  .f-reimbursed { font-size: 1rem; padding: .25rem 0; }
}
</style>
