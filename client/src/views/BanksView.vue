<script setup lang="ts">
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Skeleton from 'primevue/skeleton'
import Tag from 'primevue/tag'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { updateAccount } from '@/api/accounts'
import { authorizeConnection, completeAuthorization, deleteConnection, listConnections, refreshConnection } from '@/api/banks'
import { listSyncRuns } from '@/api/sync'
import type { AccountDto, BankConnectionDto, ConnectionStatus, SyncStatus } from '@/api/types'
import BankConnectionDialog from '@/components/BankConnectionDialog.vue'
import { useAsync } from '@/composables'
import { useLookupStore } from '@/stores/lookups'
import { daysUntil, formatDateTime } from '@/utils/format'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const confirm = useConfirm()
const lookups = useLookupStore()

const { data: connections, loading, run } = useAsync(listConnections)
const { data: runs, loading: runsLoading, run: loadRuns } = useAsync(() => listSyncRuns(5))

function showResult(ok: boolean, bank: string, message: string) {
  toast.add({
    severity: ok ? 'success' : 'error',
    summary: ok ? `${bank || 'Bank'} authorized` : `Authorization failed for ${bank || 'bank'}`,
    detail: message,
    life: 8000,
  })
}

onMounted(async () => {
  run()
  loadRuns()
  const { result, message, bankCode, bankState } = route.query
  if (result || bankCode) router.replace({ query: {} })
  if (result) showResult(result === 'ok', '', String(message ?? ''))
  if (typeof bankCode === 'string' && typeof bankState === 'string') {
    // The bank came back through the API callback; completing here ties the session to the signed-in user.
    const outcome = await completeAuthorization(bankCode, bankState)
    showResult(outcome.success, outcome.bank, outcome.message)
    run()
  }
})

const statusSeverity: Record<ConnectionStatus, 'success' | 'warn' | 'danger' | 'secondary'> = {
  Active: 'success',
  NotAuthorized: 'warn',
  Expired: 'danger',
  Revoked: 'secondary',
}
const runSeverity: Record<SyncStatus, 'success' | 'warn' | 'danger' | 'info'> = {
  Succeeded: 'success',
  PartiallySucceeded: 'warn',
  Failed: 'danger',
  Running: 'info',
}

const dialog = ref(false)
const editing = ref<BankConnectionDto | null>(null)
function openDialog(c: BankConnectionDto | null) {
  editing.value = c
  dialog.value = true
}

function replace(c: BankConnectionDto) {
  if (!connections.value) return
  const i = connections.value.findIndex((x) => x.id === c.id)
  if (i >= 0) connections.value[i] = c
  else connections.value.push(c)
  void lookups.reloadAccounts()
}

const busy = ref<string | null>(null)
async function authorize(c: BankConnectionDto) {
  busy.value = c.id
  try {
    const { url } = await authorizeConnection(c.id)
    window.location.href = url
  } catch {
    busy.value = null
  }
}
async function refresh(c: BankConnectionDto) {
  busy.value = c.id
  try {
    replace(await refreshConnection(c.id))
  } catch {
    // toasted
  } finally {
    busy.value = null
  }
}
function remove(c: BankConnectionDto) {
  confirm.require({
    header: `Delete ${c.bankName}?`,
    message: 'The bank session is revoked. Accounts and transactions already synced are kept.',
    icon: 'pi pi-exclamation-triangle',
    acceptProps: { severity: 'danger', label: 'Delete' },
    rejectProps: { text: true, label: 'Cancel' },
    accept: async () => {
      try {
        await deleteConnection(c.id)
        await run()
        void lookups.reloadAccounts()
      } catch {
        // toasted
      }
    },
  })
}

async function renameAccount(a: AccountDto, name: string) {
  const displayName = name.trim() || null
  if (displayName === a.displayName) return
  try {
    const updated = await updateAccount(a.id, { displayName, isActive: a.isActive })
    Object.assign(a, updated)
    void lookups.reloadAccounts()
  } catch {
    // toasted
  }
}

function expiry(c: BankConnectionDto): { text: string; warn: boolean } | null {
  if (!c.validUntil) return null
  const days = daysUntil(c.validUntil)
  if (days < 0) return { text: `expired ${formatDateTime(c.validUntil)}`, warn: true }
  return { text: `expires in ${days} day${days === 1 ? '' : 's'} (${formatDateTime(c.validUntil)})`, warn: days < 7 }
}
</script>

