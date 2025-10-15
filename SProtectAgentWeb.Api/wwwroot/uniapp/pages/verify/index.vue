<template>
  <view class="page">
    <view class="header glass-card">
      <view>
        <text class="title">卡密验证与资源下载</text>
        <text class="subtitle">输入卡密后自动校验授权状态并生成下载链接</text>
      </view>
    </view>

    <view class="context-card glass-card">
      <view class="context-header">
        <text class="context-label">当前软件位</text>
        <text class="context-value">{{ contextLabel }}</text>
      </view>
      <view v-if="contextCodeLabel" class="context-sub">绑定码：{{ contextCodeLabel }}</view>
      <view v-if="contextAgentLabel" class="context-sub">所属代理：{{ contextAgentLabel }}</view>
      <view v-if="shareContextError" class="context-sub error">{{ shareContextError }}</view>
      <view v-if="!shareContextError && showShareInfo" class="context-share">
        <text class="share-label">{{ shareLabel }}</text>
        <view class="share-row">
          <text class="share-url" @tap="copyShareLink">{{ shareDisplayValue }}</text>
          <button class="share-btn" @tap="copyShareLink">复制</button>
          <button v-if="isMiniProgram" class="share-btn ghost" @tap="openSharePage">打开</button>
        </view>
        <text class="share-hint">{{ shareHint }}</text>
      </view>
    </view>

    <view v-if="isGeneratorMode" class="generator-card glass-card">
      <text class="section-title">生成分享链接</text>
      <view class="field">
        <text class="field-label">绑定的软件码</text>
        <picker mode="selector" :range="bindingOptions" range-key="label" @change="handleBindingChange">
          <view class="picker-value">{{ currentBindingLabel }}</view>
        </picker>
      </view>
      <view class="field">
        <text class="field-label">软件位</text>
        <picker mode="selector" :range="softwareOptions" range-key="label" @change="handleSoftwareChange">
          <view class="picker-value">{{ currentSoftwareLabel }}</view>
        </picker>
      </view>
      <view class="generator-actions">
        <button class="secondary" :disabled="!generatorContext" @tap="generateShareLink">
          生成分享{{ isH5 ? '链接' : '路径' }}
        </button>
        <button
          v-if="showShareInfo && shareOriginIsGenerated"
          class="secondary ghost"
          @tap="copyShareLink"
        >
          复制{{ isH5 ? '链接' : '路径' }}
        </button>
        <button
          v-if="showShareInfo && shareOriginIsGenerated && isMiniProgram"
          class="secondary ghost"
          @tap="openSharePage"
        >
          打开页面
        </button>
      </view>
      <view v-if="showShareInfo && shareOriginIsGenerated" class="share-preview">{{ shareDisplayValue }}</view>
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
import { computed, ref, watch } from 'vue';
import { onLoad } from '@dcloudio/uni-app';
import { storeToRefs } from 'pinia';
import { apiRequest, setBaseURL } from '@/common/api';
import type { CardVerificationShareContext } from '@/common/types';
import { useAppStore } from '@/stores/app';
import { usePlatformStore } from '@/stores/platform';

interface ActiveVerificationContext {
  software: string;
  softwareCode?: string;
  agentAccount?: string;
  displayName?: string;
  agentDisplayName?: string;
  gateway?: string;
}

interface ShareCandidate {
  software?: string;
  softwareCode?: string;
  agentAccount?: string;
  gateway?: string;
  slug?: string;
  softwareDisplayName?: string;
  agentDisplayName?: string;
}

declare const getCurrentPages: (() => Array<{ route?: string }>) | undefined;
declare const Buffer: any;

const appStore = useAppStore();
const platformStore = usePlatformStore();
const { verification } = storeToRefs(appStore);
const { selectedSoftware, softwareList } = storeToRefs(appStore);
const { bindings, selectedBinding } = storeToRefs(platformStore);

const systemInfo = typeof uni !== 'undefined' && typeof uni.getSystemInfoSync === 'function' ? uni.getSystemInfoSync() : null;
const uniPlatform = (systemInfo?.uniPlatform || '').toLowerCase();
const isMiniProgram = uniPlatform.startsWith('mp');
const isH5 = typeof window !== 'undefined' && typeof document !== 'undefined';

