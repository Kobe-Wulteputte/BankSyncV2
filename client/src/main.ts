import Aura from '@primevue/themes/aura'
import { createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import ConfirmationService from 'primevue/confirmationservice'
import ToastService from 'primevue/toastservice'
import Tooltip from 'primevue/tooltip'
import { createApp } from 'vue'
import 'primeicons/primeicons.css'
import App from './App.vue'
import { auth0 } from './auth'
import { router } from './router'
import './style.css'

createApp(App)
  .use(createPinia())
  // Router before auth0: the plugin picks up $router at install time and uses it to navigate to
  // appState.target after the login callback. Installed the other way round it lands on '/'.
  .use(router)
  .use(auth0)
  .use(PrimeVue, { theme: { preset: Aura, options: { darkModeSelector: 'system' } } })
  .use(ToastService)
  .use(ConfirmationService)
  .directive('tooltip', Tooltip)
  .mount('#app')
