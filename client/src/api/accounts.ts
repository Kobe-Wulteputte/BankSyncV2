import { api } from './client'
import type { AccountDto, Guid, UpdateAccountBody } from './types'

export const listAccounts = () => api<AccountDto[]>('/accounts')
export const updateAccount = (id: Guid, body: UpdateAccountBody) =>
  api<AccountDto>(`/accounts/${id}`, { method: 'PUT', body })
