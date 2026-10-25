<script setup lang="ts">
import Button from 'primevue/button'
import Message from 'primevue/message'
import Textarea from 'primevue/textarea'
import { useToast } from 'primevue/usetoast'
import { onMounted, onUnmounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { importEdenred } from '@/api/banks'
import type { EdenredImportResult } from '@/api/types'
import { useLookupStore } from '@/stores/lookups'
import { ACK, bookmarkletHref, isEdenredMessage } from '@/utils/edenredBookmarklet'

const toast = useToast()
const lookups = useLookupStore()
const href = bookmarkletHref(window.location.origin)

const busy = ref(false)
const result = ref<EdenredImportResult | null>(null)
const pasted = ref('')
let received = false

async function run(json: string) {
  let operations: unknown
  try {
    operations = JSON.parse(json)
  } catch {
    toast.add({ severity: 'error', summary: 'Not valid JSON', life: 6000 })
    return
  }
  busy.value = true
  try {
    result.value = await importEdenred(operations)
    void lookups.reloadAccounts()
  } catch {
    // toasted
  } finally {
    busy.value = false
  }
}

// The bookmarklet resends until acknowledged; only the first copy is imported.
function onMessage(e: MessageEvent) {
  if (!isEdenredMessage(e)) return
  ;(e.source as Window | null)?.postMessage({ type: ACK }, e.origin)
  if (received) return
  received = true
  void run(e.data.payload)
}

onMounted(() => window.addEventListener('message', onMessage))
onUnmounted(() => window.removeEventListener('message', onMessage))
</script>

<template>
  <h1 class="page-title">Edenred</h1>

  <Message v-if="busy" severity="info" :closable="false">Importing Edenred transactions…</Message>
  <Message v-else-if="result" severity="success" :closable="false">
    {{ result.new }} new of {{ result.fetched }} fetched, {{ result.classified }} classified.
    <RouterLink :to="{ name: 'transactions' }">View transactions</RouterLink>
  </Message>

  <div class="card col gap-2 mt-3">
    <strong>Import with the bookmarklet</strong>
    <ol class="small steps">
      <li>
        Drag this link to your bookmarks bar (once):
        <a :href="href" class="bookmarklet" @click.prevent>BankSync Edenred</a>
      </li>
      <li>Open <a href="https://myedenred.be" target="_blank" rel="noopener">myedenred.be</a> and log in.</li>
      <li>Click the bookmark, then open your transactions page in myEdenred.</li>
      <li>Click <em>Send to BankSync</em> in the bar that appears. This page opens and imports them.</li>
    </ol>
    <span class="muted small">Your myEdenred login stays in that tab; only the transaction list is sent here.</span>
  </div>

  <div class="card col gap-2 mt-3">
    <strong>Or paste the JSON</strong>
    <span class="muted small">From the bar's <em>Copy JSON</em> button, or the <code>operations</code> response in the browser's developer tools.</span>
    <Textarea v-model="pasted" rows="6" fluid placeholder='{"data":[...]}' />
    <div>
      <Button label="Import" icon="pi pi-upload" :disabled="!pasted.trim()" :loading="busy" @click="run(pasted)" />
    </div>
  </div>
</template>

<style scoped>
.steps { margin: 0; padding-left: 1.25rem; line-height: 1.8; }
.bookmarklet {
  display: inline-block; padding: .1rem .6rem; border-radius: var(--p-content-border-radius);
  background: var(--p-highlight-background); color: var(--p-highlight-color); cursor: grab;
}
</style>
