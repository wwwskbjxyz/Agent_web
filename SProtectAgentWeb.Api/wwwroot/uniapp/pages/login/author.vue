<template>
  <view class="page">
    <view class="glass-card card">
      <view class="header">
        <text class="badge">作者管理</text>
        <text class="title">注册 / 登录作者端</text>
        <text class="subtitle">
          默认展示登录表单，可通过上方标签在“登录”和“注册”之间切换。完成注册后即可在此处直接登录并管理您的软件码。
        </text>
      </view>

      <view class="tabs">
        <button :class="['tab', mode === 'login' ? 'active' : '']" @tap="mode = 'login'">登录</button>
        <button :class="['tab', mode === 'register' ? 'active' : '']" @tap="mode = 'register'">注册</button>
      </view>

      <view class="form">
        <view class="form-item" v-if="mode === 'login'">
          <text class="label">用户名 / 邮箱</text>
          <input v-model="form.account" class="input" placeholder="请输入用户名或邮箱" />
        </view>
        <view class="form-item" v-else>
          <text class="label">用户名</text>
          <input v-model="form.username" class="input" placeholder="设置作者登录用户名" />
        </view>
        <view class="form-item" v-if="mode === 'register'">
          <text class="label">邮箱</text>
          <input v-model="form.email" class="input" placeholder="接收通知与找回密码的邮箱" />
        </view>
        <view class="form-item">
          <text class="label">密码</text>
          <input v-model="form.password" class="input" placeholder="至少 6 位密码" password />
        </view>
        <view class="form-item" v-if="mode === 'register'">
          <text class="label">展示名称</text>
          <input v-model="form.displayName" class="input" placeholder="作者昵称或公司名" />
        </view>
        <view class="form-item" v-if="mode === 'register'">
          <text class="label">接口地址</text>
          <input v-model="form.apiAddress" class="input" placeholder="部署主机 IP 或域名" />
        </view>
        <view class="form-item" v-if="mode === 'register'">
          <text class="label">接口端口</text>
          <input v-model.number="form.apiPort" class="input" type="number" placeholder="如：8080" />
        </view>
        <view class="form-item" v-if="mode === 'register'">
          <text class="label">软件类型</text>
          <picker :range="softwareTypes" range-key="label" @change="handleTypeChange">
            <view class="picker">{{ currentSoftwareType.label }}</view>
          </picker>
        </view>
      </view>

      <button class="submit" :disabled="isBusy" @tap="handleSubmit">
        {{ mode === 'login' ? (isBusy ? '登录中...' : '立即登录') : isBusy ? '注册中...' : '提交注册' }}
      </button>

      <view class="hint" v-if="mode === 'login' && profile">
        <text>软件码：{{ profile.softwareCode }}</text>
        <text>接口：{{ profile.apiAddress }}:{{ profile.apiPort }}</text>
      </view>

      <button class="link" @tap="back">返回入口</button>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue';
import { usePlatformStore } from '@/stores/platform';
import type { PlatformAuthorProfile } from '@/common/types';

const platform = usePlatformStore();
const mode = ref<'login' | 'register'>('login');
const profile = ref<PlatformAuthorProfile | null>(null);
const form = reactive({
  account: '',
  username: '',
  email: '',
  password: '',
  displayName: '',
  apiAddress: '',
  apiPort: 8080,
  softwareType: 'SP'
});

const softwareTypes = [
  { value: 'SP', label: 'SProtect (默认)' },
  { value: 'QP', label: 'QProtect' },
  { value: 'API', label: '自定义 API' }
];

const currentSoftwareType = computed(() => {
  return softwareTypes.find((item) => item.value === form.softwareType) ?? softwareTypes[0];
});

const isBusy = computed(() =>
  mode.value === 'login' ? platform.loading.loginAuthor : platform.loading.registerAuthor
);

function handleTypeChange(event: any) {
  const index = Number(event.detail.value ?? 0);
  const target = softwareTypes[index] ?? softwareTypes[0];
  form.softwareType = target.value;
}

