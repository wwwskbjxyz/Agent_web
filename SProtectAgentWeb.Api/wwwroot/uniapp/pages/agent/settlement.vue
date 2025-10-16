<template>
  <view class="page">
    <view v-if="!platformStore.isAuthenticated" class="empty">
      <text>请先登录代理账号后再管理结算设置。</text>
      <button class="link" @tap="goLogin">前往登录</button>
    </view>
    <scroll-view v-else scroll-y class="content">
      <view class="section">
        <view class="section-header">
          <view class="section-titles">
            <text class="section-title">结算设置</text>
            <text class="section-subtitle">为不同卡密类型配置结算价格，销量查询将自动计算总额。</text>
          </view>
          <view class="section-meta">
            <text class="meta-label">当前软件位</text>
            <text class="meta-value">{{ selectedSoftware || '未选择' }}</text>
          </view>
        </view>
        <view v-if="!selectedSoftware" class="section-empty">请返回首页选择一个软件码后再进行设置。</view>
        <view v-else>
          <view v-if="loadingSettlement" class="section-empty">结算信息加载中...</view>
          <view v-else-if="!editableRates.length" class="section-empty">当前软件位暂无卡密类型。</view>
          <view v-else>
            <view v-if="loadingCardTypes" class="section-note">卡密类型同步中，稍后将自动补齐列表。</view>
            <view v-if="hasAgentOptions" class="agent-selector">
              <view class="selector-labels">
                <text class="selector-title">配置对象</text>
                <text class="selector-hint">为不同代理设置专属结算单价</text>
              </view>
              <picker
                mode="selector"
                :range="agentOptions"
                range-key="label"
                :value="agentIndex"
                :disabled="loadingSettlement"
                @change="handleAgentChange"
              >
                <view class="selector-value" :class="{ disabled: loadingSettlement }">
                  <text class="selector-text">{{ selectedAgentLabel }}</text>
                  <text class="selector-arrow">⌄</text>
                </view>
              </picker>
            </view>
            <view class="cycle-box">
              <view class="cycle-header">
                <view class="cycle-title-group">
                  <text class="cycle-title">结算周期</text>
                  <text v-if="cycleInherited" class="cycle-badge">继承</text>
                </view>
                <view class="cycle-status" :class="{ due: cycleDue }">
                  <text class="status-label">下次结算</text>
                  <text class="status-value">{{ nextDueText }}</text>
                </view>
              </view>
              <view class="cycle-body">
                <view class="cycle-input">
                  <input
                    class="cycle-field"
                    type="digit"
                    :placeholder="effectiveCycleDays > 0 ? `默认 ${effectiveCycleDays} 天` : '输入天数'"
                    v-model="cycleDaysInput"
                    :disabled="savingRates"
                    @blur="handleCycleBlur"
                  />
                  <text class="cycle-unit">天</text>
                </view>
                <view class="cycle-input">
                  <input
                    class="cycle-field"
                    type="text"
                    :placeholder="cycleTimePlaceholder"
                    v-model="cycleTimeInput"
                    :disabled="savingRates"
                    @blur="handleCycleTimeBlur"
                  />
                  <text class="cycle-unit">时间</text>
                </view>
                <view class="cycle-meta">
                  <text class="meta-label">最近结算</text>
                  <text class="meta-value">{{ lastSettledText }}</text>
                </view>
                <view class="cycle-meta">
                  <text class="meta-label">执行时间</text>
                  <text class="meta-value">{{ effectiveCycleTimeText }}</text>
                </view>
                <view class="cycle-meta" v-if="cycleDue">
                  <text class="meta-label warn">提醒</text>
                  <text class="meta-value warn">当前周期已到，请及时完成结算。</text>
                </view>
              </view>
            </view>
            <view class="rate-table">
              <view class="rate-header">
                <text class="col-type">卡密类型</text>
                <text class="col-price">结算单价（元）</text>
              </view>
              <view v-for="item in editableRates" :key="item.cardType" class="rate-row">
                <text class="col-type">{{ item.cardType }}</text>
                <input
                  class="col-price-input"
                  type="digit"
                  :placeholder="'0.00'"
                  v-model="item.price"
                  @blur="normalizePrice(item)"
                />
              </view>
            </view>
          </view>
          <view class="actions">
            <button class="btn primary" :disabled="savingRates || loadingSettlement || loadingCardTypes" @tap="save">{{ savingRates ? '保存中...' : '保存设置' }}</button>
            <button class="btn ghost" :disabled="savingRates || loadingSettlement" @tap="refresh">重新加载</button>
          </view>
        </view>
      </view>
      <view class="section">
        <view class="section-header">
          <view class="section-titles">
            <text class="section-title">结算账单</text>
            <text class="section-subtitle">系统会根据结算周期生成待处理账单，确认收款后请标记已结算。</text>
          </view>
        </view>
        <view v-if="!pendingBills.length && !settledBills.length" class="section-empty">暂无结算账单。</view>
        <view v-else class="bill-list">
          <view v-for="bill in pendingBills" :key="`pending-${bill.id}`" class="bill-card pending">
            <view class="bill-row">
              <text class="bill-label">结算周期</text>
              <text class="bill-value">{{ formatDateTimeDisplay(bill.cycleStartUtc) }} ~ {{ formatDateTimeDisplay(bill.cycleEndUtc) }}</text>
            </view>
            <view v-if="hasBreakdown(bill)" class="bill-breakdown">
              <view class="breakdown-header">
                <text class="bill-label">结算明细</text>
                <text class="bill-subtotal">建议金额：￥{{ formatCurrency(bill.suggestedAmount ?? bill.amount) }}</text>
              </view>
              <view v-for="item in bill.breakdowns" :key="`${bill.id}-${item.agent}`" class="breakdown-row">
                <text class="breakdown-agent">{{ item.displayName || item.agent }}</text>
                <text class="breakdown-count">{{ item.count }} 张</text>
                <text class="breakdown-amount">￥{{ formatCurrency(item.amount) }}</text>
              </view>
            </view>
            <view v-else class="bill-row subtle">
              <text class="bill-label">结算明细</text>
              <text class="bill-value">当前周期无待结算数据</text>
            </view>
            <view class="bill-row">
              <text class="bill-label">结算金额</text>
              <input
                class="bill-input"
                type="digit"
                v-model="billAmountEdits[bill.id]"
                :placeholder="bill.suggestedAmount ? formatCurrency(bill.suggestedAmount) : '0.00'"
                :disabled="!hasBreakdown(bill)"
              />
            </view>
            <view class="bill-row">
              <text class="bill-label">备注</text>
              <input
                class="bill-input"
                type="text"
                v-model="billNoteEdits[bill.id]"
                placeholder="备注信息（可选）"
              />
            </view>
            <button class="btn primary" :disabled="savingRates || !hasBreakdown(bill)" @tap="confirmBill(bill.id)">确认已结算</button>
          </view>
          <view v-for="bill in settledBills" :key="`settled-${bill.id}`" class="bill-card settled">
            <view class="bill-row">
              <text class="bill-label">结算周期</text>
              <text class="bill-value">{{ formatDateTimeDisplay(bill.cycleStartUtc) }} ~ {{ formatDateTimeDisplay(bill.cycleEndUtc) }}</text>
            </view>
            <view class="bill-row">
              <text class="bill-label">结算金额</text>
              <text class="bill-value emphasis">￥{{ formatCurrency(bill.amount) }}</text>
            </view>
            <view v-if="bill.breakdowns?.length" class="bill-breakdown settled">
              <view class="breakdown-header">
                <text class="bill-label">结算明细</text>
              </view>
              <view v-for="item in bill.breakdowns" :key="`${bill.id}-${item.agent}`" class="breakdown-row">
                <text class="breakdown-agent">{{ item.displayName || item.agent }}</text>
                <text class="breakdown-count">{{ item.count }} 张</text>
                <text class="breakdown-amount">￥{{ formatCurrency(item.amount) }}</text>
              </view>
            </view>
            <view class="bill-row">
              <text class="bill-label">结算时间</text>
              <text class="bill-value">{{ formatDateTimeDisplay(bill.settledAtUtc) }}</text>
            </view>
            <view v-if="bill.note" class="bill-row">
              <text class="bill-label">备注</text>
              <text class="bill-value">{{ bill.note }}</text>
            </view>
          </view>
        </view>
      </view>
      <view class="tips">
        <text>提示：</text>
        <text>1. 结算价格仅用于前端统计，不会影响卡密本身价格。</text>
        <text>2. 修改后请重新在首页执行销量查询以刷新结算金额。</text>
      </view>
    </scroll-view>
  </view>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import { useAppStore } from '@/stores/app';
