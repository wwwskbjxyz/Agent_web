<template>
  <view v-if="options.length" class="software-picker">
    <picker mode="selector" :range="options" :value="currentIndex" @change="onChange">
      <view class="picker-display">
        <text class="picker-label">{{ currentLabel }}</text>
        <text class="picker-arrow">▼</text>
      </view>
    </picker>
  </view>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { storeToRefs } from 'pinia';
import { useAppStore } from '@/stores/app';

const appStore = useAppStore();
const { softwareList, selectedSoftware } = storeToRefs(appStore);

const options = computed(() => softwareList.value.map((item) => item.softwareName));

const currentIndex = computed(() => {
  const index = options.value.findIndex((item) => item === selectedSoftware.value);
  return index >= 0 ? index : 0;
});

const currentLabel = computed(() => selectedSoftware.value || options.value[currentIndex.value] || '请选择软件');

function onChange(event: any) {
  const index = Number(event?.detail?.value ?? currentIndex.value);
  const value = options.value[index];
  if (value) {
    appStore.setSelectedSoftware(value);
  }
}
</script>

<style scoped lang="scss">
.software-picker {
  align-self: stretch;
}

.picker-display {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12rpx 24rpx;
  border-radius: 999rpx;
  background: rgba(56, 189, 248, 0.12);
  border: 1rpx solid rgba(56, 189, 248, 0.35);
  color: #e2e8f0;
  font-size: 26rpx;
  min-width: 260rpx;
}

.picker-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.picker-arrow {
  font-size: 22rpx;
  margin-left: 12rpx;
  opacity: 0.8;
}
</style>
