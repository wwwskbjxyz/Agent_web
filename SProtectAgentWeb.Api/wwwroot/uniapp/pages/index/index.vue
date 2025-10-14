<template>
  <view class="page">
    <view class="hero glass-card">
      <view class="hero-text">
        <text class="hero-badge">SProtect · 代理后台中心</text>
        <text class="hero-title">代理监控面板</text>
        <text class="hero-subtitle">
          实时掌握软件激活、代理活跃度与卡密发放情况，支持桌面与移动端自适配。
        </text>
      </view>
      <view class="hero-meta">
        <view class="meta-item">
          <text class="meta-label">绑定软件码</text>
          <picker
            mode="selector"
            :range="bindingLabels"
            :value="bindingIndex"
            :disabled="bindingDisabled"
            @change="onBindingChange"
          >
            <view :class="['picker-display', bindingDisabled ? 'picker-disabled' : '']">
              {{ currentBindingLabel }}
            </view>
          </picker>
        </view>
        <view class="meta-item">
          <text class="meta-label">当前软件位</text>
          <SoftwarePicker />
        </view>
        <view class="meta-item theme-item">
          <text class="meta-label">主题样式</text>
          <picker mode="selector" :range="themeLabels" :value="themeIndex" @change="onThemeChange">
            <view class="picker-display">{{ themeLabels[themeIndex] }}</view>
          </picker>
        </view>
        <view class="meta-item account-item">
          <text class="meta-label">登录账号</text>
          <view class="account-info">
            <view class="account-avatar">{{ profileInitial }}</view>
            <text class="meta-value">{{ profile?.name ?? '未登录' }}</text>
          </view>
        </view>
        <view class="meta-item role-item">
          <text class="meta-label">角色权限</text>
          <view class="role-row">
            <text class="meta-value">{{ profile?.role ?? '访客' }}</text>
            <button class="meta-button ghost" :disabled="isLoggingOut" @tap="handleLogout">
              {{ isLoggingOut ? '正在退出...' : '退出登录' }}
            </button>
          </view>
        </view>
        <!-- #ifdef MP-WEIXIN -->
        <view class="meta-item wechat-item" v-if="isWeChatMiniProgram">
          <view class="wechat-header">
            <text class="meta-label">微信订阅</text>
            <view class="wechat-status-group">
              <text class="wechat-status">{{ wechatStatusText }}</text>
              <view v-if="wechatDisplayName" class="wechat-chip">{{ wechatDisplayName }}</view>
            </view>
          </view>
          <view class="wechat-actions">
            <button class="meta-button" :disabled="wechatBusy" @tap="handleWeChatBind">
              {{ wechatBindLabel }}
            </button>
            <button
              v-if="isWeChatBound"
              class="meta-button ghost"
              :disabled="wechatBusy"
              @tap="handleWeChatUnbind"
            >
              解绑
            </button>
          </view>
          <view v-if="showSubscriptionPanel" class="wechat-subscription">
            <view v-for="option in wechatTemplateOptions" :key="option.key" class="subscription-row">
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
            <button
              class="meta-button primary"
              :disabled="wechatSubscribeBusy"
              @tap="applySubscriptionRequests()"
            >
              {{ wechatSubscribeBusy ? '授权中...' : '保存并请求订阅' }}
            </button>
            <text class="subscription-hint">提示：仅对已开启的提醒类型发起订阅，可随时调整。</text>
          </view>
          <view v-else class="wechat-subscription empty">
            <text v-if="isWeChatBound">未配置订阅模板，请联系平台管理员设置模板 ID。</text>
            <text v-else>绑定微信后可开启通知提醒。</text>
          </view>
        </view>
        <!-- #endif -->
      </view>
    </view>

    <view class="quick-nav glass-card">
      <view class="quick-nav-title">快捷导航</view>
      <view class="quick-nav-grid">
        <view
          v-for="item in quickNavItems"
          :key="item.path"
          class="quick-nav-item"
          @tap="goTo(item.path)"
        >
          <view
            v-if="item.badge"
            class="quick-nav-badge"
            :class="`badge-${item.badge.type}`"
          >
            <view class="badge-main">
              <text class="badge-text">{{ item.badge.label }}</text>
              <text v-if="item.badge.value" class="badge-count">{{ item.badge.value }}</text>
            </view>
            <text v-if="item.badge.subtext" class="badge-subtext">{{ item.badge.subtext }}</text>
          </view>
          <view class="quick-nav-item-head">{{ item.label }}</view>
          <view class="quick-nav-item-desc">{{ item.description }}</view>
        </view>
      </view>
    </view>

    <view v-if="systemInfo" class="system glass-card">
      <view class="system-header">
        <text class="system-title">服务器状态</text>
        <text class="system-subtitle">{{ systemInfo.machine }} · {{ systemInfo.os }}</text>
      </view>
      <view class="system-grid">
        <view class="system-item">
          <text class="system-label">CPU 负载</text>
          <text class="system-value">{{ systemInfo.cpu }}</text>
        </view>
        <view class="system-item">
          <text class="system-label">内存占用</text>
          <text class="system-value">{{ systemInfo.memory }}</text>
        </view>
        <view class="system-item">
          <text class="system-label">运行时长</text>
          <text class="system-value">{{ systemInfo.uptime }}</text>
        </view>
        <view class="system-item">
          <text class="system-label">最近启动</text>
          <text class="system-value">{{ systemInfo.boot }}</text>
        </view>
      </view>
      <view v-if="systemInfo.warnings.length" class="system-warning">
        <text v-for="(warning, index) in systemInfo.warnings" :key="index">{{ warning }}</text>
      </view>
    </view>

    <view class="stat-grid">
      <StatCard
        v-for="item in displayStats"
        :key="item.key"
        :title="item.label"
        :value="item.value"
        :delta="item.delta"
        :trend="item.trend"
      />
    </view>

    <TrendChart

      title="过去 7 日总体态势"
      subtitle="包含封禁"

      :points="trendPoints"
      :total="trendTotal"
      total-label="最近7天总计"
      :max-value="trendMax"
    >
      <template #extra>
        <view class="chart-extra">单位：综合评分</view>
      </template>
    </TrendChart>

    <SubTrendChart
      title="最近子代理 7 天激活趋势"
      subtitle="洞察各下级代理激活情况"
      :categories="subTrendData.categories"
      :series="subTrendData.series"
      :total="subTrendData.total"
    />

    <view class="panel glass-card sales-panel">
      <view class="panel-header">
        <text class="panel-title">销量查询</text>
        <text class="panel-subtitle">按卡种、状态、代理多条件筛选激活记录</text>
      </view>
      <view class="sales-form">
        <view class="form-field">
          <text class="form-label">卡种</text>
          <picker mode="selector" :range="cardTypeOptions" :value="cardTypeIndex" @change="onCardTypeChange">
            <view class="picker-display">{{ cardTypeOptions[cardTypeIndex] }}</view>
          </picker>
        </view>
        <view class="form-field">
          <text class="form-label">状态</text>
          <picker mode="selector" :range="statusOptions" :value="statusIndex" @change="onStatusChange">
            <view class="picker-display">{{ statusOptions[statusIndex] }}</view>
          </picker>
        </view>
        <view class="form-field">
          <text class="form-label">代理</text>
          <picker mode="selector" :range="agentOptions" :value="agentIndex" @change="onAgentChange">
            <view class="picker-display">{{ agentOptions[agentIndex] }}</view>
          </picker>
        </view>
        <view class="form-field switch-field">
          <text class="form-label">包含下级</text>
          <switch :checked="salesForm.includeDescendants" @change="(e) => (salesForm.includeDescendants = e.detail.value)" />
        </view>
        <view class="form-field">
          <text class="form-label">开始时间</text>
          <DateTimePicker v-model="salesForm.startTime" placeholder="选择开始时间" />
        </view>
        <view class="form-field">
          <text class="form-label">结束时间</text>
          <DateTimePicker v-model="salesForm.endTime" placeholder="选择结束时间" />
        </view>
      </view>
      <view class="sales-actions">
        <button class="btn primary" :disabled="salesLoading" @tap="submitSalesQuery">
          {{ salesLoading ? '查询中...' : '查询' }}
        </button>
        <button class="btn ghost" :disabled="salesLoading" @tap="resetSalesForm">重置</button>
      </view>
      <view class="sales-result" v-if="!salesLoading">
        <view class="sales-summary">
          <text class="sales-count">激活数量：<text class="highlight">{{ salesResult.count }}</text></text>
          <text class="sales-total">结算总额：<text class="highlight">￥{{ formatCurrency(salesTotalAmount) }}</text></text>
          <text class="sales-meta">{{ salesSummary }}</text>
        </view>
        <view v-if="salesSettlements.length" class="sales-settlement">
          <view class="settlement-header">
            <text class="settlement-col type">卡种</text>
            <text class="settlement-col count">激活数量</text>
            <text class="settlement-col price">结算单价</text>
            <text class="settlement-col total">结算金额</text>
          </view>
          <view v-for="item in salesSettlements" :key="item.cardType" class="settlement-row">
            <text class="settlement-col type">{{ item.cardType }}</text>
            <text class="settlement-col count">{{ item.count }}</text>
            <text class="settlement-col price">￥{{ formatCurrency(item.price) }}</text>
            <text class="settlement-col total">￥{{ formatCurrency(item.total) }}</text>
          </view>
        </view>
        <view v-if="!salesResult.cards.length" class="panel-empty">暂无数据</view>
        <view v-else class="sales-list">
          <view
            v-for="item in paginatedSales"
            :key="`${item.card}-${item.activateTime}`"
            class="sales-entry glass-light"
          >
            <view class="sales-field">
              <text class="sales-label">卡密</text>
              <text class="sales-value copyable" @longpress="copyValue(item.card)">{{ item.card }}</text>
            </view>
            <view class="sales-field">
              <text class="sales-label">激活时间</text>
              <text class="sales-value copyable" @longpress="copyValue(item.activateTime || '-')">
                {{ item.activateTime || '-' }}
              </text>
            </view>
          </view>
          <view class="sales-pagination" v-if="salesTotalPages > 1">
            <view class="sales-page-info">第 {{ salesPage }} / {{ salesTotalPages }} 页</view>
            <view class="sales-page-actions">
              <button class="page-btn" :disabled="salesPage <= 1 || salesLoading" @tap="changeSalesPage(salesPage - 1)">
                上一页
              </button>
              <button class="page-btn" :disabled="salesPage >= salesTotalPages || salesLoading" @tap="changeSalesPage(salesPage + 1)">
                下一页
              </button>
            </view>
          </view>
        </view>
      </view>
      <view v-else class="panel-empty">查询中...</view>
    </view>

    <view class="layout-grid">
      <view class="panel glass-card">
        <view class="panel-header">
          <text class="panel-title">代理中心公告</text>
          <text class="panel-subtitle">与渠道共享最新巡检、版本升级信息</text>
        </view>
        <view v-if="loadingDashboard" class="panel-empty">公告加载中...</view>
        <view v-else-if="!announcements.length" class="panel-empty">暂无公告</view>
        <view v-else class="announcement-list">
          <view v-for="item in announcements" :key="item.id" class="announcement-item">
            <text class="announcement-title">{{ item.title }}</text>
            <text class="announcement-content">{{ item.content }}</text>
            <view class="announcement-meta">
              <text>{{ item.updatedAt }}</text>
            </view>
          </view>
        </view>
      </view>

      <view class="panel glass-card">
        <view class="panel-header">
          <text class="panel-title">激活地区排行榜</text>
          <text class="panel-subtitle">核心地区授权分布情况</text>
        </view>
        <view v-if="loadingDashboard" class="panel-empty">数据同步中...</view>
        <view v-else-if="!heatmap.length" class="panel-empty">暂无节点统计</view>
        <view class="heatmap-grid" v-else>
          <view v-for="city in heatmap" :key="city.name" class="heatmap-item glass-light">
            <text class="heatmap-name">{{ city.name }}</text>
            <view class="heatmap-stats">
              <view>
                <text class="heatmap-label">激活量</text>
                <text class="heatmap-value">{{ city.count }}</text>
              </view>
              <view>
                <text class="heatmap-label">占比</text>
                <text class="heatmap-value">{{ city.percentage }}%</text>
              </view>
            </view>
          </view>
        </view>
      </view>
    </view>

  </view>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import TrendChart from '@/components/TrendChart.vue';
