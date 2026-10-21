import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useFilterUrlSync } from '@/composables'

const Empty = defineComponent({ render: () => h('div') })

describe('useFilterUrlSync', () => {
  it('does not cancel a first navigation that is still waiting on a guard', async () => {
    localStorage.clear()
    const pinia = createPinia()
    setActivePinia(pinia)
    // The page is loaded at the bank callback's landing URL, as after the redirect.
    const history = createMemoryHistory()
    history.replace('/banks?bankCode=abc&bankState=def')
    const router = createRouter({
      history,
      routes: [
        { path: '/', redirect: '/expenses' },
        { path: '/expenses', component: Empty },
        { path: '/banks', component: Empty },
      ],
    })
    // Stands in for the Auth0 guard, which holds the first navigation until login state is known.
    let release!: () => void
    const held = new Promise<void>((resolve) => (release = resolve))
    router.beforeEach(() => held)

    mount(defineComponent({ setup: () => useFilterUrlSync(), render: () => h('div') }), {
      global: { plugins: [pinia, router] },
    })
    release()
    await router.isReady()
    await flushPromises()

    expect(router.currentRoute.value.path).toBe('/banks')
    expect(router.currentRoute.value.query.bankCode).toBe('abc')
    expect(router.currentRoute.value.query.from).toBeDefined()
  })
})