<template>
  <div class="flex center between mb-3">
    <h1 class="page-title" style="margin: 0">Banks</h1>
    <Button label="Add bank" icon="pi pi-plus" @click="openDialog(null)" />
  </div>

  <div v-if="loading" class="cards"><Skeleton v-for="i in 2" :key="i" height="12rem" /></div>
  <div v-else-if="!connections?.length" class="card empty">No banks connected yet. Add one to start syncing.</div>
  <div v-else class="cards">
    <div v-for="c in connections" :key="c.id" class="card col gap-2">
      <div class="flex center between">
        <div>
          <strong>{{ c.bankName }}</strong> <span class="muted small">{{ c.country }} · {{ c.provider }}</span>
        </div>
        <Tag :value="c.status" :severity="statusSeverity[c.status]" />
      </div>
      <div v-if="expiry(c)" class="small" :class="{ neg: expiry(c)!.warn }">
        <i v-if="expiry(c)!.warn" class="pi pi-exclamation-triangle" /> Consent {{ expiry(c)!.text }}
      </div>
      <div class="muted small">Last synced: {{ c.lastSyncedAt ? formatDateTime(c.lastSyncedAt) : 'never' }}</div>
      <Message v-if="c.lastSyncError" severity="error" size="small" :closable="false">{{ c.lastSyncError }}</Message>
      <div v-if="c.configuredIbans.length" class="muted small">IBANs: {{ c.configuredIbans.join(', ') }}</div>

      <div v-if="c.accounts.length" class="col gap-1">
        <span class="small muted">Accounts</span>
        <div v-for="a in c.accounts" :key="a.id" class="flex center gap-2 small">
          <span class="muted nowrap" style="min-width: 10rem">{{ a.identifier }}</span>
          <InputText :model-value="a.displayName ?? ''" placeholder="Display name" size="small" class="grow"
            @blur="renameAccount(a, ($event.target as HTMLInputElement).value)"
            @keyup.enter="($event.target as HTMLInputElement).blur()" />
          <span class="muted nowrap">{{ a.transactionCount }} tx</span>
        </div>
      </div>
      <div v-else class="muted small">No accounts yet.</div>

      <div class="flex wrap gap-2 mt-3">
        <Button :label="c.status === 'Active' ? 'Re-authorize' : 'Authorize'" icon="pi pi-lock-open" size="small"
          :loading="busy === c.id" @click="authorize(c)" />
        <Button label="Refresh" icon="pi pi-refresh" size="small" severity="secondary" :loading="busy === c.id" @click="refresh(c)" />
        <Button label="Edit" icon="pi pi-pencil" size="small" severity="secondary" outlined @click="openDialog(c)" />
        <Button label="Delete" icon="pi pi-trash" size="small" severity="danger" outlined @click="remove(c)" />
      </div>
    </div>
  </div>

  <h2 class="page-title mt-3" style="font-size: 1.1rem">Last sync runs</h2>
  <div class="card col gap-2">
    <Skeleton v-if="runsLoading" height="4rem" />
    <div v-else-if="!runs?.length" class="empty">No sync runs yet.</div>
    <div v-for="r in runs" :key="r.id" class="col gap-1 run">
      <div class="flex center gap-2 small">
        <Tag :value="r.status" :severity="runSeverity[r.status]" />
        <span>{{ formatDateTime(r.startedAt) }}</span>
        <span class="muted">{{ r.trigger }}</span>
        <span class="muted">· {{ r.connectionsSynced }} synced, {{ r.connectionsSkipped }} skipped · {{ r.transactionsNew }} new / {{ r.transactionsFetched }} fetched · {{ r.transactionsClassified }} classified</span>
      </div>
      <div v-if="r.error" class="neg small">{{ r.error }}</div>
      <div v-for="d in r.details" :key="d.bankName" class="muted small" style="padding-left: 1rem">
        {{ d.bankName }}: {{ d.status }} — {{ d.new }} new / {{ d.fetched }} fetched <span v-if="d.message">· {{ d.message }}</span>
      </div>
    </div>
  </div>

  <BankConnectionDialog v-model:visible="dialog" :connection="editing" @saved="replace" />
</template>

<style scoped>
.cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(24rem, 1fr)); gap: 1rem; }
.run + .run { border-top: 1px solid var(--p-content-border-color); padding-top: .5rem; }
</style>
