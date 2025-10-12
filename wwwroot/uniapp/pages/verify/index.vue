<template>
  <view class="page">
    <view class="header glass-card">
      <view>
        <text class="title">卡密验证与资源下载</text>
        <text class="subtitle">输入卡密后自动校验授权状态并生成下载链接</text>
      </view>
    </view>

    <view class="verify-card glass-card">
      <view class="form">
        <text class="form-label">卡密</text>
        <input
          v-model="code"
          class="form-input"
          placeholder="请输入需要验证的卡密编号"
          confirm-type="done"
        />
        <button class="form-submit" :disabled="loading" @tap="handleVerify">
          {{ loading ? '验证中...' : '立即验证' }}
        </button>
      </view>

      <view v-if="payload" class="result">
        <view :class="['status-banner', statusStyle.statusClass]">
          <text class="status-title">{{ statusStyle.title }}</text>
          <text class="status-message">{{ payload.message }}</text>
        </view>

        <view v-if="payload.stats" class="stats">
          <view class="stat glass-light">
            <text class="stat-label">验证次数</text>
            <text class="stat-value">{{ payload.stats.attemptNumber }}</text>
          </view>
          <view class="stat glass-light">
            <text class="stat-label">剩余下载</text>
            <text class="stat-value">{{ payload.stats.remainingDownloads }}</text>
          </view>
          <view v-if="payload.stats.expiresAt" class="stat glass-light">
            <text class="stat-label">有效期</text>
            <text class="stat-value">{{ payload.stats.expiresAt }}</text>
          </view>
        </view>

        <view v-if="payload.downloadUrl" class="download">
          <text class="download-label">资源包下载地址</text>
          <view class="download-row">
            <text class="download-url" @tap="copyUrl(payload.downloadUrl)">{{ payload.downloadUrl }}</text>
            <button class="download-btn" @tap="copyUrl(payload.downloadUrl)">复制链接</button>
          </view>
          <text v-if="payload.extractionCode" class="download-code">提取码：{{ payload.extractionCode }}</text>
        </view>

        <view class="history">
          <text class="history-title">下载记录</text>
          <view v-if="!payload.history.length" class="history-empty">暂无下载记录</view>
          <view v-else class="history-list">
            <view v-for="item in payload.history" :key="item.id" class="history-item glass-light">
              <view class="history-row">
                <text class="history-label">链接</text>
                <text class="history-value">{{ item.url }}</text>
              </view>
              <view class="history-row">
                <text class="history-label">提取码</text>
                <text class="history-value">{{ item.extractionCode || '—' }}</text>
              </view>
              <view class="history-row">
                <text class="history-label">分配时间</text>
                <text class="history-value">{{ item.assignedAt }}</text>
              </view>
            </view>
          </view>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { storeToRefs } from 'pinia';
import { useAppStore } from '@/stores/app';

const appStore = useAppStore();
const { verification } = storeToRefs(appStore);

const code = ref('');
const payload = computed(() => verification.value);
const loading = computed(() => appStore.loading.verification);

const statusStyle = computed(() => {
  switch (payload.value?.status) {
    case 'success':
      return { title: '验证成功', statusClass: 'status-success' };
    case 'warning':
      return { title: '请注意', statusClass: 'status-warning' };
    case 'error':
      return { title: '验证失败', statusClass: 'status-error' };
    default:
      return { title: '提醒', statusClass: 'status-info' };
  }
});

function handleVerify() {
  if (!code.value.trim()) {
    uni.showToast({ title: '请输入卡密', icon: 'none' });
    return;
  }
  appStore.loadVerification(code.value.trim());
}

function copyUrl(url: string) {
  uni.setClipboardData({
    data: url,
    success: () => {
      uni.showToast({ title: '链接已复制', icon: 'success' });
    }
  });
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

.verify-card {
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 36rpx;
}

.form {
  display: grid;
  gap: 20rpx;
}

.form-label {
  font-size: 24rpx;
  color: var(--text-muted);
}

.form-input {
  padding: 18rpx 22rpx;
  border-radius: 22rpx;
  background: var(--surface-light);
  border: 1rpx solid var(--outline-color);
  color: var(--text-primary);
  font-size: 28rpx;
}

.form-submit {
  padding: 18rpx 24rpx;
  border-radius: 999rpx;
  border: none;
  font-size: 28rpx;
  font-weight: 600;
  color: #04101c;
  background: var(--accent-gradient);
}

.form-submit:disabled {
  opacity: 0.65;
}

.result {
  display: flex;
  flex-direction: column;
  gap: 28rpx;
}

.status-banner {
  padding: 24rpx;
  border-radius: 24rpx;
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.status-success {
  background: rgba(16, 185, 129, 0.15);
  border: 1rpx solid rgba(16, 185, 129, 0.4);
}

.status-warning {
  background: rgba(251, 191, 36, 0.12);
  border: 1rpx solid rgba(251, 191, 36, 0.35);
}

.status-error {
  background: rgba(248, 113, 113, 0.14);
  border: 1rpx solid rgba(248, 113, 113, 0.35);
}

.status-info {
  background: rgba(56, 189, 248, 0.14);
  border: 1rpx solid rgba(56, 189, 248, 0.35);
}

.status-title {
  font-size: 30rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.status-message {
  font-size: 24rpx;
  color: var(--text-muted);
}

.stats {
  display: grid;
  gap: 16rpx;
  grid-template-columns: repeat(auto-fit, minmax(180rpx, 1fr));
}

.stat {
  padding: 18rpx 20rpx;
  border-radius: 18rpx;
  display: flex;
  flex-direction: column;
  gap: 8rpx;
}

.stat-label {
  font-size: 24rpx;
  color: var(--text-muted);
}

.stat-value {
  font-size: 30rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.download {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
  background: rgba(56, 189, 248, 0.08);
  border-radius: 18rpx;
  padding: 18rpx 22rpx;
}

.download-label {
  font-size: 24rpx;
  color: var(--text-muted);
}

.download-row {
  display: flex;
  align-items: center;
  gap: 16rpx;
}

.download-url {
  flex: 1;
  font-size: 24rpx;
  color: var(--text-primary);
  word-break: break-all;
}

.download-btn {
  padding: 12rpx 20rpx;
  border-radius: 12rpx;
  border: none;
  background: rgba(56, 189, 248, 0.25);
  color: #38bdf8;
  font-size: 24rpx;
}

.download-code {
  font-size: 22rpx;
  color: var(--text-muted);
}

.history {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
}

.history-title {
  font-size: 28rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.history-empty {
  padding: 16rpx;
  border-radius: 14rpx;
  background: var(--surface-light);
  color: var(--text-muted);
  font-size: 24rpx;
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.history-item {
  display: flex;
  flex-direction: column;
  gap: 10rpx;
  padding: 18rpx 20rpx;
}

.history-row {
  display: flex;
  justify-content: space-between;
  font-size: 24rpx;
  color: var(--text-muted);
}

.history-label {
  color: var(--text-muted);
}

.history-value {
  color: var(--text-primary);
  margin-left: 20rpx;
  text-align: right;
}
</style>
