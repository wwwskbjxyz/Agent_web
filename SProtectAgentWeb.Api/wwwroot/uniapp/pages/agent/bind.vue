<template>
  <view class="page">
    <view class="glass-card card">
      <view class="header">
        <text class="badge">绑定软件码</text>
        <text class="title">为当前代理添加作者账号</text>
        <text class="subtitle">输入作者提供的软件码与登录凭证，主控系统将代为安全转发。</text>
      </view>

      <view class="form">
        <view class="form-item">
          <text class="label">软件码</text>
          <input v-model="form.softwareCode" class="input" placeholder="SP-xxxx" />
        </view>
        <view class="form-item">
          <text class="label">作者账号</text>
          <input v-model="form.authorAccount" class="input" placeholder="作者端登录账号" />
        </view>
        <view class="form-item">
          <text class="label">作者密码</text>
          <input v-model="form.authorPassword" class="input" placeholder="作者端登录密码" password />
        </view>
      </view>

      <button class="submit" :disabled="isSubmitting" @tap="handleBind">
        {{ isSubmitting ? '绑定中...' : '立即绑定' }}
      </button>

      <view class="list">
        <view v-if="!bindings.length" class="empty">暂未绑定任何作者，添加后可快速访问对应模块。</view>
        <view v-for="item in bindings" :key="item.bindingId" class="binding">
          <view class="binding-info">
            <text class="binding-title">{{ item.authorDisplayName }}</text>
            <text class="binding-sub">软件码：{{ item.softwareCode }}</text>
            <text class="binding-sub">接口：{{ item.apiAddress }}:{{ item.apiPort }}</text>
          </view>
          <view class="binding-actions">
            <button class="binding-btn" @tap="edit(item)">编辑凭证</button>
            <button class="binding-btn" @tap="select(item)">进入</button>
            <button class="binding-btn danger" :disabled="platform.loading.deleteBinding" @tap="remove(item.bindingId)">
              删除
            </button>
          </view>
        </view>
      </view>
    </view>
    <view v-if="showEditor" class="editor-mask" @tap.self="closeEditor">
      <view class="glass-card editor-card">
        <view class="editor-header">
          <text class="editor-title">修改作者凭证</text>
          <text class="editor-sub">更新后将用于转发登录</text>
        </view>
        <view class="form">
          <view class="form-item">
            <text class="label">作者账号</text>
            <input v-model="editor.authorAccount" class="input" placeholder="作者端登录账号" />
          </view>
          <view class="form-item">
            <text class="label">作者密码</text>
            <input v-model="editor.authorPassword" class="input" placeholder="作者端登录密码" password />
          </view>
        </view>
        <view class="editor-actions">
          <button class="binding-btn ghost" @tap="closeEditor">取消</button>
          <button class="binding-btn" :disabled="editorLoading" @tap="submitEdit">
            {{ editorLoading ? '保存中...' : '保存修改' }}
          </button>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { usePlatformStore } from '@/stores/platform';
import { useAppStore } from '@/stores/app';

const platform = usePlatformStore();
const appStore = useAppStore();
const form = reactive({
  softwareCode: '',
  authorAccount: '',
  authorPassword: ''
});

const isSubmitting = computed(() => platform.loading.createBinding);
const bindings = computed(() => platform.bindings);
const editorLoading = computed(() => platform.loading.updateBinding);
const showEditor = ref(false);
const editor = reactive({
  bindingId: 0,
  authorAccount: '',
  authorPassword: ''
});

async function loginWithBinding(binding: (typeof platform.bindings)[number], options?: { silent?: boolean }) {
  if (!binding.authorAccount || !binding.authorPassword) {
    return;
  }
  try {
    await appStore.login(
      { username: binding.authorAccount, password: binding.authorPassword },
      { skipBootstrap: false }
    );
  } catch (error) {
    if (!options?.silent) {
      const message =
        (error as any)?.message && typeof (error as any).message === 'string'
          ? (error as any).message
          : '自动登录作者端失败，请稍后重试';
      uni.showToast({ title: message, icon: 'none' });
    }
    throw error;
  }
}

onMounted(async () => {
  if (!platform.isAuthenticated) {
    uni.reLaunch({ url: '/pages/login/agent' });
    return;
  }

  if (!platform.bindings.length) {
    await platform.fetchAgentProfile();
  }
});

async function handleBind() {
  if (!form.softwareCode || !form.authorAccount || !form.authorPassword) {
    uni.showToast({ title: '请完整填写信息', icon: 'none' });
    return;
  }

  try {
    const result = await platform.createBinding({ ...form });
    uni.showToast({ title: '绑定成功', icon: 'success' });
    platform.selectBinding(result);
    try {
      await loginWithBinding(result, { silent: true });
    } catch (error) {
      console.warn('登录作者端失败', error);
    }
    form.softwareCode = '';
    form.authorAccount = '';
    form.authorPassword = '';
  } catch (error) {
    const message =
      (error as any)?.message && typeof (error as any).message === 'string'
        ? (error as any).message
        : '绑定失败，请检查软件码';
    uni.showToast({ title: message, icon: 'none' });
  }
}