import { usePlatformStore } from '@/stores/platform';
import type { CardTypeInfo, SettlementRateItem, SettlementBillItem } from '@/common/types';

interface EditableRate {
  cardType: string;
  price: string;
}

const appStore = useAppStore();
const platformStore = usePlatformStore();
const {
  selectedSoftware,
  cardTypes,
  selectedSettlementAgent,
  settlementAgents,
  activeSettlementAgent,
  settlementCycle,
  settlementBills,
  settlementHasReminder
} = storeToRefs(appStore);
const editableRates = ref<EditableRate[]>([]);
const cycleDaysInput = ref('');
const cycleTimeInput = ref('');
const billAmountEdits = reactive<Record<number, string>>({});
const billNoteEdits = reactive<Record<number, string>>({});

const loadingSettlement = computed(() => appStore.loading.settlementRates);
const loadingCardTypes = computed(() => appStore.loading.cardTypes);
const savingRates = computed(() => appStore.loading.saveSettlementRates);

const agentOptions = computed(() => {
  const rawOptions = Array.isArray(settlementAgents.value) ? settlementAgents.value : [];
  const options = rawOptions.map((item) => ({
    value: (item?.username ?? '').toString().trim(),
    label: (item?.displayName ?? '').toString().trim() || (item?.username ?? '').toString().trim()
  }));

  const filtered = options.filter((item) => item.value.length > 0);

  if (!filtered.length) {
    const fallback = activeSettlementAgent.value?.trim();
    if (fallback) {
      filtered.push({ value: fallback, label: `${fallback} · 当前账号` });
    }
  }

  return filtered;
});

