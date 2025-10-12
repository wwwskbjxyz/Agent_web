<template>
  <view class="page">
    <view class="login-card glass-card">
      <view class="login-header">
        <text class="login-badge">SProtect Console</text>
        <text class="login-title">统一身份认证</text>
        <text class="login-subtitle">连接代理后台、卡密面板与即时沟通系统</text>
      </view>

      <view class="login-form">
        <view class="form-item">
          <text class="form-label">账号</text>
          <input
            v-model="form.username"
            placeholder="请输入控制台账号"
            class="form-input"
            confirm-type="next"
          />
        </view>

        <view class="form-item">
          <text class="form-label">密码</text>
          <input
            v-model="form.password"
            placeholder="请输入密码"
            password
            class="form-input"
            confirm-type="done"
          />
        </view>

        <view class="form-extra">
          <label class="remember">
            <switch color="#6366f1" :checked="remember" @change="toggleRemember" />
            <text>保持登录</text>
          </label>
          <button class="forget" @tap="showSupport">忘记密码？</button>
        </view>

        <button class="submit" :disabled="isSubmitting" @tap="handleSubmit">
          {{ isSubmitting ? '正在登录...' : '进入面板' }}
        </button>

        <view class="login-hint">
          <text>首次接入请在 README 中查看如何绑定后端 API。</text>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue';
import { useAppStore } from '@/stores/app';

const appStore = useAppStore();
const form = reactive({
  username: '',
  password: ''
});

const remember = ref(true);
const isSubmitting = computed(() => appStore.loading.login);

function toggleRemember(event: any) {
  remember.value = event.detail.value;
}

async function handleSubmit() {
  if (!form.username || !form.password) {
    uni.showToast({ title: '请填写完整账号与密码', icon: 'none' });
    return;
  }

  try {
    await appStore.login({ ...form });
    uni.showToast({ title: '登录成功', icon: 'success' });
    setTimeout(() => {
      uni.reLaunch({ url: '/pages/index/index' });
    }, 400);
  } catch (error) {
    const message =
      (typeof error === 'object' && error && 'message' in error && typeof (error as any).message === 'string'
        ? (error as any).message
        : '登录失败，请检查账号');
    uni.showToast({ title: message || '登录失败，请检查账号', icon: 'none' });
    console.error(error);
  }
}

function showSupport() {
  uni.showModal({
    title: '联系支持',
    content: '请联系渠道管理员重置密码或在后端系统中修改。',
    confirmText: '已了解',
    showCancel: false
  });
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

.login-card {
  width: min(680rpx, 100%);
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 48rpx 40rpx;
}

.login-header {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
  text-align: center;
}

.login-badge {
  align-self: center;
  padding: 12rpx 24rpx;
  border-radius: 999rpx;
  background: rgba(139, 92, 246, 0.18);
  color: rgba(139, 92, 246, 0.95);
  font-size: 22rpx;
  letter-spacing: 0.08em;
}

.login-title {
  font-size: 44rpx;
  font-weight: 600;
  color: #f8fafc;
}

.login-subtitle {
  font-size: 26rpx;
  color: rgba(226, 232, 240, 0.75);
}

.login-form {
  display: flex;
  flex-direction: column;
  gap: 24rpx;
}

.form-item {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.form-label {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.85);
}

.form-input {
  padding: 18rpx 22rpx;
  border-radius: 22rpx;
  background: rgba(7, 11, 18, 0.85);
  border: 1rpx solid rgba(99, 102, 241, 0.35);
  color: #f8fafc;
  font-size: 28rpx;
}

.form-extra {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.75);
}

.remember {
  display: inline-flex;
  align-items: center;
  gap: 12rpx;
}

.forget {
  background: transparent;
  border: none;
  color: rgba(56, 189, 248, 0.9);
  font-size: 24rpx;
}

.submit {
  margin-top: 12rpx;
  padding: 22rpx;
  border-radius: 28rpx;
  border: none;
  font-size: 30rpx;
  font-weight: 600;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.85), rgba(14, 165, 233, 0.85));
  color: #04070d;
}

.submit:disabled {
  opacity: 0.75;
}

.login-hint {
  margin-top: 8rpx;
  text-align: center;
  color: rgba(148, 163, 184, 0.7);
  font-size: 22rpx;
}
</style>