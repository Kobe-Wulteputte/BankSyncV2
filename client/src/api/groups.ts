import { api } from './client'
import type { GroupBody, GroupDto, Guid } from './types'

export const listGroups = () => api<GroupDto[]>('/groups')
export const createGroup = (body: GroupBody) => api<GroupDto>('/groups', { method: 'POST', body })
export const updateGroup = (id: Guid, body: GroupBody) => api<GroupDto>(`/groups/${id}`, { method: 'PUT', body })
export const deleteGroup = (id: Guid) => api<void>(`/groups/${id}`, { method: 'DELETE' })
