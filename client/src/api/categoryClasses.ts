import { api } from './client'
import type { CategoryClassBody, CategoryClassDto } from './types'

export const listCategoryClasses = () => api<CategoryClassDto[]>('/category-classes')
export const createCategoryClass = (body: CategoryClassBody) =>
  api<CategoryClassDto>('/category-classes', { method: 'POST', body })
export const updateCategoryClass = (id: number, body: CategoryClassBody) =>
  api<CategoryClassDto>(`/category-classes/${id}`, { method: 'PUT', body })
export const removeCategoryClass = (id: number) => api<void>(`/category-classes/${id}`, { method: 'DELETE' })
