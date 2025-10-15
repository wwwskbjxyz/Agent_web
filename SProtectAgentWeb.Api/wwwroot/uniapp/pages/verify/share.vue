<template>
  <view class="page">
    <view class="header glass-card">
      <view>
        <text class="title">卡密验证与资源下载</text>
        <text class="subtitle">
          {{ contextReady ? '输入卡密即可验证并获取下载链接' : '链接缺少必要信息，请联系代理确认' }}
        </text>
      </view>
    </view>

    <view class="context-card glass-card">
      <view class="context-row">
        <text class="context-label">软件位</text>
        <text class="context-value">{{ softwareLabel || '未提供' }}</text>
      </view>
      <view class="context-row">
        <text class="context-label">软件码</text>
        <text class="context-value">{{ softwareCode || '未提供' }}</text>
      </view>
      <view v-if="agentLabel" class="context-row">
        <text class="context-label">所属代理</text>
        <text class="context-value">{{ agentLabel }}</text>
      </view>
      <view v-if="contextError" class="context-warning">{{ contextError }}</view>
    </view>

    <view class="verify-card glass-card">
      <view class="form">
        <text class="form-label">卡密</text>
        <input
          v-model="cardKey"
          class="form-input"
          placeholder="请输入需要验证的卡密编号"
          confirm-type="done"
        />
        <button class="form-submit" :disabled="loading || !canVerify" @tap="handleVerify">
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
import { onLoad } from '@dcloudio/uni-app';
import { storeToRefs } from 'pinia';
import { useAppStore } from '@/stores/app';
import { apiRequest, setBaseURL } from '@/common/api';
import type { CardVerificationShareContext } from '@/common/types';

interface ShareContext {
  software: string;
  softwareCode: string;
  agentAccount?: string;
  agentDisplayName?: string;
  softwareDisplayName?: string;
  gateway?: string;
}

interface PartialShareContext {
  software?: string;
  softwareCode?: string;
  agentAccount?: string;
  agentDisplayName?: string;
  softwareDisplayName?: string;
  gateway?: string;
}

declare const getCurrentPages: (() => Array<{ route?: string }>) | undefined;

declare const Buffer: any;

const appStore = useAppStore();
const { verification } = storeToRefs(appStore);

const software = ref('');
const softwareDisplayName = ref('');
const softwareCode = ref('');
const agentAccount = ref('');
const agentDisplayName = ref('');
const contextError = ref('');

const cardKey = ref('');

const loading = computed(() => appStore.loading.verification);
const payload = computed(() => verification.value);
const softwareLabel = computed(() => softwareDisplayName.value || software.value);
const agentLabel = computed(() => agentDisplayName.value || agentAccount.value || '');
const contextReady = computed(() => !!software.value && !!softwareCode.value);
const canVerify = computed(() => contextReady.value && !!cardKey.value.trim() && !loading.value);

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

onLoad(async (options) => {
  contextError.value = '链接解析中，请稍候';
  resetContextState();
  try {
    const context = await resolveContext(options);
    if (context) {
      applyContext(context);
    } else {
      contextError.value = '链接信息缺失，请联系代理重新生成';
    }
  } catch (error) {
    console.warn('Failed to resolve share context', error);
    contextError.value = '解析链接信息失败，请稍后重试';
  }
});

function applyContext(context: ShareContext) {
  software.value = context.software;
  softwareDisplayName.value = context.softwareDisplayName || context.software;
  softwareCode.value = context.softwareCode;
  agentAccount.value = context.agentAccount ?? '';
  agentDisplayName.value = context.agentDisplayName ?? '';
  contextError.value = '';
  verification.value = null;
  if (context.gateway) {
    setBaseURL(context.gateway);
  }
}

function resetContextState() {
  software.value = '';
  softwareDisplayName.value = '';
  softwareCode.value = '';
  agentAccount.value = '';
  agentDisplayName.value = '';
  verification.value = null;
}

function handleVerify() {
  if (!cardKey.value.trim()) {
    uni.showToast({ title: '请输入卡密', icon: 'none' });
    return;
  }
  if (!contextReady.value) {
    uni.showToast({ title: contextError.value || '链接信息缺失', icon: 'none' });
    return;
  }
  appStore.loadVerification(cardKey.value.trim(), {
    software: software.value,
    softwareCode: softwareCode.value,
    agentAccount: agentAccount.value || undefined
  });
}

function copyUrl(url: string) {
  if (!url) {
    uni.showToast({ title: '暂无链接可复制', icon: 'none' });
    return;
  }
  uni.setClipboardData({
    data: url,
    success: () => {
      uni.showToast({ title: '链接已复制', icon: 'success' });
    }
  });
}

