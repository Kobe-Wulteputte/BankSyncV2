<script setup lang="ts">
import Button from 'primevue/button'
import ColorPicker from 'primevue/colorpicker'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import { ref, watch } from 'vue'
import { createGroup, updateGroup } from '@/api/groups'
import type { GroupDto } from '@/api/types'
import { useLookupStore } from '@/stores/lookups'

const visible = defineModel<boolean>('visible', { required: true })
const props = defineProps<{ group?: GroupDto | null }>()
const emit = defineEmits<{ saved: [group: GroupDto] }>()

const lookups = useLookupStore()
const name = ref('')
const color = ref<string | null>(null) // hex without '#', as ColorPicker emits
const saving = ref(false)

watch(visible, (v) => {
  if (!v) return
  name.value = props.group?.name ?? ''
  color.value = props.group?.color?.replace('#', '') ?? null
})

async function save() {
  saving.value = true
  try {
    const body = { name: name.value.trim(), color: color.value ? `#${color.value}` : null }
    const saved = props.group ? await updateGroup(props.group.id, body) : await createGroup(body)
    await lookups.reloadGroups()
    emit('saved', saved)
    visible.value = false
  } catch {
    // toasted
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:visible="visible" modal :header="group ? 'Edit group' : 'Create group'" :style="{ width: '24rem' }">
    <div class="col gap-3">
      <InputText v-model="name" placeholder="Group name" fluid autofocus @keyup.enter="save" />
      <label class="flex center gap-2">Colour <ColorPicker v-model="color" format="hex" /></label>
    </div>
    <template #footer>
      <Button label="Cancel" text @click="visible = false" />
      <Button label="Save" :loading="saving" :disabled="!name.trim()" @click="save" />
    </template>
  </Dialog>
</template>
