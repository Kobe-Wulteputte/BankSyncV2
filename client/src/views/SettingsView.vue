<script setup lang="ts">
import Button from 'primevue/button'
import ColorPicker from 'primevue/colorpicker'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Dialog from 'primevue/dialog'
import FileUpload, { type FileUploadUploaderEvent } from 'primevue/fileupload'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Tag from 'primevue/tag'
import ToggleSwitch from 'primevue/toggleswitch'
import { useConfirm } from 'primevue/useconfirm'
import { computed, onMounted, ref } from 'vue'
import { createCategory, updateCategory } from '@/api/categories'
import { createCategoryClass, removeCategoryClass, updateCategoryClass } from '@/api/categoryClasses'
import { deleteGroup } from '@/api/groups'
import { importExcel } from '@/api/importer'
import { getMe } from '@/api/me'
import type { CategoryClassDto, CategoryDto, CategoryKind, GroupDto, ImportResult } from '@/api/types'
import GroupDialog from '@/components/GroupDialog.vue'
import { useLookupStore } from '@/stores/lookups'
import { categoryColor, classColor } from '@/utils/format'

const lookups = useLookupStore()
const confirm = useConfirm()
const KINDS: CategoryKind[] = ['Expense', 'Income', 'Transfer']

// categories
const catDialog = ref(false)
const editingCat = ref<CategoryDto | null>(null)
const catForm = ref({ code: '', name: '', kind: 'Expense' as CategoryKind, color: null as string | null, isActive: true, sortOrder: 0, categoryClassId: null as number | null })
const catSaving = ref(false)

function openCategory(c: CategoryDto | null) {
  editingCat.value = c
  catForm.value = {
    code: c?.code ?? '',
    name: c?.name ?? '',
    kind: c?.kind ?? 'Expense',
    color: c?.color?.replace('#', '') ?? null,
    isActive: c?.isActive ?? true,
    sortOrder: c?.sortOrder ?? lookups.categories.length,
    categoryClassId: c?.categoryClassId ?? null,
  }
  catDialog.value = true
}
async function saveCategory() {
  catSaving.value = true
  const f = catForm.value
  const color = f.color ? `#${f.color}` : null
  try {
    if (editingCat.value) {
      await updateCategory(editingCat.value.id, { name: f.name.trim(), kind: f.kind, color, isActive: f.isActive, sortOrder: f.sortOrder, categoryClassId: f.categoryClassId })
    } else {
      await createCategory({ code: f.code.trim(), name: f.name.trim(), kind: f.kind, color, categoryClassId: f.categoryClassId })
    }
    await lookups.reloadCategories()
    catDialog.value = false
  } catch {
    // toasted
  } finally {
    catSaving.value = false
  }
}

async function setCategoryClass(c: CategoryDto, categoryClassId: number | null) {
  try {
    await updateCategory(c.id, {
      name: c.name, kind: c.kind, color: c.color, isActive: c.isActive, sortOrder: c.sortOrder, categoryClassId,
    })
  } catch {
    // toasted
  }
  await Promise.all([lookups.reloadCategories(), lookups.reloadClasses()])
}

// category classes
const classOptions = computed(() => [{ id: null as number | null, name: 'None' }, ...lookups.classes])
const classDialog = ref(false)
const editingClass = ref<CategoryClassDto | null>(null)
const classForm = ref({ name: '', color: null as string | null, sortOrder: 0 })
const classSaving = ref(false)

function openClass(c: CategoryClassDto | null) {
  editingClass.value = c
  classForm.value = { name: c?.name ?? '', color: c?.color?.replace('#', '') ?? null, sortOrder: c?.sortOrder ?? lookups.classes.length }
  classDialog.value = true
}
async function saveClass() {
  classSaving.value = true
  const f = classForm.value
  const body = { name: f.name.trim(), color: f.color ? `#${f.color}` : null, sortOrder: f.sortOrder }
  try {
    if (editingClass.value) await updateCategoryClass(editingClass.value.id, body)
    else await createCategoryClass(body)
    await lookups.reloadClasses()
    classDialog.value = false
  } catch {
    // toasted
  } finally {
    classSaving.value = false
  }
}
function removeClass(c: CategoryClassDto) {
  confirm.require({
    header: `Delete class "${c.name}"?`,
    message: `${c.categoryCount} categor${c.categoryCount === 1 ? 'y' : 'ies'} will become unclassed.`,
    icon: 'pi pi-exclamation-triangle',
    acceptProps: { severity: 'danger', label: 'Delete' },
    rejectProps: { text: true, label: 'Cancel' },
    accept: async () => {
      try {
        await removeCategoryClass(c.id)
      } catch {
        // toasted
      }
      await Promise.all([lookups.reloadClasses(), lookups.reloadCategories()])
    },
  })
}

