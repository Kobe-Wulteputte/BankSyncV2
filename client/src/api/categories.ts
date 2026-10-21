import { api } from './client'
import type { CategoryDto, CreateCategoryBody, UpdateCategoryBody } from './types'

export const listCategories = () => api<CategoryDto[]>('/categories')
export const createCategory = (body: CreateCategoryBody) => api<CategoryDto>('/categories', { method: 'POST', body })
export const updateCategory = (id: number, body: UpdateCategoryBody) =>
  api<CategoryDto>(`/categories/${id}`, { method: 'PUT', body })
