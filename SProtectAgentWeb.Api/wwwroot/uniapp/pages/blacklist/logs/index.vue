<template>
  <view class="page">
    <view class="header glass-card">
      <view>
        <text class="title">黑名单日志</text>
        <text class="subtitle">查看近期待处理的黑名单触发记录</text>
      </view>
      <view class="form">
        <picker
          mode="selector"
          :range="limitOptions.map((item) => item.label)"
          :value="selectedLimitIndex"
          @change="onLimitChange"
        >
          <view class="picker-display">{{ limitOptions[selectedLimitIndex].label }}</view>
        </picker>
        <button class="btn ghost" :disabled="loading" @tap="refresh">刷新</button>
      </view>
    </view>

    <view class="list glass-card">
      <view v-if="loading" class="empty">数据加载中...</view>
      <view v-else-if="!logs.length" class="empty">当前软件暂无黑名单日志</view>
      <view v-else class="items">
        <view v-for="item in logs" :key="item.id" class="item glass-light">
          <view class="row">
            <text class="label">时间</text>
            <text class="value">{{ formatTimestamp(item.timestamp) }}</text>
          </view>
          <view class="row" v-if="item.software">
            <text class="label">软件码</text>
            <text class="value">{{ item.software }}</text>
          </view>
          <view class="row" v-if="item.account">
            <text class="label">账号</text>
            <text class="value">{{ item.account }}</text>
          </view>
          <view class="row" v-if="item.machineCode">
            <text class="label">机器码</text>
            <text class="value highlight">{{ item.machineCode }}</text>
          </view>
          <view class="row" v-if="item.ip">
            <text class="label">IP</text>
            <text class="value">{{ item.ip }}</text>
          </view>
          <view class="row" v-if="item.reason">
            <text class="label">原因</text>
            <text class="value">{{ item.reason }}</text>
          </view>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import dayjs from 'dayjs';
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import { useAppStore } from '@/stores/app';

const appStore = useAppStore();
const { blacklistLogs, selectedSoftware } = storeToRefs(appStore);

const loading = computed(() => appStore.loading.blacklistLogs);
const logs = computed(() => blacklistLogs.value);

const limitOptions = [
  { label: '最近 50 条', value: 50 },
  { label: '最近 100 条', value: 100 },
  { label: '最近 200 条', value: 200 },
  { label: '最近 500 条', value: 500 }
];

const selectedLimitIndex = ref(2);

function onLimitChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value) || 0;
  selectedLimitIndex.value = Math.min(Math.max(index, 0), limitOptions.length - 1);
  refresh();
}

function formatTimestamp(timestamp?: string) {
  if (!timestamp) return '-';
  const parsed = dayjs(timestamp);
  if (!parsed.isValid()) return timestamp;
  return parsed.format('YYYY-MM-DD HH:mm:ss');
}

async function load() {
  const limit = limitOptions[selectedLimitIndex.value]?.value ?? 200;
  await appStore.loadBlacklistLogs(limit);
  appStore.markBlacklistLogsSeen();
}

async function refresh() {
  if (loading.value) return;
  await load();
}

onMounted(async () => {
  await load();
});

onBeforeUnmount(() => {
  appStore.markBlacklistLogsSeen();
});

watch(
  () => selectedSoftware.value,
  async (next, prev) => {
    if (!next || next === prev) return;
    await load();
  }
);
</script>

<style scoped lang="scss">
.page {
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 36rpx 28rpx 72rpx;
}

.header {
  display: flex;
  flex-direction: column;
  gap: 24rpx;
  padding: 32rpx;
}

.title {
  font-size: 42rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.subtitle {
  font-size: 26rpx;
  color: var(--text-muted);
  margin-top: 6rpx;
}

.form {
  display: flex;
  flex-wrap: wrap;
  gap: 16rpx;
  align-items: center;
}

.picker-display {
  padding: 16rpx 24rpx;
  border-radius: 20rpx;
  background: var(--surface-light);
  border: 1rpx solid var(--outline-color);
  color: var(--text-primary);
  font-size: 26rpx;
}

.btn {
  padding: 16rpx 30rpx;
  border-radius: 999rpx;
  border: 1rpx solid var(--outline-color);
  background: transparent;
  color: var(--text-muted);
  font-size: 26rpx;
  font-weight: 600;
}

.btn.ghost {
  background: transparent;
}

.list {
  padding: 24rpx 20rpx;
  display: flex;
  flex-direction: column;
  gap: 20rpx;
}

.items {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
}

.item {
  padding: 28rpx;
  border-radius: 28rpx;
  display: flex;
  flex-direction: column;
  gap: 14rpx;
}

.row {
  display: flex;
  justify-content: space-between;
  gap: 24rpx;
}

.label {
  font-size: 26rpx;
  color: var(--text-muted);
}

.value {
  font-size: 28rpx;
  color: var(--text-primary);
  word-break: break-all;
}

.value.highlight {
  color: var(--accent-color);
}

.empty {
  text-align: center;
  padding: 60rpx 20rpx;
  color: var(--text-muted);
  font-size: 28rpx;
}
</style>
