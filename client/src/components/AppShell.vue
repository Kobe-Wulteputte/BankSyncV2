<script setup lang="ts">
import { useAuth0 } from '@auth0/auth0-vue'
import Button from 'primevue/button'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import Drawer from 'primevue/drawer'
import SplitButton from 'primevue/splitbutton'
import { useToast } from 'primevue/usetoast'
import { computed, onUnmounted, ref } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { ApiError } from '@/api/client'
import { getSyncStatus, startSync } from '@/api/sync'
import type { SyncRunDto } from '@/api/types'
import { useFilterUrlSync, useMobile } from '@/composables'
import { useFilterStore } from '@/stores/filter'
import { useLookupStore } from '@/stores/lookups'
import { toIsoDate } from '@/utils/format'

const { user, logout } = useAuth0()
const toast = useToast()
const filter = useFilterStore()
const lookups = useLookupStore()
const route = useRoute()
const mobile = useMobile()
useFilterUrlSync()
void lookups.load()

// `tab`: shown in the phone's bottom bar under that short label; the rest sit behind "More".
const nav = [
  { name: 'expenses', label: 'Expenses', icon: 'pi pi-chart-pie', tab: 'Expenses' },
  { name: 'monthly', label: 'Monthly expenses', icon: 'pi pi-calendar', tab: 'Monthly' },
  { name: 'income', label: 'Income', icon: 'pi pi-chart-bar' },
  { name: 'yearly', label: 'Yearly', icon: 'pi pi-chart-line' },
  { name: 'savings', label: 'Savings', icon: 'pi pi-wallet', tab: 'Savings' },
  { name: 'cashflow', label: 'Cash flow', icon: 'pi pi-arrow-right-arrow-left' },
  { name: 'transactions', label: 'Transactions', icon: 'pi pi-list', tab: 'Transactions' },
  { name: 'banks', label: 'Banks', icon: 'pi pi-building-columns' },
  { name: 'settings', label: 'Settings', icon: 'pi pi-cog' },
]
const tabs = nav.filter((n) => n.tab)
const moreItems = nav.filter((n) => !n.tab)
const more = ref(false)
const moreActive = computed(() => moreItems.some((n) => n.name === route.name))
const navTo = (name: string) => ({ name, query: filter.toUrlQuery({ withSearch: false }) })

const syncing = ref(false)
const dataVersion = ref(0)
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
    // A sync adds transactions and categories; remount the open page so it shows them.
    void lookups.load(true)
    dataVersion.value++
  } catch {
    syncing.value = false // the api client already toasted
  }
}

async function syncNow(from?: string) {
  syncing.value = true
  try {
    await startSync(from)
  } catch (e) {
    if (!(e instanceof ApiError && e.status === 409)) {
      syncing.value = false
      return
    }
  }
  pollTimer = setTimeout(poll, 3000)
}

// For entries older than the default retrieval window; how far back works depends on the bank.
const fromDialog = ref(false)
const syncFrom = ref<Date | null>(null)
const today = new Date()
const syncMenu = [{ label: 'Sync from date…', icon: 'pi pi-calendar', command: () => (fromDialog.value = true) }]
function openFromDialog() {
  more.value = false
  fromDialog.value = true
}
function syncFromDate() {
  if (!syncFrom.value) return
  fromDialog.value = false
  void syncNow(toIsoDate(syncFrom.value))
}

onUnmounted(() => clearTimeout(pollTimer))

const doLogout = () => logout({ logoutParams: { returnTo: window.location.origin } })
</script>

