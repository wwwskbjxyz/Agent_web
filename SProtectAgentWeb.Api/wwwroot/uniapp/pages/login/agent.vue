<template>
  <view class="page">
    <view class="glass-card card">
      <view class="header">
        <text class="badge">代理中心</text>
        <text class="title">登录 / 注册代理账号</text>
        <text class="subtitle">
          默认展示登录表单，可通过上方标签切换至注册。注册仅需一次，需填写邮箱并绑定首个软件码，后续即可在此直接登录使用。
        </text>
      </view>

      <view class="tabs">
        <button :class="['tab', mode === 'login' ? 'active' : '']" @tap="mode = 'login'">登录</button>
        <button :class="['tab', mode === 'register' ? 'active' : '']" @tap="mode = 'register'">注册</button>
      </view>

      <view class="form">
        <view class="form-item" v-if="mode === 'login'">
          <text class="label">用户名 / 邮箱</text>
          <input
            v-model="form.account"
            class="input"
            placeholder="请输入用户名或邮箱"
            keyboard-type="email"
          />
        </view>

        <view class="form-item" v-else>
          <text class="label">用户名</text>
          <input v-model="form.username" class="input" placeholder="用于登录与展示" />
        </view>

        <view class="form-item" v-if="mode === 'register'">
          <text class="label">邮箱</text>
          <input v-model="form.email" class="input" placeholder="接收通知与找回密码" keyboard-type="email" />
        </view>

        <view class="form-item">
          <text class="label">密码</text>
          <input v-model="form.password" class="input" placeholder="至少 6 位密码" password />
        </view>

        <template v-if="mode === 'register'">
          <view class="form-item">
            <text class="label">绑定软件码</text>
            <input v-model="form.softwareCode" class="input" placeholder="请输入作者提供的软件码" />
          </view>
          <view class="form-item">
            <text class="label">作者账号</text>
            <input v-model="form.authorAccount" class="input" placeholder="用于转发到作者端的账号" />
          </view>
          <view class="form-item">
            <text class="label">作者密码</text>
            <input v-model="form.authorPassword" class="input" placeholder="用于转发到作者端的密码" password />
          </view>
        </template>
      </view>

      <button class="submit" :disabled="isBusy" @tap="handleSubmit">
        {{ mode === 'login' ? (isBusy ? '登录中...' : '立即登录') : isBusy ? '注册中...' : '完成注册' }}
      </button>

      <view class="extra">
        <button class="link" @tap="back">返回入口</button>
        <button
          class="link primary"
          :disabled="wechatLoginBusy"
          @tap="handleWeChatLogin"
        >
          {{ wechatLoginBusy ? '微信登录中...' : '微信快捷登录' }}
        </button>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue';
import { usePlatformStore } from '@/stores/platform';

const platform = usePlatformStore();
const mode = ref<'login' | 'register'>('login');
const form = reactive({
  account: '',
  username: '',
  email: '',
  password: '',
  softwareCode: '',
  authorAccount: '',
  authorPassword: ''
});

const isWeChatMiniProgram = ref(false);
// #ifdef MP-WEIXIN
isWeChatMiniProgram.value = true;
// #endif

const isBusy = computed(() =>
  mode.value === 'login' ? platform.loading.loginAgent : platform.loading.registerAgent
);
const wechatLoginBusy = computed(() => platform.loading.loginAgentWeChat);

async function handleSubmit() {
  if (mode.value === 'register') {
    if (!form.username || !form.email || !form.password || !form.softwareCode || !form.authorAccount || !form.authorPassword) {
      uni.showToast({ title: '请完整填写注册信息', icon: 'none' });
      return;
    }

    try {
      await platform.registerAgent({
        username: form.username.trim(),
        email: form.email.trim(),
        password: form.password,
        softwareCode: form.softwareCode.trim().toUpperCase(),
        authorAccount: form.authorAccount.trim(),
        authorPassword: form.authorPassword
      });
      uni.showToast({ title: '注册成功，请登录', icon: 'success' });
      form.account = form.email.trim();
      form.password = '';
      form.softwareCode = '';
      form.authorAccount = '';
      form.authorPassword = '';
      mode.value = 'login';
      return;
    } catch (error) {
      const message =
        (error as any)?.message && typeof (error as any).message === 'string'
          ? (error as any).message
          : '注册失败，请稍后重试';
      uni.showToast({ title: message, icon: 'none' });
      return;
    }
  }

  if (!form.account || !form.password) {
    uni.showToast({ title: '请完整填写信息', icon: 'none' });
    return;
  }

  try {
    const session = await platform.loginAgent({
      account: form.account.trim(),
      password: form.password
    });
    if (session?.bindings?.length) {
      uni.reLaunch({ url: '/pages/index/index' });
    } else {
      uni.showModal({
        title: '登录成功',
        content: '您还没有绑定任何作者软件码，是否现在绑定？',
        success(res) {
          if (res.confirm) {
            uni.navigateTo({ url: '/pages/agent/bind' });
          } else {
            uni.reLaunch({ url: '/pages/index/index' });
          }
        }
      });
    }
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

async function handleWeChatLogin() {
  if (!isWeChatMiniProgram.value) {
    uni.showToast({ title: '请在微信小程序内使用微信登录', icon: 'none' });
    return;
  }

  if (wechatLoginBusy.value) {
    return;
  }

  try {
    const jsCode = await new Promise<string>((resolve, reject) => {
      uni.login({
        provider: 'weixin',
        onlyAuthorize: true,
        success: (res) => {
          if (res.code) {
            resolve(res.code);
          } else {
            reject(new Error('未获取到登录凭证'));
          }
        },
        fail: (error) => reject(error)
      });
    });

    await platform.loginAgentWithWeChat(jsCode);
    uni.reLaunch({ url: '/pages/index/index' });
  } catch (error) {
    const message =
      (error as any)?.errMsg ||
      (error as any)?.message ||
      '微信登录失败，请稍后再试';
    uni.showToast({ title: message, icon: 'none' });
  }
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
  width: min(720rpx, 100%);
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
  color: rgba(226, 232, 240, 0.7);
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

.submit {
  margin-top: 8rpx;
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

.extra {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
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

  &.primary {
    background: linear-gradient(135deg, #38bdf8, #6366f1);
    color: #fff;

    &:disabled {
      opacity: 0.6;
    }
  }
}

button::after {
  border: none;
}
</style>