function select(item: (typeof platform.bindings)[number]) {
  platform.selectBinding(item);
  loginWithBinding(item, { silent: true })
    .then(() => {
      uni.showToast({ title: '已切换到 ' + item.softwareCode, icon: 'success' });
      uni.reLaunch({ url: '/pages/index/index' });
    })
    .catch(() => {
      uni.reLaunch({ url: '/pages/index/index' });
    });
}

function edit(item: (typeof platform.bindings)[number]) {
  editor.bindingId = item.bindingId;
  editor.authorAccount = item.authorAccount;
  editor.authorPassword = item.authorPassword;
  showEditor.value = true;
}

function closeEditor() {
  showEditor.value = false;
}

async function submitEdit() {
  if (!editor.authorAccount || !editor.authorPassword) {
    uni.showToast({ title: '请输入完整信息', icon: 'none' });
    return;
  }

  const target = bindings.value.find((item) => item.bindingId === editor.bindingId);
  if (!target) {
    uni.showToast({ title: '未找到绑定记录', icon: 'none' });
    return;
  }

  try {
    await platform.updateBinding(editor.bindingId, {
      softwareCode: target.softwareCode,
      authorAccount: editor.authorAccount.trim(),
      authorPassword: editor.authorPassword
    });
    uni.showToast({ title: '已更新', icon: 'success' });
    closeEditor();
  } catch (error) {
    const message =
      (error as any)?.message && typeof (error as any).message === 'string'
        ? (error as any).message
        : '更新失败，请稍后重试';
    uni.showToast({ title: message, icon: 'none' });
  }
}

async function remove(bindingId: number) {
  uni.showModal({
    title: '确认删除',
    content: '删除后将无法通过主控访问该作者接口，确定删除吗？',
    success: async (res) => {
      if (!res.confirm) return;
      await platform.deleteBinding(bindingId);
      uni.showToast({ title: '已删除', icon: 'success' });
    }
  });
}
</script>

<style scoped lang="scss">
.page {
  min-height: 100vh;
  padding: 60rpx 32rpx;
  display: flex;
  align-items: flex-start;
  justify-content: center;
}

.card {
  width: min(820rpx, 100%);
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 40rpx 36rpx;
}

.header {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
}

.badge {
  align-self: flex-start;
  padding: 10rpx 26rpx;
  border-radius: 999rpx;
  background: rgba(96, 165, 250, 0.18);
  color: rgba(125, 211, 252, 0.95);
  font-size: 22rpx;
  letter-spacing: 0.08em;
}

.title {
  font-size: 42rpx;
  font-weight: 600;
  color: #f8fafc;
}

.subtitle {
  font-size: 24rpx;
  color: rgba(226, 232, 240, 0.75);
  line-height: 1.6;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
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
  padding: 22rpx 0;
  border-radius: 16rpx;
  background: linear-gradient(135deg, #22d3ee, #6366f1);
  color: #fff;
  font-size: 28rpx;
  font-weight: 600;
  border: none;
}

.list {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
}

.editor-mask {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.65);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40rpx 32rpx;
  z-index: 99;
}

.editor-card {
  width: min(720rpx, 100%);
  display: flex;
  flex-direction: column;
  gap: 24rpx;
  padding: 36rpx 32rpx;
}

.editor-header {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
}

.editor-title {
  font-size: 34rpx;
  font-weight: 600;
  color: #f8fafc;
}

.editor-sub {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.75);
}

.editor-actions {
  display: flex;
  gap: 20rpx;
  justify-content: flex-end;
}

.binding-btn.ghost {
  background: rgba(148, 163, 184, 0.18);
  color: rgba(226, 232, 240, 0.9);
}

.empty {
  padding: 28rpx;
  border-radius: 16rpx;
  background: rgba(15, 23, 42, 0.45);
  color: rgba(148, 163, 184, 0.9);
  font-size: 24rpx;
  text-align: center;
}

.binding {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
  padding: 24rpx;
  border-radius: 18rpx;
  background: rgba(15, 23, 42, 0.55);
  border: 1px solid rgba(148, 163, 184, 0.18);
}

.binding-info {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
}

.binding-title {
  font-size: 30rpx;
  color: #f8fafc;
  font-weight: 600;
}

.binding-sub {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.8);
}

.binding-actions {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12rpx;
  width: 100%;
}

.binding-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 16rpx 0;
  border-radius: 14rpx;
  border: none;
  font-size: 24rpx;
  font-weight: 600;
  background: rgba(59, 130, 246, 0.22);
  color: rgba(191, 219, 254, 0.95);
  width: 100%;
}

.binding-btn.danger {
  background: rgba(239, 68, 68, 0.2);
  color: rgba(248, 113, 113, 0.95);
}
</style>