import SubTrendChart from '@/components/SubTrendChart.vue';
import StatCard from '@/components/StatCard.vue';
import SoftwarePicker from '@/components/SoftwarePicker.vue';
import DateTimePicker from '@/components/DateTimePicker.vue';
import { useAppStore } from '@/stores/app';
import { usePlatformStore } from '@/stores/platform';
import { formatDateTime } from '@/utils/time';
import type { ThemePreference } from '@/utils/storage';
import type { PlatformBinding } from '@/common/types';

const appStore = useAppStore();
const {
  dashboard,
  systemStatus,
  profile,
  selectedSoftware,
  cardTypes,
  agents,
  theme,
  chatUnreadCount,
  hasNewBlacklistLogs,
  settlementHasReminder
} = storeToRefs(appStore);
const platformStore = usePlatformStore();
const { agentWeChatBinding, agentWechatSubscriptions, wechatTemplates, wechatTemplatePreviews } = storeToRefs(platformStore);

const isWeChatMiniProgram = ref(false);
// #ifdef MP-WEIXIN
isWeChatMiniProgram.value = true;
// #endif

interface WeChatTemplateOption {
  key: 'instant' | 'blacklist' | 'settlement';
  title: string;
  description: string;
  templateId: string;
  preview?: Record<string, string> | null;
}

