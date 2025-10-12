<template>
  <view class="page">
    <view class="header glass-card">
      <view>
        <text class="title">联动记录</text>
        <text class="subtitle">查看蓝奏云链接抓取与分发日志</text>
      </view>
      <view class="header-actions">
        <SoftwarePicker />
        <button class="header-btn" @tap="createLink">新建联动</button>
      </view>
    </view>

    <DataTable
      title="蓝奏云记录"
      subtitle="包含链接、提取码及创建时间"
      :columns="columns"
      :rows="rows"
      :loading="loading"
    >
      <template #url="{ row }">
        <view class="link-url" @tap="copy(row.url)">{{ row.url }}</view>
      </template>
      <template #content="{ row }">
        <view class="link-content">{{ row.content || '—' }}</view>
      </template>
    </DataTable>
  </view>
</template>

<script setup lang="ts">
import { computed, onMounted, watch } from 'vue';
import { storeToRefs } from 'pinia';
import DataTable from '@/components/DataTable.vue';
import SoftwarePicker from '@/components/SoftwarePicker.vue';
import { useAppStore } from '@/stores/app';

const appStore = useAppStore();
const { linkRecords, selectedSoftware } = storeToRefs(appStore);

const columns = [
  { key: 'id', label: '编号', style: 'min-width:140rpx' },
  { key: 'url', label: '蓝奏云链接', style: 'min-width:260rpx' },
  { key: 'extractionCode', label: '提取码', style: 'min-width:140rpx' },
  { key: 'createdAt', label: '抓取时间', style: 'min-width:200rpx' },
  { key: 'content', label: '备注信息', style: 'min-width:240rpx' }
];

const rows = computed(() =>
  linkRecords.value.map((item) => ({
    id: item.id,
    url: item.url,
    extractionCode: item.extractionCode || '—',
    createdAt: item.createdAt,
    content: item.content || ''
  }))
);

const loading = computed(() => appStore.loading.links);

onMounted(async () => {
  await appStore.ensureReady();
  await appStore.loadLinkRecords();
});

watch(
  () => selectedSoftware.value,
  (next, prev) => {
    if (!next || next === prev) return;
    appStore.loadLinkRecords();
  }
);

function createLink() {
  uni.showToast({ title: '请接入联动创建接口', icon: 'none' });
}

function copy(text: string) {
  uni.setClipboardData({ data: text, success: () => uni.showToast({ title: '链接已复制', icon: 'success' }) });
}
</script>

<style scoped lang="scss">
.page {
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 40rpx 28rpx 80rpx;
}

.header {
  padding: 32rpx;
  display: flex;
  flex-direction: column;
  gap: 16rpx;

  @media (min-width: 768px) {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
  }
}

.title {
  font-size: 42rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.subtitle {
  font-size: 26rpx;
  color: var(--text-muted);
}

.header-actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 16rpx;
}

.header-btn {
  padding: 16rpx 30rpx;
  border-radius: 999rpx;
  background: var(--accent-gradient);
  color: #04101c;
  border: none;
  font-size: 26rpx;
  font-weight: 600;
}

.header-btn:disabled {
  opacity: 0.65;
}

.link-url {
  color: var(--text-primary);
  font-size: 24rpx;
  overflow: hidden;
  text-overflow: ellipsis;
}

.link-content {
  color: var(--text-muted);
  font-size: 24rpx;
}
</style>