// groups
const groupDialog = ref(false)
const editingGroup = ref<GroupDto | null>(null)
function openGroup(g: GroupDto | null) {
  editingGroup.value = g
  groupDialog.value = true
}
function removeGroup(g: GroupDto) {
  confirm.require({
    header: `Delete group "${g.name}"?`,
    message: `It is detached from ${g.transactionCount} transaction(s). Transactions themselves are kept.`,
    icon: 'pi pi-exclamation-triangle',
    acceptProps: { severity: 'danger', label: 'Delete' },
    rejectProps: { text: true, label: 'Cancel' },
    accept: async () => {
      try {
        await deleteGroup(g.id)
        await lookups.reloadGroups()
      } catch {
        // toasted
      }
    },
  })
}

// Categories and classes are shared by every user, so only admins change them (the API enforces it).
const isAdmin = ref(false)
onMounted(async () => {
  isAdmin.value = (await getMe()).isAdmin
})

// import
const importing = ref(false)
const importResult = ref<ImportResult | null>(null)
async function upload(e: FileUploadUploaderEvent) {
  const file = Array.isArray(e.files) ? e.files[0] : e.files
  if (!file) return
  importing.value = true
  importResult.value = null
  try {
    importResult.value = await importExcel(file)
    void lookups.load(true)
  } catch {
    // toasted
  } finally {
    importing.value = false
  }
}
</script>