const isWeChatBound = computed(() => Boolean(agentWeChatBinding.value));
const wechatBusy = computed(
  () =>
    platformStore.loading.wechatBinding ||
    platformStore.loading.wechatBind ||
    platformStore.loading.wechatUnbind
);

const wechatTemplateOptions = computed<WeChatTemplateOption[]>(() => {
  const config = wechatTemplates.value;
  if (!config) {
    return [];
  }
  const candidates: WeChatTemplateOption[] = [
    {
      key: 'instant',
      title: '即时沟通提醒',
      description: '客户有新留言或待处理消息时提醒',
      templateId: config.instantCommunication?.trim() ?? '',
      preview: wechatTemplatePreviews.value?.['instant'] ?? null
    },
    {
      key: 'blacklist',
      title: '黑名单严重警告',
      description: '黑名单记录触发时第一时间通知',
      templateId: config.blacklistAlert?.trim() ?? '',
      preview: wechatTemplatePreviews.value?.['blacklist'] ?? null
    },
    {
      key: 'settlement',
      title: '结算账单通知',
      description: '结算周期到期后提醒待对账金额',
      templateId: config.settlementNotice?.trim() ?? '',
      preview: wechatTemplatePreviews.value?.['settlement'] ?? null
    }
  ];
  return candidates.filter((item) => item.templateId.length >= 10 && !item.templateId.includes('...'));
});

const subscriptionState = computed<Record<string, boolean>>(() => agentWechatSubscriptions.value ?? {});
const showSubscriptionPanel = computed(() => isWeChatBound.value && wechatTemplateOptions.value.length > 0);

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
  const binding = agentWeChatBinding.value;
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
  if (wechatBusy.value && !agentWeChatBinding.value) {
    return '加载中...';
  }
  return isWeChatBound.value ? '已绑定' : '未绑定';
});
const wechatSubscribeBusy = ref(false);

function onSubscriptionToggle(key: string, value: boolean) {
  platformStore.setAgentWechatSubscription(key, value);
}

async function applySubscriptionRequests(auto = false) {
  if (!isWeChatMiniProgram.value) {
    if (!auto) {
      uni.showToast({ title: '请在微信小程序内操作', icon: 'none' });
    }
    return;
  }

  await platformStore.fetchWeChatTemplates().catch(() => undefined);
  const selected = wechatTemplateOptions.value.filter((option) => (subscriptionState.value[option.key] ?? true));
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
      const delivered = await platformStore.sendWeChatPreview('agent', option.key, auto);
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
  if (!isWeChatBound.value || !wechatTemplateOptions.value.length) {
    return;
  }
  const current = subscriptionState.value;
  if (current && Object.keys(current).length > 0) {
    return;
  }
  wechatTemplateOptions.value.forEach((option) => {
    platformStore.setAgentWechatSubscription(option.key, true);
  });
}

