<template>
  <view class="page" v-if="profile">
    <view class="top-bar">
      <text class="page-title">软件管理</text>
      <view class="top-actions">
        <button class="ghost" @tap="handleLogout">退出登录</button>
        <button class="danger" :disabled="platform.loading.deleteAuthor" @tap="handleDeleteAccount">
          {{ platform.loading.deleteAuthor ? '注销中...' : '注销账号' }}
        </button>
      </view>
    </view>

    <scroll-view scroll-y class="content">
      <!-- #ifdef MP-WEIXIN -->
      <view class="glass-card section wechat-section" v-if="isWeChatMiniProgram">
        <view class="section-header wechat-header">
          <text class="section-title">微信订阅</text>
          <view class="wechat-status-group">
            <text class="wechat-status">{{ wechatStatusText }}</text>
            <view v-if="wechatDisplayName" class="wechat-chip">{{ wechatDisplayName }}</view>
          </view>
        </view>
        <view class="wechat-actions">
          <button class="primary" :disabled="wechatBusy" @tap="handleWeChatBind">
            {{ wechatBindLabel }}
          </button>
          <button
            v-if="isWeChatBound"
            class="ghost"
            :disabled="wechatBusy"
            @tap="handleWeChatUnbind"
          >
            解绑
          </button>
        </view>
        <view v-if="showSubscriptionPanel" class="wechat-subscription">
          <view v-for="option in authorWechatTemplateOptions" :key="option.key" class="subscription-row">
            <view class="subscription-info">
              <text class="subscription-title">{{ option.title }}</text>
              <text class="subscription-desc">{{ option.description }}</text>
            </view>
            <switch
              class="subscription-switch"
              :checked="subscriptionState[option.key] !== false"
              @change="onSubscriptionToggle(option.key, $event.detail.value)"
              color="#22d3ee"
            />
          </view>
          <button class="primary" :disabled="wechatSubscribeBusy" @tap="applySubscriptionRequests()">
            {{ wechatSubscribeBusy ? '授权中...' : '保存并请求订阅' }}
          </button>
          <text class="subscription-hint">提示：仅对已开启的提醒类型发起订阅请求，可随时调整。</text>
        </view>
        <view v-else class="wechat-subscription empty">
          <text v-if="isWeChatBound">未配置订阅模板，请联系平台管理员设置模板 ID。</text>
          <text v-else>绑定微信后可开启通知提醒。</text>
        </view>
      </view>
      <!-- #endif -->

      <view class="glass-card section">
        <view class="section-header">
          <text class="section-title">选择软件</text>
          <button class="link ghost" @tap="startCreate" :disabled="platform.loading.createAuthorSoftware">
            {{ platform.loading.createAuthorSoftware ? '处理中...' : '新增软件码' }}
          </button>
        </view>

        <template v-if="softwares.length">
          <view class="selector" v-if="softwares.length > 1">
            <picker :range="softwares" range-key="displayName" @change="handleSoftwareChange">
              <view class="picker">
                {{ currentSoftware?.displayName || '选择软件' }}
              </view>
            </picker>
          </view>
        </template>
        <view v-else class="empty-tip">暂无软件，请先新增。</view>

        <view class="software-list" v-if="softwares.length">
          <view
            v-for="software in softwares"
            :key="software.softwareId"
            :class="['software-card', currentSoftware?.softwareId === software.softwareId ? 'active' : '']"
          >
            <view class="software-header">
              <text class="software-name">{{ software.displayName }}</text>
              <text class="software-tag">{{ resolveTypeLabel(software.softwareType) }}</text>
            </view>
            <view class="software-code-row">
              <text class="software-code">软件码：{{ software.softwareCode }}</text>
              <button class="ghost mini" @tap="copySoftwareCode(software.softwareCode)">复制</button>
            </view>
            <text class="software-endpoint">接口：{{ software.apiAddress }}:{{ software.apiPort }}</text>
            <view class="software-actions">
              <button
                class="ghost small"
                :disabled="currentSoftware?.softwareId === software.softwareId"
                @tap="selectSoftware(software)"
              >
                {{ currentSoftware?.softwareId === software.softwareId ? '当前使用' : '设为当前' }}
              </button>
              <button class="ghost small" @tap="editSoftware(software)">编辑</button>
            </view>
          </view>
        </view>

        <view v-if="currentSoftware" class="info">
          <view class="info-item code-line">
            <text>当前软件码：{{ currentSoftware.softwareCode }}</text>
            <button class="ghost mini" @tap="copySoftwareCode(currentSoftware.softwareCode)">复制</button>
          </view>
          <view class="info-actions">
            <button class="link" @tap="handleRegenerate" :disabled="platform.loading.regenerateAuthorCode">
              {{ platform.loading.regenerateAuthorCode ? '生成中...' : '刷新软件码' }}
            </button>
            <button
              class="link danger"
              @tap="handleDeleteSoftware"
              :disabled="platform.loading.deleteAuthorSoftware || softwares.length <= 1"
            >
              删除软件码
            </button>
          </view>
        </view>
      </view>

      <view class="glass-card section" v-if="isCreating || currentSoftware">
        <view class="section-header">
          <text class="section-title">{{ isCreating ? '新增软件配置' : '更新接口配置' }}</text>
        </view>

        <view class="form">
          <view class="form-item">
            <text class="label">展示名称</text>
            <input v-model="form.displayName" class="input" placeholder="作者昵称或公司名" />
          </view>
          <view class="form-item">
            <text class="label">接口地址</text>
            <input v-model="form.apiAddress" class="input" placeholder="部署主机 IP 或域名" />
          </view>
          <view class="form-item">
            <text class="label">接口端口</text>
            <input v-model.number="form.apiPort" class="input" type="number" placeholder="如：8080" />
          </view>
          <view class="form-item">
            <text class="label">软件类型</text>
            <picker :range="softwareTypes" range-key="label" @change="handleTypeChange">
              <view class="picker">{{ currentSoftwareType.label }}</view>
            </picker>
          </view>
        </view>

        <view class="form-actions">
          <button class="submit" :disabled="saving" @tap="handleSave">
            {{ saving ? '保存中...' : isCreating ? '提交新增' : '保存修改' }}
          </button>
          <button v-if="isCreating" class="ghost" @tap="cancelCreate">取消</button>
        </view>
      </view>

      <view v-if="!isCreating && currentSoftware" class="glass-card note-card">
        <text class="note-title">提示</text>
        <text class="note-text">更新后请提醒代理同步新的接口账号密码信息。</text>
      </view>
    </scroll-view>
  </view>
  <view v-else class="empty">
    <text>正在加载作者信息...</text>
  </view>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import type { PlatformAuthorSoftware } from '@/common/types';
