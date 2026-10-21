<script setup lang="ts">
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import DatePicker from 'primevue/datepicker'
import Dialog from 'primevue/dialog'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import MultiSelect from 'primevue/multiselect'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import { computed, ref, watch } from 'vue'
import { createTransaction, updateTransaction } from '@/api/transactions'
import type { TransactionDto, TransactionInputBody } from '@/api/types'
import { useLookupStore } from '@/stores/lookups'
import { parseDate, toIsoDate } from '@/utils/format'

/** Creates a manual entry when `transaction` is null, otherwise edits every field of it. */
const visible = defineModel<boolean>('visible', { required: true })
const props = defineProps<{ transaction?: TransactionDto | null }>()
const emit = defineEmits<{ saved: [transaction: TransactionDto] }>()

const lookups = useLookupStore()
const accountOptions = computed(() =>
  lookups.accounts.map((a) => ({ id: a.id, label: `${a.bankName} · ${a.displayName ?? a.identifier}` })),
)

const date = ref<Date | null>(null)
const accountId = ref<string | null>(null)
const amount = ref<number | null>(null)
const currency = ref('EUR')
const counterpartyName = ref('')
const counterpartyIban = ref('')
const description = ref('')
const categoryId = ref<number | null>(null)
const groupIds = ref<string[]>([])
const reimbursed = ref(false)
const notes = ref('')
const saving = ref(false)

watch(visible, (v) => {
  if (!v) return
  const t = props.transaction
  date.value = t ? parseDate(t.date) : new Date()
  accountId.value = t?.accountId ?? null
  amount.value = t?.amount ?? null
  currency.value = t?.currency ?? 'EUR'
  counterpartyName.value = t?.counterpartyName ?? ''
  counterpartyIban.value = t?.counterpartyIban ?? ''
  description.value = t?.description ?? ''
  categoryId.value = t?.categoryId ?? null
  groupIds.value = [...(t?.groupIds ?? [])]
  reimbursed.value = t?.reimbursed ?? false
  notes.value = t?.notes ?? ''
})

const valid = computed(() =>
  !!date.value && !!accountId.value && !!amount.value
  && (counterpartyName.value.trim() !== '' || description.value.trim() !== ''))

async function save() {
  if (!valid.value) return
  saving.value = true
  try {
    const body: TransactionInputBody = {
      date: toIsoDate(date.value!),
      accountId: accountId.value!,
      amount: amount.value!,
      currency: currency.value,
      counterpartyName: counterpartyName.value,
      counterpartyIban: counterpartyIban.value || null,
      description: description.value,
      categoryId: categoryId.value,
      groupIds: groupIds.value,
      reimbursed: reimbursed.value,
      notes: notes.value || null,
    }
    const saved = props.transaction ? await updateTransaction(props.transaction.id, body) : await createTransaction(body)
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
  <Dialog v-model:visible="visible" modal :header="transaction ? 'Edit transaction' : 'Add transaction'" :style="{ width: '34rem' }">
    <div class="form">
      <label>Date</label>
      <DatePicker v-model="date" date-format="dd/mm/yy" show-icon fluid />
      <label>Account</label>
      <Select v-model="accountId" :options="accountOptions" option-label="label" option-value="id" filter fluid placeholder="Account" />
      <label>Amount</label>
      <div class="flex gap-2">
        <InputNumber v-model="amount" mode="decimal" :min-fraction-digits="2" :max-fraction-digits="2" fluid
          placeholder="-12.50 for an expense" />
        <InputText v-model="currency" maxlength="3" style="width: 5rem" />
      </div>
      <label>Counterparty</label>
      <InputText v-model="counterpartyName" fluid />
      <label>IBAN</label>
      <InputText v-model="counterpartyIban" fluid placeholder="Optional" />
      <label>Description</label>
      <InputText v-model="description" fluid />
      <label>Category</label>
      <Select v-model="categoryId" :options="lookups.activeCategories" option-label="name" option-value="id"
        filter show-clear fluid placeholder="—" />
      <label>Groups</label>
      <MultiSelect v-model="groupIds" :options="lookups.groups" option-label="name" option-value="id"
        display="chip" fluid placeholder="—" />
      <label>Reimbursed</label>
      <Checkbox v-model="reimbursed" binary />
      <label>Notes</label>
      <Textarea v-model="notes" rows="2" auto-resize fluid />
    </div>
    <template #footer>
      <Button label="Cancel" text @click="visible = false" />
      <Button label="Save" :loading="saving" :disabled="!valid" @click="save" />
    </template>
  </Dialog>
</template>

<style scoped>
.form { display: grid; grid-template-columns: 7rem 1fr; gap: .75rem 1rem; align-items: center; }
</style>