const bindingLoginBusy = ref(false);
const bindingLabels = computed(() =>
  platformStore.bindings.map((item) => {
    const mainLabel = item.authorDisplayName?.trim();
    if (mainLabel) {
      return mainLabel;
    }
    if (item.softwareType) {
      return `${item.softwareType} (${item.softwareCode})`;
    }
    return item.softwareCode;
  })
);

const bindingIndex = computed(() => {
  if (!platformStore.selectedBinding) {
    return 0;
  }
  const index = platformStore.bindings.findIndex(
    (binding) => binding.bindingId === platformStore.selectedBinding?.bindingId
  );
  return index >= 0 ? index : 0;
});

const currentBindingLabel = computed(() => {
  if (!platformStore.bindings.length) {
    return '尚未绑定软件码';
  }
  if (!platformStore.selectedBinding) {
    return bindingLabels.value[bindingIndex.value] ?? '请选择软件码';
  }
  if (!bindingLabels.value.length) {
    const fallback = platformStore.selectedBinding;
    if (!fallback) {
      return '请选择软件码';
    }
    return fallback.authorDisplayName || fallback.softwareCode;
  }
  return bindingLabels.value[bindingIndex.value] ?? bindingLabels.value[0];
});

const bindingDisabled = computed(
  () => !platformStore.bindings.length || bindingLoginBusy.value || platformStore.loading.bindings
);

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
  if (!isWeChatMiniProgram.value || !platformStore.isAuthenticated) {
    return;
  }
  await platformStore.fetchWeChatBinding('agent').catch(() => undefined);
}

watch(
  () => [agentWeChatBinding.value?.openId ?? null, wechatTemplateOptions.value.length],
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
      console.warn('获取用户昵称失败，继续绑定', error);
    }

    await platformStore.bindWeChat('agent', code, nickname);
    await platformStore.fetchWeChatTemplates().catch(() => undefined);
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
    await platformStore.unbindWeChat('agent');
    uni.showToast({ title: '已解绑', icon: 'success' });
  } catch (error) {
    const message =
      (error as any)?.errMsg || (error as any)?.message || '解绑失败，请稍后重试';
    uni.showToast({ title: message, icon: 'none' });
  }
}

const stats = computed(() => dashboard.value.stats ?? []);
const displayStats = computed(() =>
  stats.value.map((item) => ({
    key: item.key,
    label: item.label,
    value: item.value,
    delta: 0,
    trend: 'flat' as const
  }))
);

const trendPoints = computed(() => dashboard.value.trend ?? []);
const trendTotal = computed(() => dashboard.value.trendTotal ?? 0);
const trendMax = computed(() => Math.max(0, Math.ceil(dashboard.value.trendMax ?? 0)));
const subTrendData = computed(() => dashboard.value.subAgentTrend ?? { categories: [], series: [], total: 0 });
const announcements = computed(() => dashboard.value.announcements ?? []);
const heatmap = computed(() => dashboard.value.usageHeatmap ?? []);

const profileInitial = computed(() => {
  const name = profile.value?.name?.trim();
  if (!name) {
    return '访';
  }
  return name.charAt(0).toUpperCase();
});

const loadingDashboard = computed(() => appStore.loading.dashboard);
const salesLoading = computed(() => appStore.loading.sales);
const isLoggingOut = computed(() => appStore.loading.logout);

const salesResult = computed(() => dashboard.value.salesResult ?? { count: 0, cards: [] });
const salesSettlements = computed(() => salesResult.value.settlements ?? []);
const salesTotalAmount = computed(() => Number(salesResult.value.totalAmount ?? 0));
const SALES_PAGE_SIZE = 20;
const salesPage = ref(1);
const paginatedSales = computed(() => {
  const items = salesResult.value.cards || [];
  const start = (salesPage.value - 1) * SALES_PAGE_SIZE;
  return items.slice(start, start + SALES_PAGE_SIZE);
});
const salesTotalPages = computed(() => {
  const total = salesResult.value.cards?.length ?? 0;
  return Math.max(1, Math.ceil(total / SALES_PAGE_SIZE));
});
const salesSummary = computed(() => {
  const filters = dashboard.value.salesFilters;
  if (!filters) {
    return '';
  }
  const rangeStart = filters.startTime ?? '-';
  const rangeEnd = filters.endTime ?? '-';
  const agent = filters.agent ? filters.agent : '全部';
  return `时间：${rangeStart || '-'} ~ ${rangeEnd || '-'}，代理：${agent || '全部'}`;
});

const cardTypeOptions = computed(() => ['全部', ...cardTypes.value.map((item) => item.name)]);
const statusOptions = ['全部', '启用', '禁用'];
const agentOptions = computed(() => ['全部', ...agents.value.map((item) => item.username)]);

const cardTypeIndex = ref(0);
const statusIndex = ref(0);
const agentIndex = ref(0);

const salesForm = reactive({
  cardType: '',
  status: '',
  agent: '',
  includeDescendants: true,
  startTime: '',
  endTime: ''
});

const themeOptions: Array<{ label: string; value: ThemePreference }> = [
  { label: '暗夜', value: 'dark' },
  { label: '明亮', value: 'light' },
  { label: '卡通', value: 'cartoon' }
];
const themeLabels = themeOptions.map((item) => item.label);
const themeIndex = ref(0);

interface QuickLinkItem {
  label: string;
  description: string;
  path: string;
}

interface QuickLinkBadge {
  type: 'info' | 'alert';
  label: string;
  value?: string;
  subtext?: string;
}

type QuickNavItem = QuickLinkItem & { badge?: QuickLinkBadge };