import { usePlatformStore } from '@/stores/platform';

const platform = usePlatformStore();
const { authorWeChatBinding, authorWechatSubscriptions, wechatTemplates, wechatTemplatePreviews } = storeToRefs(platform);
const profile = computed(() => platform.authorProfile);
const softwares = computed(() => profile.value?.softwares ?? []);
const selectedSoftwareId = computed({
  get: () => platform.selectedAuthorSoftwareId,
  set: (value: number | null) => platform.selectAuthorSoftware(value)
});
const currentSoftware = computed(() => {
  if (!softwares.value.length) {
    return null;
  }
  const matched = softwares.value.find((item) => item.softwareId === selectedSoftwareId.value);
  return matched ?? softwares.value[0] ?? null;
});

interface WeChatTemplateOption {
  key: 'instant' | 'blacklist' | 'settlement';
  title: string;
  description: string;
  templateId: string;
  preview?: Record<string, string> | null;
}

const isWeChatMiniProgram = ref(false);
// #ifdef MP-WEIXIN
isWeChatMiniProgram.value = true;
// #endif


const isWeChatBound = computed(() => Boolean(authorWeChatBinding.value));
const wechatBusy = computed(
  () =>
    platform.loading.wechatBinding ||
    platform.loading.wechatBind ||
    platform.loading.wechatUnbind
);
const authorWechatTemplateOptions = computed<WeChatTemplateOption[]>(() => {
  const config = wechatTemplates.value;
  if (!config) {
    return [];
  }
  const candidates: WeChatTemplateOption[] = [
    {
      key: 'instant',
      title: '即时沟通提醒',
      description: '代理或客户有新留言时提醒',
      templateId: config.instantCommunication?.trim() ?? '',
      preview: wechatTemplatePreviews.value?.['instant'] ?? null
    },
    {
      key: 'blacklist',
      title: '黑名单严重警告',
      description: '黑名单触发时快速通知',
      templateId: config.blacklistAlert?.trim() ?? '',
      preview: wechatTemplatePreviews.value?.['blacklist'] ?? null
    },
    {
      key: 'settlement',
      title: '结算账单通知',
      description: '结算周期到期时提醒处理账单',
      templateId: config.settlementNotice?.trim() ?? '',
      preview: wechatTemplatePreviews.value?.['settlement'] ?? null
    }
  ];
  return candidates.filter((item) => item.templateId.length >= 10 && !item.templateId.includes('...'));
});

