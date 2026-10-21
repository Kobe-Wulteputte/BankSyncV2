import { api } from './client'
import type { AspspDto, AuthorizeResult, BankConnectionBody, BankConnectionDto, CompleteAuthorizationResult, Guid } from './types'

export const listAspsps = (country: string) => api<AspspDto[]>('/banks/aspsps', { query: { country } })
export const listConnections = () => api<BankConnectionDto[]>('/banks/connections')
export const createConnection = (body: BankConnectionBody) =>
  api<BankConnectionDto>('/banks/connections', { method: 'POST', body })
export const updateConnection = (id: Guid, body: BankConnectionBody) =>
  api<BankConnectionDto>(`/banks/connections/${id}`, { method: 'PUT', body })
export const deleteConnection = (id: Guid) => api<void>(`/banks/connections/${id}`, { method: 'DELETE' })
export const authorizeConnection = (id: Guid) =>
  api<AuthorizeResult>(`/banks/connections/${id}/authorize`, { method: 'POST' })
export const refreshConnection = (id: Guid) =>
  api<BankConnectionDto>(`/banks/connections/${id}/refresh`, { method: 'POST' })
export const completeAuthorization = (code: string, state: string) =>
  api<CompleteAuthorizationResult>('/banks/callback', { method: 'POST', body: { code, state } })