const code = ref('');
const shareQuery = ref('');
const shareOrigin = ref<'link' | 'generated' | null>(null);
const linkContext = ref<ActiveVerificationContext | null>(null);
const shareContextError = ref('');
const generatorBindingId = ref<number | null>(null);
const generatorSoftware = ref('');
const defaultGateway = ref('');

const payload = computed(() => verification.value);
const loading = computed(() => appStore.loading.verification);
const isGeneratorMode = computed(() => !linkContext.value);
const canVerify = computed(() => {
  const ctx = activeContext.value;
  return !!ctx && !!ctx.softwareCode && !shareContextError.value;
});

const bindingOptions = computed(() =>
  bindings.value.map((item) => ({
    value: item.bindingId,
    label: `${item.authorDisplayName} (${item.softwareCode})`
  }))
);

const softwareOptions = computed(() =>
  softwareList.value.map((item) => ({
    value: item.softwareName,
    label: item.softwareName
  }))
);

const shareLabel = computed(() => (isH5 ? '分享链接' : '分享路径'));
const shareNavigatePath = computed(() => {
  if (!shareQuery.value) {
    return '';
  }
  return `/pages/verify/share?${shareQuery.value}`;
});
const shareHref = computed(() => {
  if (!shareQuery.value) {
    return '';
  }
  if (isH5 && typeof window !== 'undefined' && window.location) {
    return `${window.location.origin}/#/pages/verify/share?${shareQuery.value}`;
  }
  return shareNavigatePath.value;
});
const shareDisplayValue = computed(() => (isH5 ? shareHref.value : shareNavigatePath.value));
const shareOriginIsGenerated = computed(() => shareOrigin.value === 'generated');
const hasShareInfo = computed(() => !!shareQuery.value);
const showShareInfo = computed(() => shareOriginIsGenerated.value && hasShareInfo.value);

const generatorContext = computed<ActiveVerificationContext | null>(() => {
  if (!isGeneratorMode.value) {
    return null;
  }
  const softwareName = (generatorSoftware.value || selectedSoftware.value || '').trim();
  if (!softwareName) {
    return null;
  }
  const binding =
    bindings.value.find((item) => item.bindingId === generatorBindingId.value) || selectedBinding.value;
  const softwareCodeValue = binding?.softwareCode?.trim();
  const agentAccountValue = platformStore.agent?.username?.trim();
  return {
    software: softwareName,
    softwareCode: softwareCodeValue || undefined,
    agentAccount: agentAccountValue || undefined,
    displayName: softwareName
  };
});

const activeContext = computed<ActiveVerificationContext | null>(() => linkContext.value ?? generatorContext.value);

const contextLabel = computed(
  () => activeContext.value?.displayName || activeContext.value?.software || '未选择'
);
const contextCodeLabel = computed(() => activeContext.value?.softwareCode || '');
const contextAgentLabel = computed(
  () => activeContext.value?.agentDisplayName || activeContext.value?.agentAccount || ''
);

const currentBindingLabel = computed(() => {
  if (!isGeneratorMode.value) {
    return contextCodeLabel.value || '未提供绑定信息';
  }
  const option = bindingOptions.value.find((item) => item.value === generatorBindingId.value);
  return option?.label || '请选择绑定软件码';
});

const currentSoftwareLabel = computed(() => {
  if (!isGeneratorMode.value) {
    return contextLabel.value;
  }
  const option = softwareOptions.value.find((item) => item.value === generatorSoftware.value);
  return option?.label || '请选择软件位';
});

watch(
  bindings,
  (list) => {
    if (!isGeneratorMode.value) {
      return;
    }
    if (list.length > 0 && generatorBindingId.value == null) {
      generatorBindingId.value = list[0].bindingId;
    }
  },
  { immediate: true }
);

watch(
  selectedBinding,
  (binding) => {
    if (!isGeneratorMode.value || !binding) {
      return;
    }
    if (generatorBindingId.value == null) {
      generatorBindingId.value = binding.bindingId;
    }
  },
  { immediate: true }
);

watch(
  selectedSoftware,
  (software) => {
    if (!isGeneratorMode.value) {
      return;
    }
    if (!generatorSoftware.value && software) {
      generatorSoftware.value = software;
    }
  },
  { immediate: true }
);