const subscriptionState = computed<Record<string, boolean>>(() => authorWechatSubscriptions.value ?? {});
const showSubscriptionPanel = computed(() => isWeChatBound.value && authorWechatTemplateOptions.value.length > 0);

function maskOpenId(value?: string | null) {
  if (!value) {
    return '';
  }
  if (value.length <= 6) {
    return value;
  }
  const prefix = value.slice(0, 4);
  const suffix = value.slice(-3);
  return `${prefix}***${suffix}`;
}

const wechatDisplayName = computed(() => {
  const binding = authorWeChatBinding.value;
  if (!binding) {
    return '';
  }
  const nickname = binding.nickname?.trim();
  if (nickname) {
    return nickname;
  }
  return maskOpenId(binding.openId);
});

const wechatBindLabel = computed(() => {
  if (wechatBusy.value) {
    return '处理中...';
  }
  return isWeChatBound.value ? '重新绑定' : '绑定微信';
});

const wechatStatusText = computed(() => {
  if (!isWeChatMiniProgram.value) {
    return '仅微信小程序支持';
  }
  if (wechatBusy.value && !authorWeChatBinding.value) {
    return '加载中...';
  }
  return isWeChatBound.value ? '已绑定' : '未绑定';
});
const wechatSubscribeBusy = ref(false);

function onSubscriptionToggle(key: string, value: boolean) {
  platform.setAuthorWechatSubscription(key, value);
}

async function applySubscriptionRequests(auto = false) {
  if (!isWeChatMiniProgram.value) {
    if (!auto) {
      uni.showToast({ title: '请在微信小程序内操作', icon: 'none' });
    }
    return;
  }

  await platform.fetchWeChatTemplates().catch(() => undefined);
  const selected = authorWechatTemplateOptions.value.filter((option) => (subscriptionState.value[option.key] ?? true));
  if (!selected.length) {
    if (!auto) {
      uni.showToast({ title: '请至少开启一个提醒类型', icon: 'none' });
    }
    return;
  }

  const tmplIds = selected.map((option) => option.templateId);
  wechatSubscribeBusy.value = true;
  try {
    await new Promise<void>((resolve, reject) => {
      uni.requestSubscribeMessage({
        tmplIds,
        success: () => resolve(),
        fail: (error) => reject(error)
      });
    });
    if (!auto) {
      uni.showToast({ title: '已提交订阅', icon: 'success' });
    }

    let previewDelivered = false;
    for (const option of selected) {
      const delivered = await platform.sendWeChatPreview('author', option.key, auto);
      previewDelivered = previewDelivered || delivered;
    }

    if (!auto && previewDelivered) {
      uni.showToast({ title: '已发送测试提醒', icon: 'success' });
    }
  } catch (error) {
    const rawMessage = (error as any)?.errMsg || (error as any)?.message || '';
    if (typeof rawMessage === 'string' && rawMessage.includes('No template data return')) {
      uni.showModal({
        title: '订阅失败',
        content: '微信返回“未找到模板”，请确认小程序已配置对应的订阅消息模板并在平台中填写正确的模板 ID。',
        showCancel: false
      });
    } else if (!auto) {
      const message = rawMessage || '订阅失败，请稍后重试';
      uni.showToast({ title: message, icon: 'none' });
    }
  } finally {
    wechatSubscribeBusy.value = false;
  }
}