async function handleSubmit() {
  if (mode.value === 'register') {
    if (!form.username || !form.email || !form.password || !form.displayName || !form.apiAddress) {
      uni.showToast({ title: '请完整填写注册信息', icon: 'none' });
      return;
    }

    try {
      const result = await platform.registerAuthor({
        username: form.username.trim(),
        email: form.email.trim(),
        password: form.password,
        displayName: form.displayName.trim(),
        apiAddress: form.apiAddress.trim(),
        apiPort: form.apiPort,
        softwareType: form.softwareType
      });
      profile.value = result;
      uni.showToast({ title: '注册成功，请登录', icon: 'success' });
      form.account = form.email.trim();
      form.password = '';
      mode.value = 'login';
      return;
    } catch (error) {
      const message =
        (error as any)?.message && typeof (error as any).message === 'string'
          ? (error as any).message
          : '操作失败，请稍后重试';
      uni.showToast({ title: message, icon: 'none' });
      return;
    }
  }

  if (!form.account || !form.password) {
    uni.showToast({ title: '请完整填写信息', icon: 'none' });
    return;
  }

  try {
    const result = await platform.loginAuthor({ account: form.account.trim(), password: form.password });
    profile.value = result;
    uni.showToast({ title: '登录成功', icon: 'success' });
    await platform.fetchAuthorProfile();
    uni.reLaunch({ url: '/pages/author/dashboard' });
  } catch (error) {
    const message =
      (error as any)?.message && typeof (error as any).message === 'string'
        ? (error as any).message
        : '操作失败，请稍后重试';
    uni.showToast({ title: message, icon: 'none' });
  }
}

function back() {
  uni.reLaunch({ url: '/pages/login/index' });
}
</script>

<style scoped lang="scss">
.page {
  min-height: 100vh;
  padding: 80rpx 40rpx;
  display: flex;
  align-items: center;
  justify-content: center;
}

.card {
  width: min(760rpx, 100%);
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 48rpx 40rpx;
}

.header {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
}

.badge {
  align-self: flex-start;
  padding: 12rpx 28rpx;
  border-radius: 999rpx;
  background: rgba(59, 130, 246, 0.18);
  color: rgba(96, 165, 250, 0.95);
  font-size: 22rpx;
  letter-spacing: 0.08em;
}

.title {
  font-size: 44rpx;
  font-weight: 600;
  color: #f8fafc;
}

.subtitle {
  font-size: 24rpx;
  color: rgba(226, 232, 240, 0.72);
  line-height: 1.6;
}

.tabs {
  display: flex;
  gap: 20rpx;
  padding: 6rpx;
  border-radius: 16rpx;
  background: rgba(30, 41, 59, 0.6);
}

.tab {
  flex: 1;
  padding: 20rpx 0;
  border-radius: 12rpx;
  background: transparent;
  color: rgba(148, 163, 184, 0.9);
  border: none;
  font-size: 26rpx;
  font-weight: 500;
  display: flex;
  align-items: center;
  justify-content: center;
}

.tab.active {
  background: linear-gradient(135deg, #6366f1, #8b5cf6);
  color: #fff;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 24rpx;
}

.form-item {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.label {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.85);
}

.input {
  padding: 18rpx 24rpx;
  border-radius: 16rpx;
  background: rgba(15, 23, 42, 0.55);
  border: 1px solid rgba(148, 163, 184, 0.2);
  color: #e2e8f0;
  font-size: 26rpx;
}

.picker {
  padding: 18rpx 24rpx;
  border-radius: 16rpx;
  background: rgba(15, 23, 42, 0.55);
  border: 1px solid rgba(148, 163, 184, 0.2);
  color: #e2e8f0;
  font-size: 26rpx;
}

.submit {
  width: 100%;
  padding: 24rpx 0;
  border-radius: 18rpx;
  background: linear-gradient(135deg, #22d3ee, #6366f1);
  color: #fff;
  font-size: 28rpx;
  font-weight: 600;
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
}

.hint {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
  padding: 24rpx;
  border-radius: 18rpx;
  background: rgba(15, 23, 42, 0.45);
  border: 1px dashed rgba(94, 234, 212, 0.35);
  color: rgba(125, 211, 252, 0.95);
  font-size: 24rpx;
}

.link {
  width: 100%;
  padding: 20rpx 0;
  border-radius: 14rpx;
  background: rgba(148, 163, 184, 0.15);
  color: rgba(226, 232, 240, 0.9);
  border: none;
  font-size: 24rpx;
  display: flex;
  align-items: center;
  justify-content: center;
}

button::after {
  border: none;
}
</style>
