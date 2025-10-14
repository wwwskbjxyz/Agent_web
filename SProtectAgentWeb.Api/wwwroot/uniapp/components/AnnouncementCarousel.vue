<template>
  <view class="carousel glass-card">
    <view class="carousel-header">
      <text class="carousel-title">运维公告</text>
      <view class="carousel-controls">
        <button class="nav-btn" @tap="prev">‹</button>
        <button class="nav-btn" @tap="next">›</button>
      </view>
    </view>
    <view class="carousel-body">
      <text class="announcement-title">{{ current.title }}</text>
      <text class="announcement-content">{{ current.content }}</text>
      <view class="announcement-meta">
        <text>{{ current.author }}</text>
        <text>·</text>
        <text>{{ current.date }}</text>
      </view>
    </view>
    <view class="carousel-indicator">
      <view
        v-for="item in items.length"
        :key="item"
        :class="['dot', { active: item - 1 === currentIndex } ]"
      ></view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, onUnmounted, ref, watchEffect } from 'vue';
import type { Announcement } from '../common/types';

const props = defineProps<{
  items: Announcement[];
  interval?: number;
}>();

const currentIndex = ref(0);
const timer = ref<number | null>(null);

const current = computed(() => props.items[currentIndex.value] ?? props.items[0]);

function play() {
  stop();
  if (!props.items.length) return;
  timer.value = setInterval(() => {
    next();
  }, props.interval ?? 6000) as unknown as number;
}

function stop() {
  if (timer.value) {
    clearInterval(timer.value);
    timer.value = null;
  }
}

function next() {
  currentIndex.value = (currentIndex.value + 1) % props.items.length;
}

function prev() {
  currentIndex.value = (currentIndex.value - 1 + props.items.length) % props.items.length;
}

watchEffect(() => {
  if (props.items.length) {
    play();
  }
});

onUnmounted(() => {
  stop();
});
</script>

<style scoped lang="scss">
.carousel {
  padding: 32rpx;
  display: flex;
  flex-direction: column;
  gap: 24rpx;
  position: relative;
}

.carousel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.carousel-title {
  font-size: 30rpx;
  color: #f1f5f9;
  font-weight: 600;
}

.carousel-controls {
  display: flex;
  gap: 16rpx;
}

.nav-btn {
  width: 64rpx;
  height: 64rpx;
  border-radius: 999rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.35);
  background: rgba(15, 23, 42, 0.6);
  color: #e2e8f0;
  font-size: 38rpx;
}

.carousel-body {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
}

.announcement-title {
  font-size: 32rpx;
  font-weight: 600;
  color: #f8fafc;
}

.announcement-content {
  font-size: 26rpx;
  color: rgba(226, 232, 240, 0.78);
  line-height: 1.6;
}

.announcement-meta {
  font-size: 22rpx;
  color: rgba(148, 163, 184, 0.75);
  display: flex;
  gap: 12rpx;
}

.carousel-indicator {
  display: flex;
  gap: 12rpx;
}

.dot {
  width: 18rpx;
  height: 18rpx;
  border-radius: 999rpx;
  background: rgba(148, 163, 184, 0.4);
  transition: all 0.2s ease;
}

.dot.active {
  width: 42rpx;
  background: rgba(56, 189, 248, 0.9);
}
</style>