function seedDefaultSubscriptions() {
  if (!isWeChatBound.value || !authorWechatTemplateOptions.value.length) {
    return;
  }
  const current = subscriptionState.value;
  if (current && Object.keys(current).length > 0) {
    return;
  }
  authorWechatTemplateOptions.value.forEach((option) => {
    platform.setAuthorWechatSubscription(option.key, true);
  });
}

function mpLogin(): Promise<string> {
  return new Promise((resolve, reject) => {
    uni.login({
      provider: 'weixin',
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
}

async function ensureWeChatBinding() {
  if (!isWeChatMiniProgram.value || !platform.isAuthorAuthenticated) {
    return;
  }
  await platform.fetchWeChatBinding('author').catch(() => undefined);
  platform.fetchWeChatTemplates().catch(() => undefined);
}

watch(
  () => [authorWeChatBinding.value?.openId ?? null, authorWechatTemplateOptions.value.length],
  () => {
    seedDefaultSubscriptions();
  },
  { immediate: true }
);

async function handleWeChatBind() {
  if (!isWeChatMiniProgram.value) {
    uni.showToast({ title: '请在微信小程序内操作', icon: 'none' });
    return;
  }
  try {
    const code = await mpLogin();
    let nickname: string | undefined;
    try {
      if (typeof uni.getUserProfile === 'function') {
        const profile = await new Promise<any>((resolve, reject) => {
          uni.getUserProfile({
            desc: '用于展示绑定的微信昵称',
            success: (res) => resolve(res),
            fail: (error) => reject(error)
          });
        });
        nickname = profile.userInfo?.nickName;
      }
    } catch (error) {
      console.warn('获取作者昵称失败，继续绑定', error);
    }

    await platform.bindWeChat('author', code, nickname);
    await platform.fetchWeChatTemplates().catch(() => undefined);
    seedDefaultSubscriptions();
    uni.showToast({ title: '绑定成功', icon: 'success' });

    if (showSubscriptionPanel.value) {
      uni.showModal({
        title: '订阅提醒',
        content: '是否立即授权接收微信通知？',
        confirmText: '立即授权',
        cancelText: '稍后',
        success: (res) => {
          if (res.confirm) {
            applySubscriptionRequests(true).catch(() => undefined);
          }
        }
      });
    }
  } catch (error) {
    const message =
      (error as any)?.errMsg || (error as any)?.message || '绑定失败，请稍后重试';
    uni.showToast({ title: message, icon: 'none' });
  }
}

async function handleWeChatUnbind() {
  if (!isWeChatMiniProgram.value || !isWeChatBound.value) {
    return;
  }

  const confirmed = await new Promise<boolean>((resolve) => {
    uni.showModal({
      title: '确认解绑',
      content: '解绑后将无法接收订阅提醒，是否继续？',
      success: (res) => resolve(res.confirm),
      fail: () => resolve(false)
    });
  });

  if (!confirmed) {
    return;
  }

  try {
    await platform.unbindWeChat('author');
    uni.showToast({ title: '已解绑', icon: 'success' });
  } catch (error) {
    const message =
      (error as any)?.errMsg || (error as any)?.message || '解绑失败，请稍后重试';
    uni.showToast({ title: message, icon: 'none' });
  }
}

const isCreating = ref(false);
const form = reactive({
  softwareId: 0,
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

const currentSoftwareType = computed(
  () => softwareTypes.find((item) => item.value === form.softwareType) ?? softwareTypes[0]
);

const saving = computed(() =>
  platform.loading.updateAuthor || platform.loading.updateAuthorSoftware || platform.loading.createAuthorSoftware
);

watch(
  currentSoftware,
  (software) => {
    if (!software || isCreating.value) {
      return;
    }
    form.softwareId = software.softwareId;
    form.displayName = software.displayName;
    form.apiAddress = software.apiAddress;
    form.apiPort = software.apiPort;
    form.softwareType = software.softwareType;
  },
  { immediate: true }
);

onMounted(async () => {
  if (!platform.isAuthorAuthenticated) {
    uni.reLaunch({ url: '/pages/login/author' });
    return;
  }

  if (!profile.value) {
    try {
      await platform.fetchAuthorProfile();
    } catch (error) {
      uni.showToast({ title: '加载作者信息失败', icon: 'none' });
    }
  }

  await ensureWeChatBinding();
});

function handleTypeChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value ?? 0);
  const target = softwareTypes[index] ?? softwareTypes[0];
  form.softwareType = target.value;
}

function handleSoftwareChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value ?? 0);
  const target = softwares.value[index];
  if (target) {
    isCreating.value = false;
    platform.selectAuthorSoftware(target.softwareId);
  }
}

function copySoftwareCode(code: string) {
  if (!code) {
    return;
  }

  uni.setClipboardData({
    data: code,
    success: () => {
      uni.showToast({ title: '软件码已复制', icon: 'none' });
    },
    fail: () => {
      uni.showToast({ title: '复制失败', icon: 'none' });
    }
  });
}

function selectSoftware(software: PlatformAuthorSoftware) {
  isCreating.value = false;
  platform.selectAuthorSoftware(software.softwareId);
}

function editSoftware(software: PlatformAuthorSoftware) {
  selectSoftware(software);
  form.softwareId = software.softwareId;
  form.displayName = software.displayName;
  form.apiAddress = software.apiAddress;
  form.apiPort = software.apiPort;
  form.softwareType = software.softwareType;
  isCreating.value = false;
}

function resolveTypeLabel(type: string) {
  const target = softwareTypes.find((item) => item.value === type);
  return target?.label ?? type;
}

function startCreate() {
  isCreating.value = true;
  form.softwareId = 0;
  form.displayName = '';
  form.apiAddress = '';
  form.apiPort = 8080;
  form.softwareType = 'SP';
}

function cancelCreate() {
  isCreating.value = false;
  if (currentSoftware.value) {
    form.softwareId = currentSoftware.value.softwareId;
    form.displayName = currentSoftware.value.displayName;
    form.apiAddress = currentSoftware.value.apiAddress;
    form.apiPort = currentSoftware.value.apiPort;
    form.softwareType = currentSoftware.value.softwareType;
  }
}

async function handleSave() {
  if (!form.displayName || !form.apiAddress) {
    uni.showToast({ title: '请完善信息', icon: 'none' });
    return;
  }

  try {
    if (isCreating.value) {
      const software = await platform.createAuthorSoftware({
        displayName: form.displayName.trim(),
        apiAddress: form.apiAddress.trim(),
        apiPort: form.apiPort,
        softwareType: form.softwareType
      });
      isCreating.value = false;
      uni.showToast({ title: `已新增：${software.softwareCode}`, icon: 'none' });
    } else if (form.softwareId) {
      await platform.updateAuthorSoftware(form.softwareId, {
        displayName: form.displayName.trim(),
        apiAddress: form.apiAddress.trim(),
        apiPort: form.apiPort,
        softwareType: form.softwareType
      });
      uni.showToast({ title: '已保存', icon: 'success' });
    }
  } catch (error) {
    const message = (error as any)?.message && typeof (error as any).message === 'string'
      ? (error as any).message
      : '操作失败，请稍后重试';
    uni.showToast({ title: message, icon: 'none' });
  }
}

async function handleRegenerate() {
  if (!currentSoftware.value) {
    return;
  }

  try {
    const code = await platform.regenerateAuthorCode(currentSoftware.value.softwareId);
    uni.showToast({ title: `新软件码：${code}`, icon: 'none' });
    await platform.fetchAuthorProfile();
  } catch (error) {
    const message = (error as any)?.message && typeof (error as any).message === 'string'
      ? (error as any).message
      : '生成失败，请稍后重试';
    uni.showToast({ title: message, icon: 'none' });
  }
}

function handleLogout() {
  platform.logoutAuthor();
  uni.reLaunch({ url: '/pages/login/author' });
}

function handleDeleteAccount() {
  uni.showModal({
    title: '确认注销作者账号',
    content: '该操作不可恢复，确认继续？',
    success: async (res) => {
      if (!res.confirm) return;
      try {
        await platform.deleteAuthorAccount();
        uni.showToast({ title: '账号已注销', icon: 'none' });
        uni.reLaunch({ url: '/pages/login/author' });
      } catch (error) {
        const message = (error as any)?.message && typeof (error as any).message === 'string'
          ? (error as any).message
          : '操作失败，请稍后重试';
        uni.showToast({ title: message, icon: 'none' });
      }
    }
  });
}

function handleDeleteSoftware() {
  if (!currentSoftware.value || softwares.value.length <= 1) {
    return;
  }

  uni.showModal({
    title: '确认删除软件码',
    content: '删除后代理无法再使用此软件码，是否继续？',
    success: async (res) => {
      if (!res.confirm || !currentSoftware.value) return;
      try {
        await platform.deleteAuthorSoftware(currentSoftware.value.softwareId);
        uni.showToast({ title: '已删除', icon: 'success' });
      } catch (error) {
        const message = (error as any)?.message && typeof (error as any).message === 'string'
          ? (error as any).message
          : '删除失败，请稍后重试';
        uni.showToast({ title: message, icon: 'none' });
      }
    }
  });
}
</script>

<style scoped lang="scss">
.page {
  min-height: 100vh;
  padding: 40rpx 32rpx 80rpx;
  background: rgba(2, 6, 23, 0.9);
  display: flex;
  flex-direction: column;
}

.top-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 24rpx;
}

.page-title {
  font-size: 40rpx;
  font-weight: 600;
  color: #f8fafc;
}

.top-actions {
  display: flex;
  gap: 16rpx;
}

.content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 32rpx;
}

