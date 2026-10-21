import { api } from './client'
import type { CommonFilter, ExpensesAnalytics, MonthlyAnalytics, SavingsAnalytics, SummaryAnalytics } from './types'

export const getExpenses = (query: CommonFilter) => api<ExpensesAnalytics>('/analytics/expenses', { query: { ...query } })
export const getIncome = (query: CommonFilter) => api<MonthlyAnalytics>('/analytics/income', { query: { ...query } })
export const getMonthlyExpenses = (query: CommonFilter) =>
  api<MonthlyAnalytics>('/analytics/expenses/monthly', { query: { ...query } })
export const getSavings = (query: CommonFilter) => api<SavingsAnalytics>('/analytics/savings', { query: { ...query } })
export const getSummary = (query: CommonFilter) => api<SummaryAnalytics>('/analytics/summary', { query: { ...query } })
