<template>
  <view :class="['status-tag', variantClass]">{{ label }}</view>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  status: 'success' | 'info' | 'warning' | 'error';
  label: string;
}>();

const variantClass = computed(() => {
  switch (props.status) {
    case 'success':
      return 'status-success';
    case 'warning':
      return 'status-warning';
    case 'error':
      return 'status-error';
    default:
      return 'status-info';
  }
});
</script>

<style scoped lang="scss">
.status-tag {
  padding: 8rpx 20rpx;
  border-radius: 999rpx;
  font-size: 22rpx;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8rpx;
}

.status-success {
  color: #34d399;
  background: rgba(16, 185, 129, 0.18);
  border: 1rpx solid rgba(16, 185, 129, 0.4);
}

.status-info {
  color: #38bdf8;
  background: rgba(56, 189, 248, 0.15);
  border: 1rpx solid rgba(56, 189, 248, 0.4);
}

.status-warning {
  color: #fbbf24;
  background: rgba(251, 191, 36, 0.16);
  border: 1rpx solid rgba(251, 191, 36, 0.35);
}

.status-error {
  color: #f87171;
  background: rgba(248, 113, 113, 0.16);
  border: 1rpx solid rgba(248, 113, 113, 0.4);
}
</style>
