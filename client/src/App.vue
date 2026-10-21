<script setup lang="ts">
import { useAuth0 } from '@auth0/auth0-vue'
import ConfirmDialog from 'primevue/confirmdialog'
import ProgressSpinner from 'primevue/progressspinner'
import Toast from 'primevue/toast'
import { useToast } from 'primevue/usetoast'
import { setApiErrorHandler } from '@/api/client'
import AppShell from '@/components/AppShell.vue'

const { isLoading, isAuthenticated } = useAuth0()
const toast = useToast()

setApiErrorHandler((e) => {
  const fieldErrors = e.errors ? Object.values(e.errors).flat().join(' ') : ''
  toast.add({ severity: 'error', summary: `${e.status} ${e.title}`, detail: e.detail ?? fieldErrors, life: 6000 })
})
</script>

<template>
  <Toast position="bottom-right" />
  <ConfirmDialog />
  <div v-if="isLoading" class="flex center" style="justify-content: center; height: 100%">
    <ProgressSpinner />
  </div>
  <AppShell v-else-if="isAuthenticated" />
</template>