const hasAgentOptions = computed(() => agentOptions.value.length > 0);

const selectedAgentValue = computed(() => selectedSettlementAgent.value?.trim() || activeSettlementAgent.value?.trim() || '');

const agentIndex = computed(() => {
  const options = agentOptions.value;
  const current = selectedAgentValue.value;
  const index = options.findIndex((option) => option.value === current);
  return index >= 0 ? index : 0;
});

const selectedAgentLabel = computed(() => {
  const options = agentOptions.value;
  const current = selectedAgentValue.value;
  const match = options.find((option) => option.value === current);
  return match?.label || options[0]?.label || '当前账号';
});

const cycleInfo = computed(() => settlementCycle.value);
const cycleDue = computed(() => Boolean(cycleInfo.value?.isDue));
const effectiveCycleDays = computed(() => cycleInfo.value?.effectiveDays ?? 0);
const cycleInherited = computed(() => Boolean(cycleInfo.value?.isInherited));
const nextDueText = computed(() => formatDateTimeDisplay(cycleInfo.value?.nextDueTimeUtc));
const lastSettledText = computed(() => formatDateTimeDisplay(cycleInfo.value?.lastSettledTimeUtc));
const effectiveCycleTimeText = computed(
  () => cycleInfo.value?.effectiveTimeLabel || formatTimeLabel(0)
);
const cycleTimePlaceholder = computed(() => {
  const info = cycleInfo.value;
  if (!info) {
    return 'HH:mm';
  }
  const ownDefined = Number.isInteger(info.ownTimeMinutes) &&
    (info.ownDays > 0 || info.ownTimeMinutes !== info.effectiveTimeMinutes);
  if (ownDefined) {
    return info.ownTimeLabel || formatTimeLabel(info.ownTimeMinutes);
  }
  return `默认 ${info.effectiveTimeLabel || formatTimeLabel(info.effectiveTimeMinutes)}`;
});

