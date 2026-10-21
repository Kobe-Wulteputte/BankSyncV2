<script setup lang="ts">
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Tag from 'primevue/tag'
import { onMounted, ref } from 'vue'
import { classifyTransaction, listClassifications } from '@/api/transactions'
import type { ClassificationRunDto } from '@/api/types'
import { useAsync } from '@/composables'
import { useLookupStore } from '@/stores/lookups'
import { formatDateTime, formatPercent } from '@/utils/format'

const props = defineProps<{ transactionId: string }>()
const emit = defineEmits<{ reclassified: [] }>()

const lookups = useLookupStore()
const { data: runs, loading, run } = useAsync(() => listClassifications(props.transactionId))
const reclassifying = ref(false)

onMounted(run)

async function reclassify() {
  reclassifying.value = true
  try {
    const result: ClassificationRunDto = await classifyTransaction(props.transactionId, true)
    runs.value = [result, ...(runs.value ?? [])]
    emit('reclassified')
  } catch {
    // toasted by the api client
  } finally {
    reclassifying.value = false
  }
}

const categoryName = (id: number | null) => (id === null ? '—' : (lookups.categoryById.get(id)?.name ?? `#${id}`))
</script>

<template>
  <div class="col gap-2" style="padding: .5rem 1rem">
    <div class="flex center between">
      <strong>Classification history</strong>
      <Button label="Re-classify" icon="pi pi-sparkles" size="small" outlined :loading="reclassifying" @click="reclassify" />
    </div>
    <DataTable :value="runs ?? []" :loading="loading" size="small" data-key="id">
      <template #empty><span class="muted">Never classified.</span></template>
      <Column header="When"><template #body="{ data }">{{ formatDateTime(data.createdAt) }}</template></Column>
      <Column field="trigger" header="Trigger" />
      <Column field="model" header="Model" />
      <Column header="Predicted">
        <template #body="{ data }">
          {{ categoryName(data.predictedCategoryId) }}
          <span class="muted small">({{ data.predictedCategoryCode }})</span>
        </template>
      </Column>
      <Column header="Confidence">
        <template #body="{ data }">
          <Tag :value="formatPercent(data.confidence)" :severity="data.accepted ? 'success' : 'warn'" />
          <span class="muted small"> ≥ {{ formatPercent(data.threshold) }}</span>
        </template>
      </Column>
      <Column header="Accepted">
        <template #body="{ data }"><i :class="data.accepted ? 'pi pi-check pos' : 'pi pi-times neg'" /></template>
      </Column>
      <Column header="Alternatives">
        <template #body="{ data }">
          <div class="flex wrap gap-1">
            <Tag v-for="alt in data.alternatives" :key="alt.code" severity="secondary"
              :value="`${alt.code} ${formatPercent(alt.probability)}`" />
          </div>
        </template>
      </Column>
      <Column header="Latency"><template #body="{ data }">{{ data.latencyMs }} ms</template></Column>
      <Column header="Error"><template #body="{ data }"><span class="neg small">{{ data.error }}</span></template></Column>
    </DataTable>
  </div>
</template>
