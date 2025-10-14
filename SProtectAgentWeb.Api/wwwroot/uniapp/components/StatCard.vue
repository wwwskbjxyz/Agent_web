<template>
  <view class="stat-card glass-card">
    <view class="stat-title">{{ title }}</view>
    <view class="stat-value">{{ value }}</view>
    <view class="stat-footer">
      <view :class="['delta', deltaClass]">
        <text v-if="trend === 'up'">▲</text>
        <text v-else-if="trend === 'down'">▼</text>
        <text v-else>◼</text>
        <text class="delta-value">{{ deltaValue }}</text>
      </view>
      <text class="delta-label">较昨日</text>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  title: string;
  value: string;
  delta: number;
  trend: 'up' | 'down' | 'flat';
}>();

const deltaClass = computed(() => {
  if (props.trend === 'up') return 'delta-up';
  if (props.trend === 'down') return 'delta-down';
  return 'delta-flat';
});

const deltaValue = computed(() => `${Math.abs(props.delta)}%`);
</script>

<style scoped lang="scss">
.stat-card {
  padding: 32rpx 36rpx;
  display: flex;
  flex-direction: column;
  gap: 18rpx;
  min-width: 280rpx;
}

.stat-title {
  font-size: 26rpx;
  color: rgba(226, 232, 240, 0.78);
  letter-spacing: 0.04em;
}

.stat-value {
  font-size: 52rpx;
  font-weight: 600;
  color: #f8fafc;
}

.stat-footer {
  display: flex;
  align-items: center;
  gap: 16rpx;
  font-size: 24rpx;
}

.delta {
  display: inline-flex;
  align-items: center;
  gap: 8rpx;
  padding: 6rpx 12rpx;
  border-radius: 999rpx;
  font-size: 22rpx;
}

.delta-up {
  color: #22d3ee;
  background: rgba(14, 165, 233, 0.12);
}

.delta-down {
  color: #f87171;
  background: rgba(248, 113, 113, 0.12);
}

.delta-flat {
  color: #94a3b8;
  background: rgba(148, 163, 184, 0.15);
}

.delta-value {
  margin-left: 6rpx;
}

.delta-label {
  color: rgba(148, 163, 184, 0.75);
}
</style>