const baseQuickLinks: QuickLinkItem[] = [
  { label: '软件绑定管理', description: '新增、修改与删除绑定软件码', path: '/pages/agent/bind' },
  { label: '结算设置', description: '为当前账号维护各卡密类型的结算价格', path: '/pages/agent/settlement' },
  { label: '卡密管理', description: '搜索、启用禁用与解绑操作', path: '/pages/cards/index' },
  { label: '代理管理', description: '封禁启用、加款与权限配置', path: '/pages/agents/index' },
  { label: '即时沟通', description: '与渠道实时协同巡检', path: '/pages/chat/index' },
  { label: '卡密验证', description: '批量验证与下载链接', path: '/pages/verify/index' },
  { label: '黑名单记录', description: '查询黑名单触发日志', path: '/pages/blacklist/logs/index' },
  { label: '黑名单机器码', description: '管理封禁机器码', path: '/pages/blacklist/machines/index' }
];

const quickNavItems = computed<QuickNavItem[]>(() => {
  const unread = chatUnreadCount.value;
  const hasBlacklistAlert = hasNewBlacklistLogs.value;

  return baseQuickLinks.map((item) => {
    const next: QuickNavItem = { ...item };
    if (item.path === '/pages/chat/index' && unread > 0) {
      next.badge = {
        type: 'info',
        label: '新消息',
        value: unread > 99 ? '99+' : String(unread)
      };
    } else if (item.path === '/pages/agent/settlement' && settlementHasReminder.value) {
      next.badge = {
        type: 'alert',
        label: '待结算',
        subtext: '请尽快处理'
      };
    } else if (item.path === '/pages/blacklist/logs/index' && hasBlacklistAlert) {
      next.badge = {
        type: 'alert',
        label: '严重警告',
        subtext: '有人尝试破解'
      };
    }
    return next;
  });
});

const systemInfo = computed(() => {
  const info = systemStatus.value;
  if (!info) return null;
  return {
    machine: info.machineName || '未识别主机',
    os: info.osDescription || '未知系统',
    cpu: info.cpuLoadPercentage != null ? `${info.cpuLoadPercentage.toFixed(1)}%` : '--',
    memory: info.memoryUsagePercentage != null ? `${info.memoryUsagePercentage.toFixed(1)}%` : '--',
    uptime: formatDuration(info.uptimeSeconds),
    boot: info.bootTime ? formatDateTime(info.bootTime) : '--',
    warnings: Array.isArray(info.warnings) ? info.warnings : []
  };
});

function formatPickerValue(date: Date) {
  const pad = (value: number) => value.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function normalizePickerValue(value?: string | null) {
  if (!value) return '';
  const trimmed = value.trim();
  if (!trimmed) return '';
  if (/^\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}$/.test(trimmed)) {
    return trimmed;
  }
  const parsed = new Date(trimmed.replace('T', ' ').replace(/\//g, '-'));
  if (Number.isNaN(parsed.getTime())) {
    return '';
  }
  return formatPickerValue(parsed);
}

function formatDateTimeForApi(value: string) {
  return normalizePickerValue(value);
}

function formatCurrency(value?: number) {
  const amount = Number(value ?? 0);
  if (!Number.isFinite(amount)) {
    return '0.00';
  }
  return amount.toFixed(2);
}

function applySalesFilters(filters?: Partial<ReturnType<typeof dashboard.value['salesFilters']>>) {
  const now = new Date();
  const defaultStart = new Date(now.getTime() - 6 * 24 * 60 * 60 * 1000);
  salesForm.cardType = filters?.cardType ?? '';
  salesForm.status = filters?.status ?? '';
  salesForm.agent = filters?.agent ?? '';
  salesForm.includeDescendants = filters?.includeDescendants ?? true;
  salesForm.startTime = normalizePickerValue(filters?.startTime) || formatPickerValue(defaultStart);
  salesForm.endTime = normalizePickerValue(filters?.endTime) || formatPickerValue(now);

  cardTypeIndex.value = Math.max(0, cardTypeOptions.value.indexOf(salesForm.cardType || '全部'));
  statusIndex.value = Math.max(0, statusOptions.indexOf(salesForm.status || '全部'));
  agentIndex.value = Math.max(0, agentOptions.value.indexOf(salesForm.agent || '全部'));

  salesPage.value = 1;
}

function copyValue(value?: string) {
  const text = (value ?? '').toString().trim();
  if (!text) {
    uni.showToast({ title: '无可复制内容', icon: 'none' });
    return;
  }
  uni.setClipboardData({
    data: text,
    success: () => {
      uni.showToast({ title: '已复制', icon: 'success', duration: 800 });
    },
    fail: () => {
      uni.showToast({ title: '复制失败', icon: 'none' });
    }
  });
}

function changeSalesPage(next: number) {
  if (next < 1 || next > salesTotalPages.value || next === salesPage.value) {
    return;
  }
  salesPage.value = next;
}

function onThemeChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value);
  const option = themeOptions[index] ?? themeOptions[0];
  themeIndex.value = index;
  appStore.setTheme(option.value);
}

async function handleLogout() {
  if (isLoggingOut.value) {
    return;
  }
  try {
    await appStore.logout();
  } catch (error) {
    console.error('退出登录失败', error);
    uni.showToast({ title: '退出失败，请重试', icon: 'none' });
  }
}

function resetSalesForm() {
  applySalesFilters({ includeDescendants: true });
  appStore.clearSalesResult({
    includeDescendants: salesForm.includeDescendants,
    cardType: '',
    status: '',
    agent: '',
    startTime: salesForm.startTime,
    endTime: salesForm.endTime
  });
}

