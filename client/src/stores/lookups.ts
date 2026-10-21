import { defineStore } from 'pinia'
import { listAccounts } from '@/api/accounts'
import { listCategories } from '@/api/categories'
import { listCategoryClasses } from '@/api/categoryClasses'
import { listGroups } from '@/api/groups'
import type { AccountDto, CategoryClassDto, CategoryDto, GroupDto } from '@/api/types'
import { classColor } from '@/utils/format'

/** Categories / groups / accounts, loaded once and shared by filters, tables and settings. */
export const useLookupStore = defineStore('lookups', {
  state: () => ({
    categories: [] as CategoryDto[],
    classes: [] as CategoryClassDto[],
    groups: [] as GroupDto[],
    accounts: [] as AccountDto[],
    loaded: false,
    loading: null as Promise<void> | null,
  }),
  getters: {
    activeCategories: (s) => s.categories.filter((c) => c.isActive).sort((a, b) => a.sortOrder - b.sortOrder),
    classById: (s) => new Map(s.classes.map((c) => [c.id, c])),
    /** Class colour; grey for null (Unclassed). */
    classColor: (s) => (id: number | null | undefined) => classColor(s.classes.find((c) => c.id === id) ?? { id: id ?? null }),
    categoryById: (s) => new Map(s.categories.map((c) => [c.id, c])),
    groupById: (s) => new Map(s.groups.map((g) => [g.id, g])),
    accountById: (s) => new Map(s.accounts.map((a) => [a.id, a])),
  },
  actions: {
    load(force = false) {
      if (this.loaded && !force) return Promise.resolve()
      if (this.loading && !force) return this.loading
      this.loading = Promise.all([listCategories(), listCategoryClasses(), listGroups(), listAccounts()])
        .then(([categories, classes, groups, accounts]) => {
          this.categories = categories
          this.classes = classes
          this.groups = groups
          this.accounts = accounts
          this.loaded = true
        })
        .finally(() => {
          this.loading = null
        })
      return this.loading
    },
    async reloadCategories() {
      this.categories = await listCategories()
    },
    async reloadClasses() {
      this.classes = await listCategoryClasses()
    },
    async reloadGroups() {
      this.groups = await listGroups()
    },
    async reloadAccounts() {
      this.accounts = await listAccounts()
    },
  },
})