async function resolveContext(options: Record<string, any> | undefined): Promise<ShareContext | null> {
  const primary = await ensureContext(resolveFromOptions(options));
  if (primary) {
    return primary;
  }
  const fallback = await resolveFromRoute();
  return ensureContext(fallback);
}

async function ensureContext(candidate: PartialShareContext | null | undefined): Promise<ShareContext | null> {
  if (!candidate) {
    return null;
  }

  const gatewayValue = resolveGateway(candidate.gateway ?? '');
  const codeValue = sanitizeString(candidate.softwareCode);
  const softwareValue = sanitizeString(candidate.software);
  const displayValue = sanitizeString(candidate.softwareDisplayName);
  const agentValue = sanitizeString(candidate.agentAccount);
  const agentDisplayValue = sanitizeString(candidate.agentDisplayName);

  if (softwareValue && codeValue) {
    return {
      software: softwareValue,
      softwareCode: codeValue,
      softwareDisplayName: displayValue || softwareValue,
      agentAccount: agentValue || undefined,
      agentDisplayName: agentDisplayValue || undefined,
      gateway: gatewayValue || undefined
    };
  }

  if (!codeValue) {
    return null;
  }

  const remote = await fetchContextByCode(codeValue);
  if (!remote) {
    return null;
  }

  const remoteSoftware = sanitizeString(remote.software);
  const remoteCode = sanitizeString(remote.softwareCode) || codeValue;
  const remoteDisplay = sanitizeString(remote.softwareDisplayName);
  const remoteAgent = sanitizeString(remote.agentAccount);
  const remoteAgentDisplay = sanitizeString(remote.agentDisplayName);

  if (!remoteSoftware) {
    return null;
  }

  return {
    software: remoteSoftware,
    softwareCode: remoteCode,
    softwareDisplayName: displayValue || remoteDisplay || remoteSoftware,
    agentAccount: agentValue || remoteAgent || undefined,
    agentDisplayName: agentDisplayValue || remoteAgentDisplay || undefined,
    gateway: gatewayValue || undefined
  };
}

function resolveFromOptions(options: Record<string, any> | undefined): PartialShareContext | null {
  if (!options) {
    return null;
  }
  const shareRaw = decodeComponent(options.share);
  if (shareRaw) {
    const decoded = decodeShareSlug(shareRaw);
    if (decoded) {
      return decoded;
    }
  }

  const softwareValue = decodeComponent(
    options.s ?? options.software ?? options.softwareName ?? options.slot
  );
  const codeValue = decodeComponent(
    options.c ?? options.code ?? options.softwareCode ?? options.binding
  );
  const agentValue = decodeComponent(options.a ?? options.agent ?? options.agentAccount);
  const agentDisplayValue = decodeComponent(
    options.agentName ?? options.agentDisplay ?? options.agentDisplayName
  );
  const displayValue = decodeComponent(
    options.display ?? options.softwareDisplay ?? options.softwareDisplayName ?? options.name
  );
  const gatewayValue = resolveGateway(decodeComponent(options.gateway ?? options.api ?? options.gw));

  if (!codeValue) {
    return null;
  }

  return {
    software: softwareValue || undefined,
    softwareCode: codeValue,
    agentAccount: agentValue || undefined,
    agentDisplayName: agentDisplayValue || undefined,
    softwareDisplayName: displayValue || undefined,
    gateway: gatewayValue || undefined
  };
}

async function resolveFromRoute(): Promise<PartialShareContext | null> {
  if (typeof getCurrentPages !== 'function') {
    return null;
  }
  const pages = getCurrentPages();
  if (!Array.isArray(pages) || !pages.length) {
    return null;
  }
  const current = pages[pages.length - 1] as any;
  const fullPath: string = current?.$page?.fullPath || '';
  if (!fullPath) {
    return null;
  }
  const queryIndex = fullPath.indexOf('?');
  if (queryIndex < 0) {
    const segments = fullPath.split('/').filter(Boolean);
    if (segments.length > 2) {
      const slug = segments[segments.length - 1];
      const decoded = decodeShareSlug(decodeComponent(slug));
      if (decoded) {
        return decoded;
      }
    }
    return null;
  }
  const queryString = fullPath.slice(queryIndex + 1);
  const map = parseQuery(queryString);
  if (map.share) {
    const decoded = decodeShareSlug(decodeComponent(map.share));
    if (decoded) {
      return decoded;
    }
  }
  const softwareValue = decodeComponent(
    map.s || map.software || map.softwarename || map.slot || ''
  );
  const codeValue = decodeComponent(
    map.c || map.code || map.softwarecode || map.binding || ''
  );
  const agentValue = decodeComponent(
    map.a || map.agent || map.agentaccount || ''
  );
  const agentDisplayValue = decodeComponent(
    map.agentname || map.agentdisplay || map.agentdisplayname || ''
  );
  const displayValue = decodeComponent(
    map.display || map.softwaredisplay || map.softwaredisplayname || map.name || ''
  );
  const gatewayValue = resolveGateway(decodeComponent(map.gateway || map.api || map.gw));

  if (!codeValue) {
    return null;
  }

  return {
    software: softwareValue || undefined,
    softwareCode: codeValue,
    agentAccount: agentValue || undefined,
    agentDisplayName: agentDisplayValue || undefined,
    softwareDisplayName: displayValue || undefined,
    gateway: gatewayValue || undefined
  };
}