async function submitSalesQuery() {
  if (!selectedSoftware.value) {
    uni.showToast({ title: '请先选择软件位', icon: 'none' });
    return;
  }

  try {
    const startTime = formatDateTimeForApi(salesForm.startTime);
    const endTime = formatDateTimeForApi(salesForm.endTime);
    await appStore.runSalesQuery({
      cardType: salesForm.cardType || undefined,
      status: salesForm.status || undefined,
      agent: salesForm.agent || undefined,
      includeDescendants: salesForm.includeDescendants,
      startTime: startTime || undefined,
      endTime: endTime || undefined
    });
    uni.showToast({ title: '查询完成', icon: 'success' });
  } catch (error) {
    console.error('submitSalesQuery error', error);
    uni.showToast({ title: '查询失败', icon: 'none' });
  }
}

function onCardTypeChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value);
  cardTypeIndex.value = index;
  salesForm.cardType = index === 0 ? '' : cardTypeOptions.value[index];
}

function onStatusChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value);
  statusIndex.value = index;
  salesForm.status = index === 0 ? '' : statusOptions[index];
}

function onAgentChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value);
  agentIndex.value = index;
  salesForm.agent = index === 0 ? '' : agentOptions.value[index];
}

async function loginWithBinding(binding: PlatformBinding, options?: { silent?: boolean }) {
  if (!binding.authorAccount || !binding.authorPassword) {
    return;
  }
  bindingLoginBusy.value = true;
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
          : '同步作者登录失败';
      uni.showToast({ title: message, icon: 'none' });
    }
    throw error;
  } finally {
    bindingLoginBusy.value = false;
  }
}

function onBindingChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value ?? bindingIndex.value);
  const binding = platformStore.bindings[index];
  if (!binding) {
    return;
  }
  if (platformStore.selectedBinding?.bindingId === binding.bindingId) {
    return;
  }
  platformStore.selectBinding(binding);
  loginWithBinding(binding, { silent: true })
    .then(() => {
      uni.showToast({ title: `已切换 ${binding.softwareCode}`, icon: 'success' });
    })
    .catch(() => {
      /* 提示已在 loginWithBinding 处理 */
    });
}

onMounted(async () => {
  if (!platformStore.isAuthenticated) {
    uni.reLaunch({ url: '/pages/login/agent' });
    return;
  }

  await ensureWeChatBinding();

  if (isWeChatMiniProgram.value) {
    platformStore.fetchWeChatTemplates().catch(() => undefined);
  }

  if (!platformStore.bindings.length) {
    await platformStore.fetchAgentProfile();
  }

  if (!platformStore.selectedBinding && platformStore.bindings.length) {
    platformStore.selectBinding(platformStore.bindings[0]);
  }

  if (!platformStore.selectedBinding) {
    uni.showModal({
      title: '尚未绑定作者',
      content: '请先绑定软件码后再访问控制台。',
      showCancel: false,
      success: () => {
        uni.navigateTo({ url: '/pages/agent/bind' });
      }
    });
    return;
  }

  try {
    await loginWithBinding(platformStore.selectedBinding, { silent: true });
  } catch (error) {
    console.warn('自动同步作者凭证失败', error);
  }

  await appStore.ensureReady();
  await appStore.loadDashboard();
  await Promise.all([
    appStore.loadCardTypes(),
    appStore.loadAgents({ limit: 200 }),
    appStore.loadChatSessions().catch((error) => {
      console.warn('加载即时沟通会话失败', error);
    }),
    appStore.loadBlacklistLogs(100).catch((error) => {
      console.warn('加载黑名单日志失败', error);
    })
  ]);
  applySalesFilters(dashboard.value.salesFilters ?? { includeDescendants: true });
});

watch(
  () => selectedSoftware.value,
  (next, prev) => {
    if (!next || next === prev) return;
    appStore.loadDashboard();
    appStore.loadCardTypes(true);
    appStore.loadAgents({ limit: 200, page: 1, keyword: '' });
    appStore.loadChatSessions().catch((error) => {
      console.warn('切换软件时加载会话失败', error);
    });
    appStore.loadBlacklistLogs(100).catch((error) => {
      console.warn('切换软件时加载黑名单日志失败', error);
    });
    resetSalesForm();
  }
);

watch(
  () => dashboard.value.salesFilters,
  (filters) => {
    if (!filters) {
      resetSalesForm();
      return;
    }
    applySalesFilters(filters);
  },
  { immediate: true }
);

watch(
  () => dashboard.value.salesResult,
  () => {
    salesPage.value = 1;
  }
);

watch(
  theme,
  (next) => {
    const idx = themeOptions.findIndex((item) => item.value === next);
    themeIndex.value = idx >= 0 ? idx : 0;
  },
  { immediate: true }
);

function formatDuration(seconds?: number | string | null) {
  if (seconds == null) return '—';
  const total = Number(seconds);
  if (!Number.isFinite(total) || total <= 0) return '—';
  const days = Math.floor(total / 86400);
  const hours = Math.floor((total % 86400) / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  if (days > 0) {
    return `${days}天${hours}小时`;
  }
  if (hours > 0) {
    return `${hours}小时${minutes}分钟`;
  }
  const secs = Math.floor(total % 60);
  return `${minutes}分钟${secs}秒`;
}

function goAgents() {
  uni.navigateTo({ url: '/pages/agents/index' });
}

function goTo(path: string) {
  uni.navigateTo({ url: path });
}
</script>

<style scoped lang="scss">
.page {
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 40rpx 28rpx 80rpx;
}

.hero {
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 40rpx 36rpx;

  @media (min-width: 900px) {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
  }
}

.hero-text {
  max-width: 620rpx;
  display: flex;
  flex-direction: column;
  gap: 18rpx;
}

.hero-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 12rpx 24rpx;
  border-radius: 999rpx;
  background: rgba(139, 92, 246, 0.18);
  color: rgba(139, 92, 246, 0.95);
  font-size: 22rpx;
  letter-spacing: 0.08em;
}