.section {
  display: flex;
  flex-direction: column;
  gap: 24rpx;
  padding: 32rpx 28rpx;
}

.wechat-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16rpx;
}

.wechat-status-group {
  display: flex;
  align-items: center;
  gap: 12rpx;
}

.wechat-status {
  font-size: 28rpx;
  color: rgba(226, 232, 240, 0.85);
}

.wechat-chip {
  padding: 6rpx 20rpx;
  border-radius: 999rpx;
  background: rgba(59, 130, 246, 0.2);
  color: #bfdbfe;
  font-size: 24rpx;
  font-weight: 500;
}

.wechat-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 16rpx;
  align-items: center;
  margin-top: 12rpx;
}

.wechat-subscription {
  margin-top: 18rpx;
  padding: 20rpx;
  border-radius: 20rpx;
  background: rgba(15, 23, 42, 0.55);
  border: 1px solid rgba(148, 163, 184, 0.25);
  display: flex;
  flex-direction: column;
  gap: 18rpx;
}

.subscription-row {
  display: flex;
  justify-content: space-between;
  gap: 16rpx;
  align-items: center;
}

.subscription-info {
  display: flex;
  flex-direction: column;
  gap: 6rpx;
  flex: 1;
}

.subscription-title {
  font-size: 26rpx;
  font-weight: 600;
  color: #f8fafc;
}

