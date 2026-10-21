import { authGuard } from '@auth0/auth0-vue'
import { createRouter, createWebHistory } from 'vue-router'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/expenses' },
    { path: '/expenses', name: 'expenses', component: () => import('@/views/ExpensesView.vue') },
    { path: '/income', name: 'income', component: () => import('@/views/IncomeView.vue') },
    { path: '/savings', name: 'savings', component: () => import('@/views/SavingsView.vue') },
    { path: '/cashflow', name: 'cashflow', component: () => import('@/views/CashFlowView.vue') },
    { path: '/transactions', name: 'transactions', component: () => import('@/views/TransactionsView.vue') },
    { path: '/banks', name: 'banks', component: () => import('@/views/BanksView.vue') },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
  ],
})

router.beforeEach(authGuard)