.hero-title {
  font-size: 44rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.hero-subtitle {
  font-size: 26rpx;
  color: var(--text-muted);
}

.quick-nav {
  display: flex;
  flex-direction: column;
  gap: 24rpx;
  padding: 28rpx 32rpx;
}

.quick-nav-title {
  font-size: 28rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.quick-nav-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220rpx, 1fr));
  gap: 18rpx;
}

.quick-nav-item {
  padding: 48rpx 28rpx 24rpx;
  border-radius: 24rpx;
  background: var(--surface-light);
  border: 1rpx solid var(--outline-color);
  display: flex;
  flex-direction: column;
  gap: 10rpx;
  position: relative;
  overflow: visible;
}

.quick-nav-item-head {
  font-size: 28rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.quick-nav-item-desc {
  font-size: 24rpx;
  color: var(--text-muted);
  line-height: 1.4;
}

.quick-nav-badge {
  position: absolute;
  top: 0;
  left: 50%;
  transform: translate(-50%, -60%);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8rpx;
  pointer-events: none;
}

.badge-main {
  display: inline-flex;
  align-items: center;
  gap: 8rpx;
  padding: 10rpx 24rpx;
  border-radius: 999rpx;
  box-shadow: 0 8rpx 24rpx rgba(15, 23, 42, 0.25);
}

.badge-text {
  font-size: 24rpx;
  font-weight: 600;
}

.badge-count {
  font-size: 22rpx;
  font-weight: 700;
}

.badge-subtext {
  font-size: 22rpx;
  font-weight: 600;
  text-align: center;
}

.badge-info .badge-main {
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.95), rgba(129, 140, 248, 0.95));
  color: #fff;
}

.badge-alert .badge-main {
  background: linear-gradient(135deg, rgba(248, 113, 113, 0.95), rgba(239, 68, 68, 0.95));
  color: #fff;
}

.badge-alert .badge-subtext {
  color: rgba(248, 113, 113, 0.95);
}

.hero-meta {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
  gap: 18rpx;

  @media (min-width: 768px) {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}

.account-info {
  display: flex;
  align-items: center;
  gap: 16rpx;
}

.account-avatar {
  width: 48rpx;
  height: 48rpx;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24rpx;
  font-weight: 600;
  color: #0f172a;
  background: linear-gradient(135deg, rgba(96, 165, 250, 0.9), rgba(129, 140, 248, 0.9));
}

.role-row {
  display: flex;
  align-items: center;
  gap: 16rpx;
  flex-wrap: wrap;
}

.meta-item {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
}

.wechat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.wechat-status-group {
  display: flex;
  align-items: center;
  gap: 12rpx;
}

.wechat-status {
  font-size: 26rpx;
  color: var(--text-muted);
}

.wechat-chip {
  padding: 6rpx 18rpx;
  border-radius: 999rpx;
  background: rgba(14, 165, 233, 0.15);
  color: #0ea5e9;
  font-size: 24rpx;
  font-weight: 500;
}

.wechat-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12rpx;
  align-items: center;
  margin-top: 12rpx;
}

.wechat-subscription {
  margin-top: 16rpx;
  padding: 18rpx;
  border-radius: 18rpx;
  background: rgba(15, 23, 42, 0.3);
  border: 1rpx solid rgba(148, 163, 184, 0.25);
  display: flex;
  flex-direction: column;
  gap: 16rpx;
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
  color: var(--text-primary);
}

.subscription-desc {
  font-size: 22rpx;
  color: var(--text-muted);
}

.subscription-switch {
  transform: scale(0.9);
}

.subscription-hint {
  font-size: 22rpx;
  color: var(--text-muted);
}

.wechat-subscription.empty {
  margin-top: 16rpx;
  font-size: 24rpx;
  color: var(--text-muted);
  line-height: 1.5;
}

.picker-display {
  padding: 16rpx 24rpx;
  border-radius: 18rpx;
  background: var(--surface-light);
  border: 1rpx solid var(--outline-color);
  color: var(--text-primary);
  font-size: 24rpx;
}

.picker-disabled {
  opacity: 0.6;
}

.meta-label {
  font-size: 24rpx;
  color: var(--text-muted);
}

.meta-value {
  font-size: 30rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.meta-button {
  margin-top: auto;
  padding: 16rpx 30rpx;
  border-radius: 999rpx;
  border: none;
  background: var(--accent-gradient);
  color: #05070f;
  font-size: 26rpx;
  font-weight: 600;
}

.meta-button.ghost {
  background: transparent;
  border: 1rpx solid var(--outline-color);
  color: var(--text-muted);
}

.meta-button:disabled {
  opacity: 0.6;
}

.role-row .meta-button {
  margin-top: 0;
}

.system {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
  padding: 32rpx;
}

.system-header {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
}

.system-title {
  font-size: 30rpx;
  font-weight: 600;
}

.system-subtitle {
  font-size: 24rpx;
  color: var(--text-muted);
}

.system-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16rpx;
}

.system-item {
  display: flex;
  flex-direction: column;
  gap: 6rpx;
  padding: 16rpx;
  border-radius: 16rpx;
  background: var(--surface-light);
}

