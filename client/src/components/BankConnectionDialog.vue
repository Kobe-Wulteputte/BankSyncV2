<script setup lang="ts">
import AutoComplete from 'primevue/autocomplete'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import { computed, ref, watch } from 'vue'
import { createConnection, listAspsps, updateConnection } from '@/api/banks'
import type { AspspDto, BankConnectionDto } from '@/api/types'

const COUNTRIES = ['BE', 'NL', 'FR', 'DE', 'LU', 'ES', 'IT', 'AT', 'PT', 'IE', 'FI', 'SE', 'DK', 'NO', 'PL']
const MAX_CONSENT_DAYS = 90

const visible = defineModel<boolean>('visible', { required: true })
const props = defineProps<{ connection?: BankConnectionDto | null }>()
const emit = defineEmits<{ saved: [connection: BankConnectionDto] }>()

const country = ref('BE')
const aspsps = ref<AspspDto[]>([])
const loadingAspsps = ref(false)
const bankName = ref<string | null>(null)
const psuType = ref<string | null>(null)
const ibans = ref<string[]>([])
const selectAccountsAtBank = ref(false)
const consentDays = ref<number | null>(null)
const saving = ref(false)

const bank = computed(() => aspsps.value.find((a) => a.name === bankName.value) ?? null)
const maxDays = computed(() => Math.min(bank.value?.maxConsentValidityDays ?? MAX_CONSENT_DAYS, MAX_CONSENT_DAYS))

async function loadAspsps() {
  loadingAspsps.value = true
  try {
    aspsps.value = await listAspsps(country.value)
  } catch {
    aspsps.value = []
  } finally {
    loadingAspsps.value = false
  }
}

watch(visible, (v) => {
  if (!v) return
  const c = props.connection
  country.value = c?.country ?? 'BE'
  bankName.value = c?.bankName ?? null
  psuType.value = c?.psuType ?? null
  ibans.value = [...(c?.configuredIbans ?? [])]
  selectAccountsAtBank.value = c?.selectAccountsAtBank ?? false
  consentDays.value = c?.consentValidityDays ?? null
  void loadAspsps()
})
watch(country, () => {
  bankName.value = null
  void loadAspsps()
})
watch(bank, (b) => {
  if (!b) return
  if (!psuType.value || !b.psuTypes.includes(psuType.value)) psuType.value = b.psuTypes[0] ?? null
  if (!consentDays.value || consentDays.value > maxDays.value) consentDays.value = maxDays.value
})

async function save() {
  if (!bankName.value) return
  saving.value = true
  try {
    const body = {
      bankName: bankName.value,
      country: country.value,
      psuType: psuType.value,
      selectAccountsAtBank: selectAccountsAtBank.value,
      consentValidityDays: consentDays.value,
      ibans: ibans.value.map((i) => i.replace(/\s/g, '').toUpperCase()).filter(Boolean),
    }
    const saved = props.connection ? await updateConnection(props.connection.id, body) : await createConnection(body)
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
  <Dialog v-model:visible="visible" modal :header="connection ? 'Edit bank' : 'Add bank'" :style="{ width: '30rem' }">
    <div class="col gap-3">
      <label class="col gap-1">Country
        <Select v-model="country" :options="COUNTRIES" fluid />
      </label>
      <label class="col gap-1">Bank
        <Select v-model="bankName" :options="aspsps" option-label="name" option-value="name" filter fluid
          :loading="loadingAspsps" placeholder="Search a bank">
          <template #option="{ option }">
            <div class="flex center gap-2">
              <img v-if="option.logoUrl" :src="option.logoUrl" alt="" style="width: 1.5rem; height: 1.5rem; object-fit: contain" />
              {{ option.name }}
            </div>
          </template>
        </Select>
      </label>
      <label v-if="bank && bank.psuTypes.length > 1" class="col gap-1">User type
        <Select v-model="psuType" :options="bank.psuTypes" fluid />
      </label>
      <label class="col gap-1">IBANs <span class="muted small">(leave empty to sync all accounts; press enter after each)</span>
        <AutoComplete v-model="ibans" multiple :typeahead="false" fluid placeholder="BE68 5390 0754 7034" />
      </label>
      <label v-if="bank?.supportsAccountPreselection !== false" class="flex center gap-2">
        <Checkbox v-model="selectAccountsAtBank" binary /> Select accounts at the bank
      </label>
      <label class="col gap-1">Consent validity (days, max {{ maxDays }})
        <InputNumber v-model="consentDays" :min="1" :max="maxDays" show-buttons fluid />
      </label>
    </div>
    <template #footer>
      <Button label="Cancel" text @click="visible = false" />
      <Button label="Save" :loading="saving" :disabled="!bankName" @click="save" />
    </template>
  </Dialog>
</template>
