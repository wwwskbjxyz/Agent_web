<template>
  <view class="page">
    <view class="header glass-card">
      <view>
        <text class="title">黑名单机器码</text>
        <text class="subtitle">维护需要封禁的机器码或 IP 列表</text>
      </view>
      <view class="form">
        <input class="input" placeholder="输入机器码或唯一值" v-model="form.value" />
        <picker mode="selector" :range="typeOptions.map((item) => item.label)" :value="form.typeIndex" @change="onTypeChange">
          <view class="picker-display">{{ typeOptions[form.typeIndex].label }}</view>
        </picker>
        <input class="input" placeholder="备注（可选）" v-model="form.remark" />
        <button class="btn primary" :disabled="!form.value.trim() || submitting" @tap="create">{{ submitting ? '提交中...' : '加入黑名单' }}</button>
        <button class="btn ghost" :disabled="loading" @tap="refresh">刷新列表</button>
      </view>
    </view>

    <view class="list glass-card">
      <view v-if="loading" class="empty">数据加载中...</view>
      <view v-else-if="!machines.length" class="empty">当前软件暂无黑名单记录</view>
      <view v-else class="items">
        <view v-for="item in machines" :key="item.value" class="item glass-light">
          <view class="row">
            <text class="label">值</text>
            <text class="value highlight">{{ item.value }}</text>
          </view>
          <view class="row">
            <text class="label">类型</text>
            <text class="value">{{ formatType(item.type) }}</text>
          </view>
          <view class="row">
            <text class="label">备注</text>
            <text class="value">{{ item.remarks || '-' }}</text>
          </view>
          <button class="btn danger" @tap="remove(item.value)">删除</button>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch, onMounted } from 'vue';
import { storeToRefs } from 'pinia';
import { useAppStore } from '@/stores/app';

const appStore = useAppStore();
const { blacklistMachines, selectedSoftware } = storeToRefs(appStore);

const loading = computed(() => appStore.loading.blacklistMachines);
const machines = computed(() => blacklistMachines.value);
const submitting = ref(false);

const typeOptions = [
  { label: '机器码 (2)', value: 2 },
  { label: 'IP (1)', value: 1 },
  { label: '其他 (0)', value: 0 }
];

const form = reactive({
  value: '',
  typeIndex: 0,
  remark: ''
});

function onTypeChange(event: UniApp.PickerChangeEvent) {
  form.typeIndex = Number(event.detail.value) || 0;
}

async function load() {
  await appStore.loadBlacklistMachines();
}

async function refresh() {
  await load();
}

function formatType(type: number) {
  const match = typeOptions.find((item) => item.value === type);
  if (match) return match.label.replace(/ \(\d+\)/, '');
  return '其他';
}

async function create() {
  if (!form.value.trim()) {
    uni.showToast({ title: '请输入机器码', icon: 'none' });
    return;
  }
  submitting.value = true;
  try {
    await appStore.createBlacklistMachine({
      value: form.value.trim(),
      type: typeOptions[form.typeIndex]?.value ?? 2,
      remarks: form.remark.trim() || undefined
    });
    form.value = '';
    form.remark = '';
    uni.showToast({ title: '已加入黑名单', icon: 'success' });
  } catch (error) {
    console.error('create blacklist machine error', error);
    uni.showToast({ title: '操作失败', icon: 'none' });
  } finally {
    submitting.value = false;
  }
}

async function remove(value: string) {
  if (!value) return;
  try {
    await appStore.deleteBlacklistMachines([value]);
    uni.showToast({ title: '已删除', icon: 'success' });
  } catch (error) {
    console.error('delete blacklist machine error', error);
    uni.showToast({ title: '删除失败', icon: 'none' });
  }
}

onMounted(async () => {
  await appStore.ensureReady();
  await load();
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
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220rpx, 1fr));
  gap: 16rpx;
  align-items: center;
}

.input {
  padding: 16rpx 24rpx;
  border-radius: 20rpx;
  background: var(--surface-light);
  border: 1rpx solid var(--outline-color);
  color: var(--text-primary);
  font-size: 26rpx;
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

.btn.primary {
  background: var(--accent-gradient);
  color: #04101c;
  border: none;
}

.btn.ghost {
  border-color: var(--outline-color);
}

.btn:disabled {
  opacity: 0.6;
}

.list {
  display: flex;
  flex-direction: column;
  gap: 18rpx;
}

.empty {
  padding: 28rpx 24rpx;
  text-align: center;
  color: var(--text-muted);
  font-size: 24rpx;
}

.items {
  display: flex;
  flex-direction: column;
  gap: 18rpx;
}

.item {
  display: flex;
  flex-direction: column;
  gap: 14rpx;
  padding: 18rpx 22rpx;
}

.row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16rpx;
}

.label {
  font-size: 24rpx;
  color: var(--text-muted);
}

.value {
  font-size: 24rpx;
  color: var(--text-primary);
  word-break: break-all;
  text-align: right;
}

.value.highlight {
  font-weight: 600;
}
</style>