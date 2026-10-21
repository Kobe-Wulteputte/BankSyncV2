import { api } from './client'
import type { MeDto } from './types'

export const getMe = () => api<MeDto>('/me')
