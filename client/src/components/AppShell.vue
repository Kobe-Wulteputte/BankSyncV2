<script setup lang="ts">
import { useAuth0 } from '@auth0/auth0-vue'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import { onUnmounted, ref } from 'vue'
import { RouterLink, RouterView } from 'vue-router'
import { ApiError } from '@/api/client'
import { getSyncStatus, startSync } from '@/api/sync'
import type { SyncRunDto } from '@/api/types'
import { useFilterUrlSync } from '@/composables'
import { useFilterStore } from '@/stores/filter'
import { useLookupStore } from '@/stores/lookups'

const { user, logout } = useAuth0()
const toast = useToast()
const filter = useFilterStore()
const lookups = useLookupStore()
useFilterUrlSync()
void lookups.load()

const nav = [
  { name: 'expenses', label: 'Expenses', icon: 'pi pi-chart-pie' },
  { name: 'income', label: 'Income', icon: 'pi pi-chart-bar' },
  { name: 'savings', label: 'Savings', icon: 'pi pi-wallet' },
  { name: 'cashflow', label: 'Cash flow', icon: 'pi pi-arrow-right-arrow-left' },
  { name: 'transactions', label: 'Transactions', icon: 'pi pi-list' },
  { name: 'banks', label: 'Banks', icon: 'pi pi-building-columns' },
  { name: 'settings', label: 'Settings', icon: 'pi pi-cog' },
]

const syncing = ref(false)
let pollTimer: ReturnType<typeof setTimeout> | undefined

function describe(run: SyncRunDto): string {
  return `${run.connectionsSynced} bank(s), ${run.transactionsNew} new / ${run.transactionsFetched} fetched, ${run.transactionsClassified} classified`
}

async function poll() {
  try {
    const status = await getSyncStatus()
    if (status.running) {
      pollTimer = setTimeout(poll, 3000)
      return
    }
    syncing.value = false
    const run = status.lastRun
    if (!run) return
    const severity = run.status === 'Succeeded' ? 'success' : run.status === 'Failed' ? 'error' : 'warn'
    toast.add({ severity, summary: `Sync ${run.status}`, detail: run.error ?? describe(run), life: 8000 })
  } catch {
    syncing.value = false // the api client already toasted
  }
}

async function syncNow() {
  syncing.value = true
  try {
    await startSync()
  } catch (e) {
    if (!(e instanceof ApiError && e.status === 409)) {
      syncing.value = false
      return
    }
  }
  pollTimer = setTimeout(poll, 3000)
}

onUnmounted(() => clearTimeout(pollTimer))

const doLogout = () => logout({ logoutParams: { returnTo: window.location.origin } })
</script>

<template>
  <div class="shell">
    <aside class="sidebar">
      <div class="brand"><i class="pi pi-sync" /> BankSync</div>
      <nav class="col gap-1">
        <RouterLink
          v-for="item in nav"
          :key="item.name"
          :to="{ name: item.name, query: filter.toUrlQuery() }"
          class="nav-link"
          active-class="active"
        >
          <i :class="item.icon" /> {{ item.label }}
        </RouterLink>
      </nav>
    </aside>
    <div class="col grow" style="min-width: 0">
      <header class="topbar flex center between gap-2">
        <span class="muted small">{{ user?.email }}</span>
        <div class="flex gap-2">
          <Button label="Sync now" icon="pi pi-refresh" size="small" :loading="syncing" @click="syncNow" />
          <Button label="Logout" icon="pi pi-sign-out" size="small" severity="secondary" outlined @click="doLogout" />
        </div>
      </header>
      <main class="content">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<style scoped>
.shell { display: flex; height: 100%; }
.sidebar {
  width: 14rem; flex: 0 0 auto; padding: 1rem .75rem;
  background: var(--p-content-background); border-right: 1px solid var(--p-content-border-color);
}
.brand { font-weight: 700; font-size: 1.1rem; padding: .5rem .75rem 1.25rem; display: flex; gap: .5rem; align-items: center; }
.nav-link {
  display: flex; align-items: center; gap: .6rem; padding: .55rem .75rem; border-radius: var(--p-content-border-radius);
  color: var(--p-text-muted-color);
}
.nav-link:hover { background: var(--p-content-hover-background); color: var(--p-text-color); }
.nav-link.active { background: var(--p-highlight-background); color: var(--p-highlight-color); }
.topbar { padding: .6rem 1.5rem; border-bottom: 1px solid var(--p-content-border-color); background: var(--p-content-background); }
.content { padding: 1.5rem; overflow: auto; flex: 1; }
@media (max-width: 800px) {
  .shell { flex-direction: column; }
  .sidebar { width: auto; border-right: 0; border-bottom: 1px solid var(--p-content-border-color); }
  .sidebar nav { flex-direction: row; flex-wrap: wrap; }
}
</style>