<template>
  <div class="shell">
    <aside v-if="!mobile" class="sidebar">
      <div class="brand"><i class="pi pi-sync" /> BankSync</div>
      <nav class="col gap-1">
        <RouterLink v-for="item in nav" :key="item.name" :to="navTo(item.name)" class="nav-link" active-class="active">
          <i :class="item.icon" /> {{ item.label }}
        </RouterLink>
      </nav>
    </aside>
    <div class="col grow" style="min-width: 0">
      <header v-if="mobile" class="appbar">
        <div class="brand"><i class="pi pi-sync" /> BankSync</div>
        <Button :icon="`pi pi-refresh${syncing ? ' pi-spin' : ''}`" text rounded aria-label="Sync now" :disabled="syncing"
          @click="syncNow()" />
      </header>
      <header v-else class="topbar flex center between gap-2">
        <span class="muted small">{{ user?.email }}</span>
        <div class="flex gap-2">
          <SplitButton label="Sync now" icon="pi pi-refresh" size="small" :model="syncMenu" :disabled="syncing" @click="syncNow()" />
          <Button label="Logout" icon="pi pi-sign-out" size="small" severity="secondary" outlined @click="doLogout" />
        </div>
      </header>
      <Dialog v-model:visible="fromDialog" modal header="Sync from date" :style="{ width: '22rem' }">
        <p class="small muted mt-0">Fetches every bank from this date. Entries already present are skipped.</p>
        <DatePicker v-model="syncFrom" date-format="dd/mm/yy" :max-date="today" show-icon fluid />
        <template #footer>
          <Button label="Cancel" severity="secondary" text @click="fromDialog = false" />
          <Button label="Sync" icon="pi pi-refresh" :disabled="!syncFrom" @click="syncFromDate" />
        </template>
      </Dialog>
      <main class="content">
        <!-- Route name in the key: Income and Monthly expenses share one component that reads its kind once. -->
        <RouterView :key="`${String($route.name)}-${dataVersion}`" />
      </main>
      <nav v-if="mobile" class="tabbar" aria-label="Main">
        <RouterLink v-for="item in tabs" :key="item.name" :to="navTo(item.name)" class="tab" active-class="active">
          <span class="tab-icon"><i :class="item.icon" /></span>{{ item.tab }}
        </RouterLink>
        <button type="button" class="tab" :class="{ active: moreActive }" aria-haspopup="dialog" @click="more = true">
          <span class="tab-icon"><i class="pi pi-bars" /></span>More
        </button>
      </nav>
      <Drawer v-if="mobile" v-model:visible="more" position="bottom" header="More" class="sheet">
        <nav class="list">
          <RouterLink v-for="item in moreItems" :key="item.name" :to="navTo(item.name)" class="list-item tappable"
            active-class="active" @click="more = false">
            <i :class="item.icon" /> {{ item.label }}
          </RouterLink>
          <button type="button" class="list-item tappable" :disabled="syncing" @click="openFromDialog">
            <i class="pi pi-calendar" /> Sync from date…
          </button>
        </nav>
        <div class="flex center between gap-2 mt-3">
          <span class="muted small truncate">{{ user?.email }}</span>
          <Button label="Logout" icon="pi pi-sign-out" severity="secondary" outlined @click="doLogout" />
        </div>
      </Drawer>
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

/* Phone: app bar on top, the page scrolls between it and the bottom tab bar. */
.appbar {
  display: flex; align-items: center; justify-content: space-between;
  padding: calc(.25rem + env(safe-area-inset-top)) .5rem .25rem 1rem;
  background: var(--p-content-background); border-bottom: 1px solid var(--p-content-border-color);
}
.appbar .brand { padding: 0; }
.tabbar {
  display: flex; padding-bottom: env(safe-area-inset-bottom);
  background: var(--p-content-background); border-top: 1px solid var(--p-content-border-color);
}
.tab {
  flex: 1 1 0; min-width: 0; display: flex; flex-direction: column; align-items: center; gap: .2rem;
  padding: .5rem 0 .45rem; font: inherit; font-size: .7rem; font-weight: 500;
  color: var(--p-text-muted-color); background: none; border: 0; cursor: pointer;
}
.tab-icon { display: grid; place-items: center; width: 3.5rem; height: 1.9rem; border-radius: 1rem; transition: background-color .2s; }
.tab-icon .pi { font-size: 1.1rem; }
.tab.active { color: var(--p-highlight-color); }
.tab.active .tab-icon { background: var(--p-highlight-background); }
@media (max-width: 800px) {
  .shell { height: 100dvh; }
  .content { padding: 1rem; overscroll-behavior: contain; }
}
</style>