.system-label {
  font-size: 22rpx;
  color: var(--text-muted);
}

.system-value {
  font-size: 28rpx;
  font-weight: 600;
}

.system-warning {
  display: flex;
  flex-direction: column;
  gap: 6rpx;
  padding: 12rpx 18rpx;
  border-radius: 12rpx;
  background: rgba(248, 113, 113, 0.12);
  color: var(--danger-color);
  font-size: 24rpx;
}

.stat-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260rpx, 1fr));
  gap: 20rpx;
}

.chart-extra {
  font-size: 22rpx;
  color: var(--text-muted);
}

.sales-panel {
  display: flex;
  flex-direction: column;
  gap: 24rpx;
}

.sales-form {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240rpx, 1fr));
  gap: 20rpx;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.form-label {
  font-size: 24rpx;
  color: var(--text-muted);
}

.form-input,
.sales-form .picker-display {
  padding: 18rpx 20rpx;
  border-radius: 20rpx;
  background: var(--surface-light);
  border: 1rpx solid var(--outline-color);
  color: var(--text-primary);
  font-size: 26rpx;
}

.sales-form .picker-display {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.switch-field {
  flex-direction: row;
  align-items: center;
  justify-content: space-between;
}

.sales-actions {
  display: flex;
  gap: 16rpx;
  flex-wrap: wrap;
}

.btn {
  padding: 18rpx 32rpx;
  border-radius: 999rpx;
  border: none;
  font-size: 26rpx;
  font-weight: 600;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.btn.primary {
  background: var(--accent-gradient);
  color: #05070f;
}

.btn.ghost {
  background: transparent;
  border: 1rpx solid var(--outline-color);
  color: var(--text-primary);
}

.sales-result {
  display: flex;
  flex-direction: column;
  gap: 18rpx;
}

.sales-summary {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
}

.sales-count {
  font-size: 28rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.sales-total {
  font-size: 26rpx;
  color: var(--text-primary);
}

.sales-settlement {
  margin-top: 12rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.2);
  border-radius: 18rpx;
  overflow: hidden;
}

.settlement-header,
.settlement-row {
  display: grid;
  grid-template-columns: 2fr 1fr 1fr 1fr;
  align-items: center;
  padding: 16rpx 20rpx;
  gap: 12rpx;
}

.settlement-header {
  background: rgba(59, 130, 246, 0.15);
  color: rgba(226, 232, 240, 0.9);
  font-size: 24rpx;
  font-weight: 500;
}

.settlement-row {
  background: rgba(15, 23, 42, 0.45);
  font-size: 24rpx;
  color: rgba(226, 232, 240, 0.85);
}

.settlement-row + .settlement-row {
  border-top: 1rpx solid rgba(148, 163, 184, 0.12);
}

.settlement-col.type {
  font-weight: 600;
}

.settlement-col.count,
.settlement-col.price {
  text-align: center;
}

.settlement-col.total {
  text-align: right;
}

.highlight {
  color: var(--color-secondary, #38bdf8);
}

.sales-meta {
  font-size: 24rpx;
  color: var(--text-muted);
}

.sales-list {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
}

.sales-entry {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
  padding: 18rpx 22rpx;
}

.sales-field {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16rpx;
}

.sales-label {
  font-size: 24rpx;
  color: var(--text-muted);
}

.sales-value {
  font-size: 24rpx;
  color: var(--text-primary);
  text-align: right;
  word-break: break-all;
}

.sales-pagination {
  margin-top: 12rpx;
  display: flex;
  flex-direction: column;
  gap: 12rpx;
  align-items: center;
}

.sales-page-info {
  font-size: 24rpx;
  color: var(--text-muted);
}

.sales-page-actions {
  display: flex;
  gap: 16rpx;
}

.page-btn {
  padding: 14rpx 26rpx;
  border-radius: 999rpx;
  border: 1rpx solid var(--outline-color);
  background: var(--surface-light);
  color: var(--text-primary);
  font-size: 24rpx;
}

.page-btn:disabled {
  opacity: 0.5;
}

.layout-grid {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
  gap: 24rpx;

  @media (min-width: 920px) {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

.panel {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
  padding: 32rpx;
}

.panel-header {
  display: flex;
  flex-direction: column;
  gap: 6rpx;
}

.panel-title {
  font-size: 30rpx;
  font-weight: 600;
}

.panel-subtitle {
  font-size: 24rpx;
  color: var(--text-muted);
}

.panel-empty {
  padding: 48rpx 12rpx;
  text-align: center;
  color: var(--text-muted);
}

.announcement-list {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
}

.announcement-item {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
  padding: 18rpx 20rpx;
  border-radius: 18rpx;
  background: var(--surface-light);
  border: 1rpx solid var(--outline-color);
}

.announcement-title {
  font-size: 28rpx;
  font-weight: 600;
}

.announcement-content {
  font-size: 24rpx;
  color: var(--text-secondary);
  line-height: 1.6;
}

.announcement-meta {
  font-size: 22rpx;
  color: var(--text-muted);
}

.heatmap-grid {
  display: grid;
  grid-template-columns: repeat(1, minmax(0, 1fr));
  gap: 16rpx;

  @media (min-width: 720px) {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

.heatmap-item {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
  padding: 20rpx 24rpx;
}

.heatmap-name {
  font-size: 28rpx;
  font-weight: 600;
}

.heatmap-stats {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 24rpx;
  color: var(--text-muted);
}

.heatmap-label {
  margin-right: 8rpx;
}

.heatmap-value {
  font-size: 28rpx;
  font-weight: 600;
  color: var(--text-primary);
}

  
</style>