function sanitizeString(value: any): string {
  if (typeof value !== 'string') {
    return '';
  }
  return value.trim();
}

function decodeComponent(value: any): string {
  const input = sanitizeString(value);
  if (!input) {
    return '';
  }
  try {
    return decodeURIComponent(input);
  } catch (error) {
    return input;
  }
}

function parseQuery(query: string): Record<string, string> {
  const map: Record<string, string> = {};
  if (!query) {
    return map;
  }
  const pairs = query.split('&');
  for (const pair of pairs) {
    if (!pair) continue;
    const [key, value = ''] = pair.split('=');
    const normalizedKey = sanitizeString(key);
    if (!normalizedKey) continue;
    map[normalizedKey] = value;
  }
  return map;
}

function decodeShareSlug(slug: string): PartialShareContext | null {
  if (!slug) {
    return null;
  }
  try {
    let base64 = slug.replace(/-/g, '+').replace(/_/g, '/');
    const padding = base64.length % 4;
    if (padding) {
      base64 = base64.padEnd(base64.length + (4 - padding), '=');
    }
    const buffer = base64ToArrayBuffer(base64);
    const json = utf8Decode(new Uint8Array(buffer));
    const raw = JSON.parse(json) ?? {};
    const softwareValue = sanitizeString(raw.s ?? raw.software ?? raw.softwareName ?? raw.slot);
    const codeValue = sanitizeString(raw.c ?? raw.code ?? raw.softwareCode ?? raw.binding);
    const agentValue = sanitizeString(raw.a ?? raw.agent ?? raw.agentAccount);
    const agentDisplayValue = sanitizeString(
      raw.an ?? raw.agentName ?? raw.agentDisplay ?? raw.agentDisplayName
    );
    const displayValue = sanitizeString(
      raw.d ?? raw.display ?? raw.softwareDisplay ?? raw.softwareDisplayName
    );
    const gatewayValue = resolveGateway(sanitizeString(raw.g));
    if (!codeValue) {
      return null;
    }
    return {
      software: softwareValue || undefined,
      softwareCode: codeValue,
      agentAccount: agentValue || undefined,
      agentDisplayName: agentDisplayValue || undefined,
      softwareDisplayName: displayValue || undefined,
      gateway: gatewayValue || undefined
    };
  } catch (error) {
    console.warn('Failed to decode share slug', error);
    return null;
  }
}

async function fetchContextByCode(code: string): Promise<PartialShareContext | null> {
  const normalized = sanitizeString(code);
  if (!normalized) {
    return null;
  }
  try {
    const payload = await apiRequest<CardVerificationShareContext>({
      url: '/api/card-verification/context',
      method: 'GET',
      data: { softwareCode: normalized },
      skipProxy: true,
      auth: false
    });
    if (!payload) {
      return null;
    }
    return {
      software: sanitizeString(payload.software) || undefined,
      softwareCode: sanitizeString(payload.softwareCode) || normalized,
      softwareDisplayName: sanitizeString(payload.softwareDisplayName) || undefined,
      agentAccount: sanitizeString(payload.agentAccount) || undefined,
      agentDisplayName: sanitizeString(payload.agentDisplayName) || undefined
    };
  } catch (error) {
    console.warn('Failed to load verification context', error);
    return null;
  }
}

function resolveGateway(value: string): string {
  if (!value) {
    return '';
  }
  const trimmed = value.trim();
  if (!trimmed) {
    return '';
  }
  const lower = trimmed.toLowerCase();
  if (!lower.startsWith('http://') && !lower.startsWith('https://')) {
    return '';
  }
  return trimmed.replace(/\/+$/, '');
}

function base64ToArrayBuffer(base64: string): ArrayBuffer {
  if (typeof uni !== 'undefined' && typeof uni.base64ToArrayBuffer === 'function') {
    return uni.base64ToArrayBuffer(base64);
  }
  if (typeof atob === 'function') {
    const binary = atob(base64);
    const length = binary.length;
    const bytes = new Uint8Array(length);
    for (let i = 0; i < length; i++) {
      bytes[i] = binary.charCodeAt(i);
    }
    return bytes.buffer;
  }
  if (typeof Buffer !== 'undefined') {
    const buffer = Buffer.from(base64, 'base64');
    return buffer.buffer.slice(buffer.byteOffset, buffer.byteOffset + buffer.byteLength);
  }
  throw new Error('当前环境不支持 Base64 解码');
}

