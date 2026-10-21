<script setup lang="ts">
import Skeleton from 'primevue/skeleton'

defineProps<{ label: string; value: string; sub?: string; loading?: boolean; tone?: 'pos' | 'neg' }>()
</script>

<template>
  <div class="card col gap-1">
    <span class="muted small">{{ label }}</span>
    <Skeleton v-if="loading" height="1.8rem" width="60%" />
    <span v-else class="value" :class="tone">{{ value }}</span>
    <span v-if="sub && !loading" class="muted small">{{ sub }}</span>
  </div>
</template>

<style scoped>
.value { font-size: 1.5rem; font-weight: 600; }
</style>