const pendingBills = computed(() => settlementBills.value.filter((bill) => !bill.isSettled));
const settledBills = computed(() => settlementBills.value.filter((bill) => bill.isSettled));

function hasBreakdown(bill: SettlementBillItem) {
  return Array.isArray(bill?.breakdowns) && bill.breakdowns.length > 0;
}

watch(
  agentOptions,
  async (options) => {
    if (!options.length) {
      return;
    }
    const current = selectedAgentValue.value;
    const hasMatch = options.some((option) => option.value === current);
    if (!hasMatch) {
      const fallback = options[0];
      if (fallback) {
        appStore.setSelectedSettlementAgent(fallback.value);
        await refresh(true);
      }
    }
  },
  { immediate: false }
);

watch(
  settlementCycle,
  (cycle) => {
    if (!cycle) {
      cycleDaysInput.value = '';
      cycleTimeInput.value = '';
      return;
    }
    const source = cycle.ownDays > 0 ? cycle.ownDays : cycle.effectiveDays;
    cycleDaysInput.value = source > 0 ? String(source) : '';
    const timeSource =
      cycle.ownDays > 0 || cycle.ownTimeMinutes !== cycle.effectiveTimeMinutes
        ? cycle.ownTimeMinutes
        : cycle.effectiveTimeMinutes;
    if (Number.isFinite(timeSource)) {
      cycleTimeInput.value = formatTimeLabel(Number(timeSource));
    } else {
      cycleTimeInput.value = '';
    }
  },
  { immediate: true }
);

watch(
  pendingBills,
  (bills) => {
    const activeIds = new Set<number>();
    bills.forEach((bill) => {
      activeIds.add(bill.id);
      const suggested = formatAmountInput(bill.suggestedAmount ?? bill.amount);
      if (!billAmountEdits[bill.id] || billAmountEdits[bill.id] === '0' || billAmountEdits[bill.id] === '0.00') {
        billAmountEdits[bill.id] = suggested;
      }
      if (bill.note && !billNoteEdits[bill.id]) {
        billNoteEdits[bill.id] = bill.note;
      }
    });

    Object.keys(billAmountEdits).forEach((key) => {
      const id = Number(key);
      if (!Number.isFinite(id) || !activeIds.has(id)) {
        delete billAmountEdits[id];
      }
    });

    Object.keys(billNoteEdits).forEach((key) => {
      const id = Number(key);
      if (!Number.isFinite(id) || !activeIds.has(id)) {
        delete billNoteEdits[id];
      }
    });
  },
  { immediate: true }
);

async function bootstrap() {
  if (!platformStore.isAuthenticated) {
    return;
  }
  await refresh(true);
}

let refreshGeneration = 0;

function buildEditableRates(
  settlementRatesList: SettlementRateItem[],
  cardTypeList?: CardTypeInfo[] | null
): EditableRate[] {
  const availableTypes = new Set<string>();
  settlementRatesList.forEach((rate) => {
    if (rate?.cardType) {
      availableTypes.add(rate.cardType);
    }
  });

  if (Array.isArray(cardTypeList)) {
    cardTypeList.forEach((type) => {
      if (type?.name) {
        availableTypes.add(type.name);
      }
    });
  }

  return Array.from(availableTypes)
    .sort((a, b) => a.localeCompare(b))
    .map((cardType) => {
      const current = settlementRatesList.find((rate) => rate.cardType === cardType);
      return {
        cardType,
        price: current ? formatCurrency(current.price) : '0.00'
      };
    });
}