function utf8Decode(bytes: Uint8Array): string {
  if (typeof TextDecoder !== 'undefined') {
    return new TextDecoder().decode(bytes);
  }
  let result = '';
  for (let i = 0; i < bytes.length;) {
    const byte1 = bytes[i++];
    if (byte1 < 0x80) {
      result += String.fromCharCode(byte1);
    } else if (byte1 < 0xe0) {
      const byte2 = bytes[i++];
      result += String.fromCharCode(((byte1 & 0x1f) << 6) | (byte2 & 0x3f));
    } else if (byte1 < 0xf0) {
      const byte2 = bytes[i++];
      const byte3 = bytes[i++];
      result += String.fromCharCode(((byte1 & 0x0f) << 12) | ((byte2 & 0x3f) << 6) | (byte3 & 0x3f));
    } else {
      const byte2 = bytes[i++];
      const byte3 = bytes[i++];
      const byte4 = bytes[i++];
      const codePoint =
        ((byte1 & 0x07) << 18) |
        ((byte2 & 0x3f) << 12) |
        ((byte3 & 0x3f) << 6) |
        (byte4 & 0x3f);
      const offset = codePoint - 0x10000;
      const high = 0xd800 + (offset >> 10);
      const low = 0xdc00 + (offset & 0x3ff);
      result += String.fromCharCode(high, low);
    }
  }
  return result;
}
</script>

<style scoped lang="scss">
.page {
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 40rpx 28rpx 80rpx;
  min-height: 100vh;
  background: radial-gradient(120% 120% at 50% 0%, rgba(0, 150, 255, 0.15), rgba(5, 7, 15, 0.92)), #05070f;
  color: var(--text-primary, #f5f7ff);
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

.context-card {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
  padding: 32rpx;
}

.context-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 26rpx;
}

.context-label {
  color: var(--text-muted);
}

.context-value {
  color: var(--text-primary);
  font-weight: 600;
  margin-left: 12rpx;
}

.context-warning {
  color: #ff6b6b;
  font-size: 24rpx;
}

.verify-card {
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 32rpx;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 24rpx;
}

.form-label {
  font-size: 26rpx;
  color: var(--text-muted);
}

.form-input {
  width: 100%;
  height: 88rpx;
  border-radius: 16rpx;
  padding: 0 24rpx;
  background: rgba(255, 255, 255, 0.04);
  color: var(--text-primary);
}

.form-submit {
  height: 88rpx;
  border-radius: 16rpx;
  background: linear-gradient(135deg, #00e0ff, #0072ff);
  color: #ffffff;
  font-size: 30rpx;
}

.result {
  display: flex;
  flex-direction: column;
  gap: 28rpx;
}

.status-banner {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
  padding: 24rpx;
  border-radius: 20rpx;
}

.status-title {
  font-size: 32rpx;
  font-weight: 600;
}

.status-message {
  font-size: 26rpx;
}

.status-success {
  background: rgba(0, 208, 132, 0.14);
  color: #2affb3;
}

.status-warning {
  background: rgba(255, 196, 0, 0.14);
  color: #ffc400;
}

.status-error {
  background: rgba(255, 77, 79, 0.14);
  color: #ff4d4f;
}

.status-info {
  background: rgba(0, 122, 255, 0.14);
  color: #00a8ff;
}

.stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180rpx, 1fr));
  gap: 20rpx;
}

.stat {
  padding: 24rpx;
  border-radius: 20rpx;
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.stat-label {
  font-size: 24rpx;
  color: var(--text-muted);
}

.stat-value {
  font-size: 32rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.download {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.download-label {
  font-size: 26rpx;
  color: var(--text-muted);
}

.download-row {
  display: flex;
  align-items: center;
  gap: 16rpx;
}

.download-url {
  flex: 1;
  font-size: 26rpx;
  color: var(--accent-color);
  word-break: break-all;
}

.download-btn {
  height: 72rpx;
  padding: 0 24rpx;
  border-radius: 16rpx;
  font-size: 26rpx;
  background: rgba(0, 225, 255, 0.12);
  color: var(--accent-color);
}

.download-code {
  font-size: 24rpx;
  color: var(--text-secondary);
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
  font-size: 24rpx;
  color: var(--text-muted);
}

.history-list {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
}

.history-item {
  padding: 24rpx;
  border-radius: 20rpx;
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.history-row {
  display: flex;
  justify-content: space-between;
  font-size: 24rpx;
}

.history-label {
  color: var(--text-muted);
}

.history-value {
  color: var(--text-primary);
  margin-left: 12rpx;
}
</style>
