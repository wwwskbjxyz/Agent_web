<template>
  <view class="page" v-if="platform.isAuthenticated">
    <scroll-view scroll-y class="content">
      <view class="section">
        <view class="section-header">
          <text class="section-title">已绑定的软件</text>
          <button class="link" @tap="goBind">绑定软件码</button>
        </view>

        <view class="binding-list">
          <view
            v-for="item in platform.bindings"
            :key="item.bindingId"
            :class="['binding-card', platform.selectedBinding?.bindingId === item.bindingId ? 'active' : '']"
            @tap="select(item)"
          >
            <view class="binding-head">
              <text class="binding-code">{{ item.authorDisplayName || '未命名软件' }}</text>
              <text class="binding-type">{{ item.softwareType }}</text>
            </view>
            <text class="binding-meta">接口：{{ item.apiAddress }}:{{ item.apiPort }}</text>
            <text class="binding-meta">作者账号：{{ item.authorAccount }}</text>
          </view>
        </view>
      </view>

    </scroll-view>
  </view>
  <view v-else class="empty">
    <text>请先登录代理账号。</text>
    <button class="link" @tap="goLogin">前往登录</button>
  </view>
</template>

<script setup lang="ts">
import { onMounted } from 'vue';
import { usePlatformStore } from '@/stores/platform';
import { useAppStore } from '@/stores/app';

const platform = usePlatformStore();
const appStore = useAppStore();

onMounted(async () => {
  if (!platform.isAuthenticated) {
    return;
  }

  if (!platform.bindings.length) {
    await platform.fetchAgentProfile();
  }

  const active = platform.selectedBinding || platform.bindings[0];
  if (active) {
    appStore
      .login({ username: active.authorAccount, password: active.authorPassword })
      .catch(() => {
        // 忽略自动登录失败，用户可手动登录
      });
  }
});

function select(item: (typeof platform.bindings)[number]) {
  platform.selectBinding(item);
  appStore
    .login({ username: item.authorAccount, password: item.authorPassword })
    .then(() => {
      uni.showToast({ title: '已切换 ' + item.softwareCode, icon: 'success' });
    })
    .catch((error) => {
      const message =
        (error as any)?.message && typeof (error as any).message === 'string'
          ? (error as any).message
          : '自动登录作者端失败，请手动登录';
      uni.showToast({ title: message, icon: 'none' });
    });
}

function goBind() {
  uni.navigateTo({ url: '/pages/agent/bind' });
}

function goLogin() {
  uni.reLaunch({ url: '/pages/login/agent' });
}
</script>

<style scoped lang="scss">
.page {
  min-height: 100vh;
  padding: 24rpx 24rpx 80rpx;
}

.content {
  height: 100%;
}

.section {
  margin-bottom: 32rpx;
  padding: 32rpx 28rpx;
  border-radius: 24rpx;
  background: rgba(15, 23, 42, 0.55);
  border: 1px solid rgba(148, 163, 184, 0.18);
  display: flex;
  flex-direction: column;
  gap: 24rpx;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.section-title {
  font-size: 32rpx;
  font-weight: 600;
  color: #f8fafc;
}

.link {
  padding: 16rpx 24rpx;
  border-radius: 14rpx;
  border: none;
  background: rgba(59, 130, 246, 0.25);
  color: rgba(191, 219, 254, 0.95);
  font-size: 24rpx;
}

.binding-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280rpx, 1fr));
  gap: 20rpx;
}

.binding-card {
  padding: 24rpx;
  border-radius: 20rpx;
  background: rgba(15, 23, 42, 0.6);
  border: 1px solid transparent;
  display: flex;
  flex-direction: column;
  gap: 12rpx;
  color: rgba(226, 232, 240, 0.85);
}

.binding-card.active {
  border-color: rgba(96, 165, 250, 0.6);
  box-shadow: 0 0 0 1px rgba(59, 130, 246, 0.35);
}

.binding-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.binding-code {
  font-size: 30rpx;
  font-weight: 600;
  color: #f8fafc;
  max-width: 420rpx;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.binding-type {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.8);
}

.binding-meta {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.75);
}

.empty {
  min-height: 100vh;
  padding: 80rpx 40rpx;
  display: flex;
  flex-direction: column;
  gap: 24rpx;
  align-items: center;
  justify-content: center;
  color: rgba(226, 232, 240, 0.85);
}
</style>
