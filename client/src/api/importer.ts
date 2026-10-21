import { api } from './client'
import type { ImportResult } from './types'

export function importExcel(file: File) {
  const form = new FormData()
  form.append('file', file)
  return api<ImportResult>('/import/excel', { method: 'POST', form })
}

/** Admin only: sends a test mail to the address the expiry mails use. */
export const sendTestMail = () => api<{ recipient: string }>('/mail/test', { method: 'POST' })