watch(
  bindingOptions,
  (options) => {
    if (!isGeneratorMode.value) {
      return;
    }
    if (!options.length) {
      generatorBindingId.value = null;
      return;
    }
    if (!options.some((item) => item.value === generatorBindingId.value)) {
      generatorBindingId.value = options[0].value;
    }
  },
  { immediate: true }
);

watch(
  softwareOptions,
  (options) => {
    if (!isGeneratorMode.value) {
      return;
    }
    if (!options.length) {
      generatorSoftware.value = '';
      return;
    }
    if (!options.some((item) => item.value === generatorSoftware.value)) {
      generatorSoftware.value = options[0].value;
    }
  },
  { immediate: true }
);

watch([generatorBindingId, generatorSoftware], () => {
  if (shareOrigin.value === 'generated') {
    shareQuery.value = '';
    shareOrigin.value = null;
  }
});


function handleBindingChange(event: any) {
  const index = Number(event?.detail?.value ?? 0);
  const option = bindingOptions.value[index];
  generatorBindingId.value = option?.value ?? null;
}

function handleSoftwareChange(event: any) {
  const index = Number(event?.detail?.value ?? 0);
  const option = softwareOptions.value[index];
  generatorSoftware.value = option?.value ?? '';
}

function handleVerify() {
  if (!code.value.trim()) {
    uni.showToast({ title: '请输入卡密', icon: 'none' });
    return;
  }
  if (!canVerify.value) {
    const message = shareContextError.value || '未选择软件位，无法验证';
    uni.showToast({ title: message, icon: 'none' });
    return;
  }
  const ctx = activeContext.value;
  if (!ctx) {
    uni.showToast({ title: '未选择软件位，无法验证', icon: 'none' });
    return;
  }
  appStore.loadVerification(code.value.trim(), ctx);
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

function copyShareLink() {
  if (!showShareInfo.value || !shareDisplayValue.value) {
    uni.showToast({ title: '暂无分享信息', icon: 'none' });
    return;
  }
  copyUrl(shareDisplayValue.value);
}

function openSharePage() {
  if (!shareNavigatePath.value) {
    uni.showToast({ title: '暂无分享路径', icon: 'none' });
    return;
  }
  uni.navigateTo({ url: shareNavigatePath.value });
}


function generateShareLink() {
  const ctx = generatorContext.value;
  if (!ctx) {
    uni.showToast({ title: '请先选择绑定和软件位', icon: 'none' });
    return;
  }
  const binding =
    bindings.value.find((item) => item.bindingId === generatorBindingId.value) || selectedBinding.value;
  if (!ctx.softwareCode) {
    const codeValue = binding?.softwareCode ? `（${binding.softwareCode}）` : '';
    uni.showToast({ title: `绑定缺少软件码${codeValue}`, icon: 'none' });
    return;
  }
  shareQuery.value = buildShareQueryString(ctx);
  shareOrigin.value = 'generated';
  shareContextError.value = '';
  uni.showToast({ title: isH5 ? '已生成分享链接' : '已生成分享路径', icon: 'success' });
}

function buildShareCandidate(
  source: Record<string, any> | undefined
): { detected: boolean; candidate: ShareCandidate } {
  const candidate: ShareCandidate = {};
  let detected = false;

  if (!source) {
    return { detected: false, candidate };
  }

  const readValue = (...keys: string[]): string => {
    for (const key of keys) {
      const direct = extractValue(key);
      if (direct) {
        return direct;
      }
    }
    return '';
  };

  const extractValue = (key: string): string => {
    const raw = source[key];
    if (typeof raw === 'string' && raw.trim()) {
      return decodeURIComponentSafe(raw);
    }
    const lower = key.toLowerCase();
    if (lower !== key) {
      const fallback = source[lower];
      if (typeof fallback === 'string' && fallback.trim()) {
        return decodeURIComponentSafe(fallback);
      }
    }
    return '';
  };

  const softwareValue = normalizeParam(readValue('software', 's', 'softwareName', 'slot'));
  if (softwareValue) {
    candidate.software = softwareValue;
    detected = true;
  }

  const displayValue = normalizeParam(
    readValue('display', 'softwareDisplay', 'softwareDisplayName', 'name')
  );
  if (displayValue) {
    candidate.softwareDisplayName = displayValue;
    detected = true;
  }

  const codeValue = normalizeParam(readValue('code', 'c', 'softwareCode', 'binding'));
  if (codeValue) {
    candidate.softwareCode = codeValue;
    detected = true;
  }

  const agentValue = normalizeParam(readValue('agent', 'a', 'agentAccount'));
  if (agentValue) {
    candidate.agentAccount = agentValue;
    detected = true;
  }

  const agentDisplayValue = normalizeParam(
    readValue('agentName', 'agentDisplay', 'agentDisplayName')
  );
  if (agentDisplayValue) {
    candidate.agentDisplayName = agentDisplayValue;
    detected = true;
  }

  const gatewayRaw = normalizeParam(readValue('gateway', 'api', 'gw'));
  if (gatewayRaw) {
    detected = true;
    const normalizedGateway = resolveGateway(gatewayRaw);
    if (normalizedGateway) {
      candidate.gateway = normalizedGateway;
    }
  }

  const slugValue = normalizeParam(readValue('share', 'context', 'slug', 'token'));
  if (slugValue) {
    candidate.slug = slugValue;
    detected = true;
  }

  return { detected, candidate };
}

async function detectShareCandidateFromRoute(): Promise<{
  detected: boolean;
  candidate: ShareCandidate;
}> {
  if (typeof getCurrentPages !== 'function') {
    return { detected: false, candidate: {} as ShareCandidate };
  }

  const pages = getCurrentPages();
  if (!Array.isArray(pages) || !pages.length) {
    return { detected: false, candidate: {} as ShareCandidate };
  }

  const current = pages[pages.length - 1] as any;
  const fullPath: string = current?.$page?.fullPath || current?.route || '';
  if (!fullPath) {
    return { detected: false, candidate: {} as ShareCandidate };
  }

  const queryIndex = fullPath.indexOf('?');
  if (queryIndex >= 0) {
    const queryString = fullPath.slice(queryIndex + 1);
    const map = parseQuery(queryString);
    return buildShareCandidate(map);
  }

  const segments = fullPath.split('/').filter(Boolean);
  if (segments.length > 2) {
    const slug = decodeURIComponentSafe(segments[segments.length - 1]);
    if (slug) {
      return buildShareCandidate({ share: slug });
    }
  }

  return { detected: false, candidate: {} as ShareCandidate };
}

async function resolveContextFromCandidate(
  candidate: ShareCandidate
): Promise<ActiveVerificationContext | null> {
  const slugContext = candidate.slug ? decodeLegacySlug(candidate.slug) : null;

  const gatewayValue =
    resolveGateway(candidate.gateway) || resolveGateway(slugContext?.gateway);

  const softwareValue =
    normalizeParam(candidate.software) || normalizeParam(slugContext?.software);
  const codeValue =
    normalizeParam(candidate.softwareCode) || normalizeParam(slugContext?.softwareCode);
  const agentAccountValue =
    normalizeParam(candidate.agentAccount) || normalizeParam(slugContext?.agentAccount);
  const displayValue = normalizeParam(candidate.softwareDisplayName);
  const agentDisplayValue = normalizeParam(candidate.agentDisplayName);

  if (softwareValue && codeValue) {
    return {
      software: softwareValue,
      softwareCode: codeValue,
      agentAccount: agentAccountValue || undefined,
      displayName: displayValue || softwareValue,
      agentDisplayName: agentDisplayValue || undefined,
      gateway: gatewayValue || undefined
    };
  }

  if (!codeValue) {
    return null;
  }

  const remote = await fetchShareContextByCode(codeValue, gatewayValue || undefined);
  if (!remote) {
    return null;
  }

  const remoteSoftware = normalizeParam(remote.software);
  const remoteCode = normalizeParam(remote.softwareCode) || codeValue;
  if (!remoteSoftware) {
    return null;
  }

  return {
    software: remoteSoftware,
    softwareCode: remoteCode,
    agentAccount: agentAccountValue || normalizeParam(remote.agentAccount) || undefined,
    displayName:
      displayValue || normalizeParam(remote.softwareDisplayName) || remoteSoftware,
    agentDisplayName:
      agentDisplayValue || normalizeParam(remote.agentDisplayName) || undefined,
    gateway: gatewayValue || undefined
  };
}

async function fetchShareContextByCode(
  code: string,
  gateway?: string
): Promise<CardVerificationShareContext | null> {
  const normalized = normalizeParam(code);
  if (!normalized) {
    return null;
  }

  const endpoint = gateway
    ? `${gateway}/api/card-verification/context`
    : '/api/card-verification/context';

  try {
    const payload = await apiRequest<CardVerificationShareContext>({
      url: endpoint,
      method: 'GET',
      data: { softwareCode: normalized },
      skipProxy: true,
      auth: false
    });
    if (!payload) {
      return null;
    }
    return payload;
  } catch (error) {
    console.warn('Failed to load verification context', error);
    return null;
  }
}

function applyLinkContext(context: ActiveVerificationContext) {
  const sanitizedGateway = resolveGateway(context.gateway);
  if (sanitizedGateway) {
    defaultGateway.value = sanitizedGateway;
    setBaseURL(sanitizedGateway);
  }

  const nextContext: ActiveVerificationContext = {
    software: context.software,
    softwareCode: context.softwareCode,
    agentAccount: context.agentAccount,
    displayName: context.displayName || context.software,
    agentDisplayName: context.agentDisplayName || context.agentAccount,
    gateway: sanitizedGateway || undefined
  };

  linkContext.value = nextContext;
  shareQuery.value = buildShareQueryString(nextContext);
  verification.value = null;
  shareContextError.value = '';
  code.value = '';
  shareOrigin.value = 'link';
}

function parseQuery(input: string): Record<string, string> {
  const map: Record<string, string> = {};
  if (!input) {
    return map;
  }

  input.split('&').forEach((segment) => {
    if (!segment) {
      return;
    }
    const [rawKey, rawValue = ''] = segment.split('=');
    const key = (rawKey || '').trim();
    if (!key) {
      return;
    }
    const decoded = decodeURIComponentSafe(rawValue);
    map[key] = decoded;
    const lower = key.toLowerCase();
    if (lower !== key && map[lower] == null) {
      map[lower] = decoded;
    }
  });

  return map;
}

function decodeURIComponentSafe(value: any): string {
  if (typeof value !== 'string') {
    return '';
  }
  try {
    return decodeURIComponent(value);
  } catch (error) {
    return value;
  }
}

function normalizeParam(value: any): string {
  if (typeof value !== 'string') {
    return '';
  }
  return value.trim();
}

function buildShareQueryString(context: ActiveVerificationContext): string {
  const parts: string[] = [`software=${encodeURIComponent(context.software)}`];
  if (context.softwareCode) {
    parts.push(`code=${encodeURIComponent(context.softwareCode)}`);
  }
  if (context.agentAccount) {
    parts.push(`agent=${encodeURIComponent(context.agentAccount)}`);
  }
  const gateway = context.gateway || defaultGateway.value;
  const normalizedGateway = resolveGateway(gateway);
  if (normalizedGateway) {
    parts.push(`gateway=${encodeURIComponent(normalizedGateway)}`);
  }
  return parts.join('&');
}

function resolveGateway(raw?: string | null): string {
  if (!raw) {
    return '';
  }
  const trimmed = raw.trim();
  if (!trimmed) {
    return '';
  }
  const lower = trimmed.toLowerCase();
  if (!lower.startsWith('http://') && !lower.startsWith('https://')) {
    return '';
  }
  return trimmed.replace(/\/+$/, '');
}

function decodeLegacySlug(slug: string): ActiveVerificationContext | null {
  if (!slug) {
    return null;
  }
  try {
    let base64 = slug.replace(/-/g, '+').replace(/_/g, '/');
    const padding = base64.length % 4;
    if (padding) {
      base64 = base64.padEnd(base64.length + (4 - padding), '=');
    }

    let buffer: ArrayBuffer;
    if (typeof uni !== 'undefined' && typeof uni.base64ToArrayBuffer === 'function') {
      buffer = uni.base64ToArrayBuffer(base64);
    } else if (typeof atob === 'function') {
      const binary = atob(base64);
      const bytes = new Uint8Array(binary.length);
      for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
      }
      buffer = bytes.buffer;
    } else if (typeof Buffer !== 'undefined') {
      const buf = Buffer.from(base64, 'base64');
      buffer = buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength);
    } else {
      return null;
    }

    const json = typeof TextDecoder !== 'undefined'
      ? new TextDecoder().decode(new Uint8Array(buffer))
      : utf8Decode(new Uint8Array(buffer));
    const raw = JSON.parse(json) ?? {};
    const software = normalizeParam(raw.s || raw.software || raw.softwareName || raw.slot);
    const softwareCode = normalizeParam(raw.c || raw.code || raw.softwareCode || raw.binding);
    const agentAccount = normalizeParam(raw.a || raw.agent || raw.agentAccount);
    const gateway = resolveGateway(normalizeParam(raw.g || raw.gateway));
    if (!software || !softwareCode) {
      return null;
    }
    return {
      software,
      softwareCode,
      agentAccount: agentAccount || undefined,
      gateway: gateway || undefined
    };
  } catch (error) {
    console.warn('Failed to decode legacy share slug', error);
    return null;
  }
}