.subscription-desc {
  font-size: 22rpx;
  color: rgba(226, 232, 240, 0.7);
}

.subscription-switch {
  transform: scale(0.9);
}

.subscription-hint {
  font-size: 22rpx;
  color: rgba(203, 213, 225, 0.75);
}

.wechat-subscription.empty {
  margin-top: 16rpx;
  font-size: 24rpx;
  color: rgba(226, 232, 240, 0.7);
  line-height: 1.5;
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

.selector {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.picker {
  padding: 18rpx 24rpx;
  border-radius: 16rpx;
  background: rgba(15, 23, 42, 0.55);
  color: #e0f2fe;
  font-size: 26rpx;
}

.software-list {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
  margin-top: 24rpx;
}

.software-card {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
  padding: 24rpx;
  border-radius: 20rpx;
  background: rgba(15, 23, 42, 0.55);
  border: 1px solid rgba(148, 163, 184, 0.25);
  transition: border-color 0.2s ease, transform 0.2s ease;
}

.software-card.active {
  border-color: rgba(96, 165, 250, 0.75);
  box-shadow: 0 12rpx 28rpx rgba(59, 130, 246, 0.25);
  transform: translateY(-4rpx);
}

.software-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12rpx;
}

.software-name {
  font-size: 32rpx;
  font-weight: 600;
  color: #f8fafc;
}

.software-tag {
  padding: 8rpx 18rpx;
  border-radius: 999rpx;
  background: rgba(59, 130, 246, 0.2);
  color: rgba(191, 219, 254, 0.95);
  font-size: 22rpx;
}

.software-code,
.software-endpoint {
  font-size: 24rpx;
  color: rgba(226, 232, 240, 0.82);
}

.software-code-row {
  display: flex;
  align-items: center;
  gap: 16rpx;
  flex-wrap: wrap;
}

.software-actions {
  display: flex;
  gap: 16rpx;
  flex-wrap: wrap;
}

.ghost.small {
  flex: 1;
  min-width: 220rpx;
  padding: 22rpx 0;
  display: flex;
  align-items: center;
  justify-content: center;
}

.info {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
  color: rgba(226, 232, 240, 0.9);
  font-size: 26rpx;
}

.info-item.code-line {
  display: flex;
  align-items: center;
  gap: 16rpx;
  flex-wrap: wrap;
}

.info-actions {
  display: flex;
  gap: 16rpx;
  flex-wrap: wrap;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
}

.form-item {
  display: flex;
  flex-direction: column;
  gap: 10rpx;
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

.form-actions {
  display: flex;
  gap: 16rpx;
  flex-wrap: wrap;
}

.submit {
  flex: 1;
  min-width: 220rpx;
  padding: 24rpx 0;
  border: none;
  border-radius: 18rpx;
  background: linear-gradient(135deg, #38bdf8, #6366f1);
  color: #fff;
  font-size: 28rpx;
  font-weight: 600;
}

.link {
  padding: 14rpx 24rpx;
  border: none;
  background: rgba(59, 130, 246, 0.2);
  color: rgba(191, 219, 254, 0.95);
  border-radius: 14rpx;
  font-size: 24rpx;
}

.link.ghost {
  background: rgba(148, 163, 184, 0.25);
  color: rgba(226, 232, 240, 0.9);
}

.link.danger {
  background: rgba(248, 113, 113, 0.2);
  color: #fecaca;
}

.link:disabled {
  opacity: 0.6;
}

.primary {
  padding: 22rpx 40rpx;
  border-radius: 16rpx;
  border: none;
  background: linear-gradient(135deg, #60a5fa, #6366f1);
  color: #0f172a;
  font-size: 26rpx;
  font-weight: 600;
}

.ghost {
  padding: 22rpx 40rpx;
  border-radius: 16rpx;
  background: rgba(148, 163, 184, 0.2);
  color: rgba(226, 232, 240, 0.9);
  border: none;
  font-size: 26rpx;
}

.ghost.mini {
  padding: 16rpx 28rpx;
  font-size: 24rpx;
  min-width: auto;
}

.danger {
  padding: 22rpx 40rpx;
  border-radius: 16rpx;
  border: none;
  background: linear-gradient(135deg, #ef4444, #b91c1c);
  color: #fff;
  font-size: 26rpx;
  font-weight: 600;
}

.note-card {
  padding: 28rpx 24rpx;
  gap: 12rpx;
}

.note-title {
  font-size: 28rpx;
  font-weight: 600;
  color: #bae6fd;
}

.note-text {
  font-size: 24rpx;
  color: rgba(191, 219, 254, 0.85);
}

.empty {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(148, 163, 184, 0.8);
  font-size: 28rpx;
}

.empty-tip {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.8);
}
</style>
