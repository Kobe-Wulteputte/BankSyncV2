<script setup lang="ts">
import Button from 'primevue/button'
import Column from 'primevue/column'
import DataTable from 'primevue/datatable'
import Tag from 'primevue/tag'
import { onMounted, ref } from 'vue'
import { classifyTransaction, listClassifications } from '@/api/transactions'
import type { ClassificationRunDto } from '@/api/types'
import { useAsync, useMobile } from '@/composables'
import { useLookupStore } from '@/stores/lookups'
import { formatDateTime, formatPercent } from '@/utils/format'

const props = defineProps<{ transactionId: string }>()
const emit = defineEmits<{ reclassified: [] }>()

const lookups = useLookupStore()
const mobile = useMobile()
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
  <div class="col gap-2" :style="mobile ? undefined : 'padding: .5rem 1rem'">
    <div class="flex center between">
      <strong>Classification history</strong>
      <Button label="Re-classify" icon="pi pi-sparkles" size="small" outlined :loading="reclassifying" @click="reclassify" />
    </div>
    <div v-if="mobile" class="col gap-2">
      <span v-if="loading" class="muted small"><i class="pi pi-spin pi-spinner" /> Loading…</span>
      <span v-else-if="!runs?.length" class="muted small">Never classified.</span>
      <div v-for="r in runs" :key="r.id" class="run col gap-1 small">
        <div class="flex center between gap-2">
          <span>
            <i :class="r.accepted ? 'pi pi-check pos' : 'pi pi-times neg'" />
            {{ categoryName(r.predictedCategoryId) }} <span class="muted">({{ r.predictedCategoryCode }})</span>
          </span>
          <span class="nowrap">
            <Tag :value="formatPercent(r.confidence)" :severity="r.accepted ? 'success' : 'warn'" />
            <span class="muted"> ≥ {{ formatPercent(r.threshold) }}</span>
          </span>
        </div>
        <span class="muted">{{ formatDateTime(r.createdAt) }} · {{ r.trigger }} · {{ r.latencyMs }} ms</span>
        <span class="muted" style="overflow-wrap: anywhere">{{ r.model }}</span>
        <div v-if="r.alternatives.length" class="flex wrap gap-1">
          <Tag v-for="alt in r.alternatives" :key="alt.code" severity="secondary" :value="`${alt.code} ${formatPercent(alt.probability)}`" />
        </div>
        <span v-if="r.error" class="neg">{{ r.error }}</span>
      </div>
    </div>
    <DataTable v-else :value="runs ?? []" :loading="loading" size="small" data-key="id">
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

<style scoped>
.run { padding: .625rem .75rem; border-radius: var(--p-content-border-radius); background: var(--p-content-hover-background); }
</style>