async function refresh(force = false) {
  const software = selectedSoftware.value;
  if (!software) {
    editableRates.value = [];
    return;
  }

  try {
    const generation = ++refreshGeneration;
    const cardTypesPromise = appStore
      .loadCardTypes(force)
      .catch((error) => {
        console.warn('loadCardTypes failed', error);
        return null;
      });

    const loadedSettlementRates = await appStore.loadSettlementRates(force);
    const settlementRates = Array.isArray(loadedSettlementRates) ? loadedSettlementRates : [];

    editableRates.value = buildEditableRates(settlementRates, cardTypes.value);

    cardTypesPromise.then((loadedCardTypes) => {
      if (generation !== refreshGeneration) {
        return;
      }
      const resolved = Array.isArray(loadedCardTypes) && loadedCardTypes.length > 0
        ? loadedCardTypes
        : cardTypes.value;
      editableRates.value = buildEditableRates(settlementRates, resolved);
    });
  } catch (error) {
    console.error('refresh settlement rates failed', error);
    uni.showToast({ title: '加载失败', icon: 'none' });
  }
}

function formatCurrency(value: number | string) {
  const amount = Number(value ?? 0);
  if (!Number.isFinite(amount)) {
    return '0.00';
  }
  return amount.toFixed(2);
}

function formatAmountInput(value?: number | null) {
  if (value == null) {
    return '';
  }
  const numeric = Number(value);
  if (!Number.isFinite(numeric) || numeric <= 0) {
    return '';
  }
  return formatCurrency(numeric);
}

function normalizePrice(item: EditableRate) {
  item.price = formatCurrency(item.price);
}