onLoad(async (options) => {
  shareOrigin.value = null;
  shareQuery.value = '';
  shareContextError.value = '';
  linkContext.value = null;

  const primaryDetection = buildShareCandidate(options);
  let detection = primaryDetection;
  if (!detection.detected) {
    detection = await detectShareCandidateFromRoute();
  }

  if (!detection.detected) {
    return;
  }

  shareOrigin.value = 'link';
  shareContextError.value = '链接解析中，请稍候';

  try {
    const context = await resolveContextFromCandidate(detection.candidate);
    if (context) {
      applyLinkContext(context);
    } else {
      shareContextError.value = '链接信息缺失或已失效，请联系代理重新生成';
    }
  } catch (error) {
    console.warn('Failed to resolve share link context', error);
    shareContextError.value = '解析分享信息失败，请稍后再试';
  }
});

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

const shareHint = computed(() => (isH5 ? '复制链接分享给用户' : '复制或打开小程序路径'));

function utf8Decode(bytes: Uint8Array): string {
  if (typeof TextDecoder !== 'undefined') {
    return new TextDecoder().decode(bytes);
  }
  let result = '';
  for (let i = 0; i < bytes.length; ) {
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

.context-card,
.generator-card {
  display: flex;
  flex-direction: column;
  gap: 24rpx;
  padding: 32rpx;
}

.context-header {
  display: flex;
  flex-direction: column;
  gap: 6rpx;
}

.context-label {
  font-size: 24rpx;
  color: var(--text-muted);
}

.context-value {
  font-size: 30rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.context-sub {
  font-size: 24rpx;
  color: var(--text-secondary);
}

.context-sub.loading {
  color: var(--text-muted);
}

.context-sub.error {
  color: #ff6b6b;
}

.context-share {
  display: grid;
  gap: 12rpx;
}

.share-row {
  display: flex;
  align-items: center;
  gap: 16rpx;
}

.share-label {
  font-size: 24rpx;
  color: var(--text-muted);
}

.share-url {
  flex: 1;
  font-size: 26rpx;
  color: var(--accent-color);
  word-break: break-all;
}

.share-btn {
  padding: 12rpx 20rpx;
  border-radius: 999rpx;
  font-size: 24rpx;
  background: var(--surface-light);
  color: var(--text-primary);
}

.share-btn.ghost {
  background: transparent;
  border: 1rpx solid rgba(56, 189, 248, 0.35);
  color: var(--accent-color);
}

.share-hint {
  font-size: 22rpx;
  color: var(--text-muted);
}

.generator-card .section-title {
  font-size: 28rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.field {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.field-label {
  font-size: 24rpx;
  color: var(--text-muted);
}

.picker-value {
  padding: 18rpx 22rpx;
  border-radius: 18rpx;
  background: var(--surface-light);
  border: 1rpx solid var(--outline-color);
  color: var(--text-primary);
  font-size: 26rpx;
}

.generator-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 16rpx;
}

.share-preview {
  font-size: 24rpx;
  color: var(--text-secondary);
  word-break: break-all;
}

.secondary {
  padding: 14rpx 28rpx;
  border-radius: 999rpx;
  border: none;
  font-size: 26rpx;
  font-weight: 500;
  color: var(--text-primary);
  background: rgba(255, 255, 255, 0.08);
}

.secondary.ghost {
  border: 1rpx solid rgba(255, 255, 255, 0.16);
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
