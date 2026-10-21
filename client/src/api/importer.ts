import { api } from './client'
import type { ImportResult } from './types'

export function importExcel(file: File) {
  const form = new FormData()
  form.append('file', file)
  return api<ImportResult>('/import/excel', { method: 'POST', form })
}