function formatDateTimeDisplay(value?: string | null) {
  if (!value) {
    return '--';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  const yyyy = date.getFullYear();
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  const dd = String(date.getDate()).padStart(2, '0');
  const hh = String(date.getHours()).padStart(2, '0');
  const mi = String(date.getMinutes()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd} ${hh}:${mi}`;
}

function formatTimeLabel(value?: number | string | null) {
  const minutes = Number(value);
  if (!Number.isFinite(minutes)) {
    return '00:00';
  }
  const normalized = ((minutes % 1440) + 1440) % 1440;
  const hours = Math.floor(normalized / 60);
  const mins = Math.floor(normalized % 60);
  return `${hours.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}`;
}

function parseCycleDays(): number | null {
  const raw = cycleDaysInput.value.trim();
  if (!raw) {
    return null;
  }
  const numeric = Number(raw);
  if (!Number.isFinite(numeric) || numeric <= 0) {
    return 0;
  }
  return Math.floor(numeric);
}

function handleCycleBlur() {
  const parsed = parseCycleDays();
  if (parsed === null || parsed <= 0) {
    cycleDaysInput.value = '';
  } else {
    cycleDaysInput.value = String(parsed);
  }
}

function parseCycleTimeMinutes(): number | null | undefined {
  const raw = cycleTimeInput.value.trim();
  if (!raw) {
    return null;
  }
  const normalized = raw.replace('：', ':');
  const match = normalized.match(/^\s*(\d{1,2})\s*:\s*(\d{1,2})\s*$/);
  if (!match) {
    return undefined;
  }
  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  if (!Number.isFinite(hours) || !Number.isFinite(minutes) || hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
    return undefined;
  }
  return hours * 60 + minutes;
}

function handleCycleTimeBlur() {
  const parsed = parseCycleTimeMinutes();
  if (parsed === undefined) {
    cycleTimeInput.value = '';
    uni.showToast({ title: '时间格式应为 HH:mm', icon: 'none' });
    return;
  }
  if (parsed === null) {
    cycleTimeInput.value = '';
    return;
  }
  cycleTimeInput.value = formatTimeLabel(parsed);
}

async function save() {
  const software = selectedSoftware.value;
  if (!software) {
    uni.showToast({ title: '请先选择软件位', icon: 'none' });
    return;
  }

  const payload = editableRates.value.map((item) => ({
    cardType: item.cardType,
    price: Number(item.price) || 0
  }));

  try {
    const cycleDays = parseCycleDays();
    const cycleTimeMinutes = parseCycleTimeMinutes();
    if (cycleTimeMinutes === undefined) {
      uni.showToast({ title: '请填写正确的结算时间（HH:mm）', icon: 'none' });
      return;
    }
    await appStore.saveSettlementRates(
      payload,
      cycleDays ?? undefined,
      cycleTimeMinutes ?? undefined
    );
    uni.showToast({ title: '保存成功', icon: 'success' });
    await refresh(true);
  } catch (error) {
    console.error('save settlement rates failed', error);
    const message = (error as any)?.message || '保存失败';
    uni.showToast({ title: message, icon: 'none' });
  }
}

async function confirmBill(billId: number) {
  const rawAmount = billAmountEdits[billId] ?? '0';
  let amount = Number(rawAmount);
  const bill = pendingBills.value.find((item) => item.id === billId);
  if ((!Number.isFinite(amount) || amount <= 0) && bill && Number(bill.suggestedAmount ?? 0) > 0) {
    amount = Number(bill.suggestedAmount);
  }

  if (!Number.isFinite(amount) || amount < 0) {
    uni.showToast({ title: '请输入有效金额', icon: 'none' });
    return;
  }

  try {
    await appStore.completeSettlementBill(billId, amount, billNoteEdits[billId]);
    delete billAmountEdits[billId];
    delete billNoteEdits[billId];
    uni.showToast({ title: '账单已结算', icon: 'success' });
  } catch (error) {
    console.error('complete settlement bill failed', error);
    const message = (error as any)?.message || '更新失败';
    uni.showToast({ title: message, icon: 'none' });
  }
}

async function handleAgentChange(event: any) {
  const index = Number(event?.detail?.value ?? 0);
  const options = agentOptions.value;
  const option = options[index];
  if (!option) {
    return;
  }
  appStore.setSelectedSettlementAgent(option.value);
  await refresh(true);
}

function goLogin() {
  uni.reLaunch({ url: '/pages/login/agent' });
}

watch(
  () => selectedSoftware.value,
  () => {
    refresh();
  }
);

onMounted(() => {
  bootstrap();
});
</script>

<style scoped lang="scss">
.page {
  min-height: 100vh;
  background: radial-gradient(circle at top left, rgba(59, 130, 246, 0.12), transparent 55%), #0f172a;
  padding: 24rpx 24rpx 48rpx;
  color: rgba(226, 232, 240, 0.9);
}

.content {
  height: 100%;
}

.section {
  display: flex;
  flex-direction: column;
  gap: 24rpx;
  padding: 32rpx 28rpx;
  border-radius: 24rpx;
  background: rgba(15, 23, 42, 0.6);
  border: 1px solid rgba(148, 163, 184, 0.2);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 24rpx;
}

.agent-selector {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24rpx;
  margin-bottom: 24rpx;
  padding: 20rpx 24rpx;
  border-radius: 20rpx;
  background: rgba(15, 23, 42, 0.45);
  border: 1px solid rgba(148, 163, 184, 0.16);
}

.selector-labels {
  display: flex;
  flex-direction: column;
  gap: 6rpx;
}

.selector-title {
  font-size: 28rpx;
  font-weight: 600;
  color: rgba(226, 232, 240, 0.92);
}

.selector-hint {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.85);
}

.selector-value {
  min-width: 260rpx;
  padding: 18rpx 24rpx;
  border-radius: 16rpx;
  border: 1px solid rgba(94, 234, 212, 0.35);
  display: flex;
  align-items: center;
  gap: 16rpx;
  color: rgba(226, 232, 240, 0.95);
  background: rgba(15, 23, 42, 0.65);
}

.cycle-box {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
  padding: 24rpx;
  border-radius: 20rpx;
  border: 1px solid rgba(94, 234, 212, 0.25);
  background: rgba(15, 23, 42, 0.5);
  margin-bottom: 24rpx;
}

.cycle-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.cycle-title-group {
  display: flex;
  align-items: center;
  gap: 12rpx;
}

.cycle-title {
  font-size: 30rpx;
  font-weight: 600;
  color: rgba(226, 232, 240, 0.95);
}

.cycle-badge {
  font-size: 22rpx;
  padding: 4rpx 12rpx;
  border-radius: 999rpx;
  background: rgba(59, 130, 246, 0.25);
  color: rgba(191, 219, 254, 0.95);
}

.cycle-status {
  display: flex;
  align-items: center;
  gap: 12rpx;
  font-size: 26rpx;
  color: rgba(148, 163, 184, 0.9);
}

.cycle-status.due .status-value {
  color: #fbbf24;
}

.status-label {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.85);
}

.status-value {
  font-size: 28rpx;
  font-weight: 600;
  color: rgba(226, 232, 240, 0.95);
}

.cycle-body {
  display: flex;
  flex-direction: column;
  gap: 18rpx;
}

.cycle-input {
  display: flex;
  align-items: center;
  gap: 16rpx;
}

.cycle-input + .cycle-input {
  margin-top: 12rpx;
}

.cycle-field {
  flex: 1;
  padding: 18rpx 20rpx;
  border-radius: 14rpx;
  background: rgba(15, 23, 42, 0.6);
  border: 1px solid rgba(94, 234, 212, 0.35);
  color: rgba(226, 232, 240, 0.95);
}

.cycle-unit {
  font-size: 26rpx;
  color: rgba(148, 163, 184, 0.85);
}

.cycle-meta {
  display: flex;
  justify-content: space-between;
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.85);
}

.cycle-meta .meta-value {
  color: rgba(226, 232, 240, 0.9);
}

.cycle-meta.warn .meta-label,
.cycle-meta.warn .meta-value {
  color: #fbbf24;
}

.bill-list {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
}

.bill-card {
  border-radius: 20rpx;
  border: 1px solid rgba(148, 163, 184, 0.18);
  background: rgba(15, 23, 42, 0.55);
  padding: 24rpx 26rpx;
  display: flex;
  flex-direction: column;
  gap: 18rpx;
}

.bill-card.pending {
  border-color: rgba(250, 204, 21, 0.45);
  box-shadow: 0 0 24rpx rgba(250, 204, 21, 0.12);
}

.bill-card.settled {
  border-color: rgba(94, 234, 212, 0.35);
}

.bill-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 18rpx;
  font-size: 26rpx;
}

.bill-label {
  color: rgba(148, 163, 184, 0.8);
}

.bill-value {
  color: rgba(226, 232, 240, 0.95);
  text-align: right;
  flex: 1;
}

.bill-value.emphasis {
  font-weight: 600;
  color: rgba(94, 234, 212, 0.9);
}

.bill-input {
  flex: 1;
  padding: 16rpx 18rpx;
  border-radius: 14rpx;
  border: 1px solid rgba(94, 234, 212, 0.3);
  background: rgba(15, 23, 42, 0.55);
  color: rgba(226, 232, 240, 0.95);
}

.bill-row.subtle .bill-value {
  color: rgba(148, 163, 184, 0.75);
}

.bill-breakdown {
  border: 1px solid rgba(148, 163, 184, 0.16);
  border-radius: 16rpx;
  padding: 16rpx 20rpx;
  background: rgba(15, 23, 42, 0.45);
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.bill-breakdown.settled {
  border-color: rgba(94, 234, 212, 0.25);
  background: rgba(15, 23, 42, 0.35);
}

.breakdown-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 26rpx;
}

.bill-subtotal {
  color: rgba(94, 234, 212, 0.85);
  font-weight: 600;
}

.breakdown-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12rpx;
  font-size: 26rpx;
}

.breakdown-agent {
  flex: 1;
  color: rgba(226, 232, 240, 0.92);
}

.breakdown-count {
  color: rgba(148, 163, 184, 0.8);
  min-width: 120rpx;
  text-align: right;
}

.breakdown-amount {
  min-width: 160rpx;
  text-align: right;
  color: rgba(94, 234, 212, 0.9);
  font-weight: 600;
}

.selector-value.disabled {
  opacity: 0.6;
}

.selector-text {
  flex: 1;
  font-size: 26rpx;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.selector-arrow {
  font-size: 32rpx;
  color: rgba(148, 163, 184, 0.8);
}

.section-titles {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
}

.section-title {
  font-size: 34rpx;
  font-weight: 600;
}

.section-subtitle {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.85);
}

.section-meta {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 6rpx;
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.85);
}

.meta-label {
  font-size: 22rpx;
}

.meta-value {
  font-size: 28rpx;
  font-weight: 600;
  color: rgba(191, 219, 254, 0.95);
}

.section-empty {
  padding: 48rpx 20rpx;
  text-align: center;
  color: rgba(148, 163, 184, 0.8);
}

.section-note {
  margin-bottom: 16rpx;
  padding: 16rpx 20rpx;
  border-radius: 14rpx;
  background: rgba(59, 130, 246, 0.12);
  color: rgba(191, 219, 254, 0.9);
  font-size: 24rpx;
}

.rate-table {
  border: 1rpx solid rgba(148, 163, 184, 0.2);
  border-radius: 18rpx;
  overflow: hidden;
}

.rate-header,
.rate-row {
  display: grid;
  grid-template-columns: 2fr 1fr;
  align-items: center;
  padding: 18rpx 24rpx;
  gap: 16rpx;
}

.rate-header {
  background: rgba(59, 130, 246, 0.15);
  font-size: 24rpx;
  color: rgba(226, 232, 240, 0.9);
}

.rate-row {
  background: rgba(15, 23, 42, 0.45);
  font-size: 24rpx;
}

.rate-row + .rate-row {
  border-top: 1rpx solid rgba(148, 163, 184, 0.12);
}

.col-type {
  font-weight: 600;
}

.col-price {
  text-align: right;
}

.col-price-input {
  width: 100%;
  padding: 16rpx 20rpx;
  border-radius: 14rpx;
  background: rgba(15, 23, 42, 0.75);
  border: 1rpx solid rgba(148, 163, 184, 0.35);
  color: rgba(226, 232, 240, 0.9);
  font-size: 24rpx;
  text-align: right;
}

.actions {
  margin-top: 28rpx;
  display: flex;
  gap: 20rpx;
}

.btn {
  flex: 1;
  padding: 18rpx 24rpx;
  border-radius: 16rpx;
  font-size: 26rpx;
}

.btn.primary {
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.85), rgba(129, 140, 248, 0.85));
  color: #f8fafc;
  border: none;
}

.btn.ghost {
  background: transparent;
  color: rgba(191, 219, 254, 0.95);
  border: 1rpx solid rgba(148, 163, 184, 0.3);
}

.tips {
  margin-top: 28rpx;
  padding: 24rpx 28rpx;
  border-radius: 18rpx;
  background: rgba(30, 41, 59, 0.65);
  border: 1rpx solid rgba(148, 163, 184, 0.18);
  display: flex;
  flex-direction: column;
  gap: 12rpx;
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.85);
}

.empty {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  gap: 20rpx;
  align-items: center;
  justify-content: center;
  color: rgba(226, 232, 240, 0.85);
}

.link {
  padding: 14rpx 28rpx;
  border-radius: 14rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.35);
  background: transparent;
  color: rgba(191, 219, 254, 0.95);
}
</style>