<template>
  <h1 class="page-title">Settings</h1>

  <section class="card mb-3" style="padding: 0">
    <div class="flex center between" style="padding: 1rem">
      <h2 style="margin: 0; font-size: 1.1rem">Category classes</h2>
      <Button v-if="isAdmin" label="New class" icon="pi pi-plus" size="small" @click="openClass(null)" />
    </div>
    <DataTable :value="lookups.classes" size="small" data-key="id" sort-field="sortOrder" :sort-order="1">
      <template #empty><div class="empty">No classes.</div></template>
      <Column field="sortOrder" header="#" sortable header-style="width: 4rem" />
      <Column field="name" header="Name" sortable>
        <template #body="{ data: c }"><span class="dot" :style="{ background: classColor(c) }" />{{ c.name }}</template>
      </Column>
      <Column field="categoryCount" header="Categories" sortable />
      <Column v-if="isAdmin" header-style="width: 7rem">
        <template #body="{ data: c }">
          <Button icon="pi pi-pencil" text size="small" @click="openClass(c)" />
          <Button icon="pi pi-trash" text size="small" severity="danger" @click="removeClass(c)" />
        </template>
      </Column>
    </DataTable>
  </section>

  <section class="card mb-3" style="padding: 0">
    <div class="flex center between" style="padding: 1rem">
      <h2 style="margin: 0; font-size: 1.1rem">Categories</h2>
      <Button v-if="isAdmin" label="New category" icon="pi pi-plus" size="small" @click="openCategory(null)" />
    </div>
    <DataTable :value="lookups.categories" size="small" data-key="id" sort-field="sortOrder" :sort-order="1">
      <template #empty><div class="empty">No categories.</div></template>
      <Column field="sortOrder" header="#" sortable header-style="width: 4rem" />
      <Column field="code" header="Code" sortable />
      <Column field="name" header="Name" sortable>
        <template #body="{ data: c }"><span class="dot" :style="{ background: categoryColor(c) }" />{{ c.name }}</template>
      </Column>
      <Column field="kind" header="Kind" sortable>
        <template #body="{ data: c }">
          <Tag :value="c.kind" :severity="c.kind === 'Income' ? 'success' : c.kind === 'Transfer' ? 'secondary' : 'danger'" />
        </template>
      </Column>
      <Column header="Class">
        <template #body="{ data: c }">
          <Select :model-value="c.categoryClassId" :options="classOptions" option-label="name" option-value="id" size="small" :disabled="!isAdmin"
            @update:model-value="setCategoryClass(c, $event)" />
        </template>
      </Column>
      <Column field="isActive" header="Active" sortable>
        <template #body="{ data: c }"><i :class="c.isActive ? 'pi pi-check pos' : 'pi pi-minus muted'" /></template>
      </Column>
      <Column v-if="isAdmin" header-style="width: 5rem">
        <template #body="{ data: c }"><Button icon="pi pi-pencil" text size="small" @click="openCategory(c)" /></template>
      </Column>
    </DataTable>
  </section>

  <section class="card mb-3" style="padding: 0">
    <div class="flex center between" style="padding: 1rem">
      <h2 style="margin: 0; font-size: 1.1rem">Groups</h2>
      <Button label="New group" icon="pi pi-plus" size="small" @click="openGroup(null)" />
    </div>
    <DataTable :value="lookups.groups" size="small" data-key="id">
      <template #empty><div class="empty">No groups.</div></template>
      <Column field="name" header="Name" sortable>
        <template #body="{ data: g }"><span class="dot" :style="{ background: g.color ?? 'var(--p-surface-400)' }" />{{ g.name }}</template>
      </Column>
      <Column field="transactionCount" header="Transactions" sortable />
      <Column header-style="width: 7rem">
        <template #body="{ data: g }">
          <Button icon="pi pi-pencil" text size="small" @click="openGroup(g)" />
          <Button icon="pi pi-trash" text size="small" severity="danger" @click="removeGroup(g)" />
        </template>
      </Column>
    </DataTable>
  </section>

  <section v-if="isAdmin" class="card col gap-3">
    <h2 style="margin: 0; font-size: 1.1rem">Import V1 Excel</h2>
    <FileUpload mode="basic" custom-upload auto accept=".xlsx,.xls" choose-label="Choose Excel file" :disabled="importing"
      @uploader="upload" />
    <span v-if="importing" class="muted small"><i class="pi pi-spin pi-spinner" /> Importing…</span>
    <div v-if="importResult" class="col gap-1 small">
      <div>
        <strong>{{ importResult.imported }}</strong> imported of {{ importResult.rows }} rows ·
        {{ importResult.updated }} existing rows updated · {{ importResult.skippedDuplicates }} unchanged duplicates ·
        {{ importResult.groupsCreated }} groups, {{ importResult.accountsCreated }} accounts and {{ importResult.categoriesCreated }} categories created
      </div>
      <ul v-if="importResult.errors.length" class="neg" style="margin: 0; padding-left: 1.2rem">
        <li v-for="(err, i) in importResult.errors" :key="i">{{ err }}</li>
      </ul>
    </div>
  </section>

  <Dialog v-model:visible="catDialog" modal :header="editingCat ? 'Edit category' : 'New category'" :style="{ width: '26rem' }">
    <div class="col gap-3">
      <label class="col gap-1">Class <Select v-model="catForm.categoryClassId" :options="classOptions" option-label="name" option-value="id" fluid /></label>
      <label v-if="!editingCat" class="col gap-1">Code <InputText v-model="catForm.code" placeholder="GROCERIES" fluid /></label>
      <label class="col gap-1">Name <InputText v-model="catForm.name" fluid /></label>
      <label class="col gap-1">Kind <Select v-model="catForm.kind" :options="KINDS" fluid /></label>
      <label class="flex center gap-2">Colour <ColorPicker v-model="catForm.color" format="hex" />
        <Button v-if="catForm.color" label="clear" text size="small" @click="catForm.color = null" />
      </label>
      <template v-if="editingCat">
        <label class="col gap-1">Sort order <InputNumber v-model="catForm.sortOrder" show-buttons fluid /></label>
        <label class="flex center gap-2"><ToggleSwitch v-model="catForm.isActive" /> Active</label>
      </template>
    </div>
    <template #footer>
      <Button label="Cancel" text @click="catDialog = false" />
      <Button label="Save" :loading="catSaving" :disabled="!catForm.name.trim() || (!editingCat && !catForm.code.trim())" @click="saveCategory" />
    </template>
  </Dialog>

  <Dialog v-model:visible="classDialog" modal :header="editingClass ? 'Edit class' : 'New class'" :style="{ width: '24rem' }">
    <div class="col gap-3">
      <label class="col gap-1">Name <InputText v-model="classForm.name" fluid /></label>
      <label class="flex center gap-2">Colour <ColorPicker v-model="classForm.color" format="hex" />
        <Button v-if="classForm.color" label="clear" text size="small" @click="classForm.color = null" />
      </label>
      <label class="col gap-1">Sort order <InputNumber v-model="classForm.sortOrder" show-buttons fluid /></label>
    </div>
    <template #footer>
      <Button label="Cancel" text @click="classDialog = false" />
      <Button label="Save" :loading="classSaving" :disabled="!classForm.name.trim()" @click="saveClass" />
    </template>
  </Dialog>

  <GroupDialog v-model:visible="groupDialog" :group="editingGroup" />
</template>
