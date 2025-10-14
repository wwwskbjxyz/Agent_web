<template>
  <view class="page">
    <view class="header glass-card">
      <view class="header-main">
        <text class="title">卡密管理</text>
        <text class="subtitle">多条件搜索、批量生成与状态管理</text>
      </view>
      <view class="header-actions">
        <SoftwarePicker />
        <button class="header-btn" @tap="createCard">批量生成</button>
        <button class="header-btn ghost" @tap="exportCard">导出当前列表</button>
      </view>
    </view>

    <view class="filters glass-card">
      <view class="filters-grid">
        <view class="field field--full">
          <text class="label">关键字 / 备注 / 卡密</text>
          <textarea
            class="textarea"
            v-model="filtersForm.keyword"
            placeholder="支持卡密、备注、机器码、多个关键词可换行或空格分隔"
            auto-height
          />
        </view>
        <view class="field">
          <text class="label">卡密类型</text>
          <picker mode="selector" :range="cardTypeOptions" :value="cardTypeIndex" @change="onCardTypeChange">
            <view class="picker-display">{{ cardTypeOptions[cardTypeIndex] || '全部' }}</view>
          </picker>
        </view>
        <view class="field">
          <text class="label">状态</text>
          <picker mode="selector" :range="statusLabels" :value="statusIndex" @change="onStatusChange">
            <view class="picker-display">{{ statusLabels[statusIndex] || '全部' }}</view>
          </picker>
        </view>
        <view class="field">
          <text class="label">代理账号</text>
          <picker mode="selector" :range="agentOptions" :value="agentIndex" @change="onAgentChange">
            <view class="picker-display">{{ agentOptions[agentIndex] || '全部' }}</view>
          </picker>
        </view>
        <view class="field">
          <text class="label">查询模式</text>
          <picker mode="selector" :range="searchTypeLabels" :value="searchTypeIndex" @change="onSearchTypeChange">
            <view class="picker-display">{{ searchTypeLabels[searchTypeIndex] || '智能匹配' }}</view>
          </picker>
        </view>
        <view class="field switch-field">
          <text class="label">包含下级</text>
          <switch :checked="filtersForm.includeDescendants" :disabled="disableDescendants" @change="onDescendantChange" />
          <text v-if="disableDescendants" class="hint">指定代理时默认不包含下级</text>
        </view>
        <view class="field">
          <text class="label">机器码</text>
          <input class="input" v-model="filtersForm.machineCode" placeholder="输入完整机器码" />
        </view>
        <view class="field">
          <text class="label">IP 地址</text>
          <input class="input" v-model="filtersForm.ip" placeholder="如 113.45.90.1" />
        </view>
        <view class="field">
          <text class="label">开始时间</text>
          <DateTimePicker v-model="filtersForm.startTime" placeholder="选择开始时间" />
        </view>
        <view class="field">
          <text class="label">结束时间</text>
          <DateTimePicker v-model="filtersForm.endTime" placeholder="选择结束时间" />
        </view>
        <view class="field">
          <text class="label">每页条数</text>
          <picker mode="selector" :range="pageSizeLabels" :value="pageSizeIndex" @change="onPageSizeChange">
            <view class="picker-display">{{ pageSizeLabels[pageSizeIndex] }}</view>
          </picker>
        </view>
      </view>
      <view class="filter-actions">
        <button class="btn primary" :disabled="loading" @tap="submitSearch">{{ loading ? '查询中...' : '查询' }}</button>
        <button class="btn ghost" :disabled="loading" @tap="resetFilters">重置</button>
      </view>
    </view>

    <DataTable
      title="卡密总览"
      :subtitle="summaryText"
      :columns="columns"
      :rows="rows"
      :loading="loading"
      layout="stack"
      primary-column="key"
      operations-column="operations"
      :collapse-details="true"
    >
      <template #actions>
        <button class="mini-btn ghost" :disabled="loading" @tap="refreshList">刷新</button>
      </template>
      <template #key="{ row }">
        <view class="key-header">
          <text class="copy-text copyable" @longpress="copyValue(row.key)">{{ row.key }}</text>
          <StatusTag :status="statusMap[row.status]" :label="row.statusText || statusText[row.status]" />
        </view>
      </template>
      <template #status="{ row }">
        <StatusTag :status="statusMap[row.status]" :label="row.statusText || statusText[row.status]" />
      </template>
      <template #ip="{ row }">
        <text class="copy-text copyable" @longpress="copyValue(row.ip || '')">{{ row.ip || '—' }}</text>
      </template>
      <template #machineCodes="{ row }">
        <view v-if="row.machineCodes && row.machineCodes.length" class="machine-list">
          <view
            v-for="code in row.machineCodes"
            :key="code"
            class="machine-item copyable"
            @longpress="copyValue(code)"
          >
            {{ code }}
          </view>
        </view>
        <text v-else class="placeholder">—</text>
      </template>
      <template #remark="{ row }">
        <text v-if="row.remark" class="remark">{{ row.remark }}</text>
        <text v-else class="placeholder">—</text>
      </template>
      <template #fullKey="{ row }">
        <text class="full-key copyable" @longpress="copyValue(row.fullKey)">{{ row.fullKey }}</text>
      </template>
      <template #operations="{ row }">
        <view class="action-group">
          <button
            class="action-btn"
            :class="{ primary: row.status !== 'enabled' }"
            :disabled="loading || isActionBusy(row.key, 'enable')"
            @tap="handleStatusAction(row, 'enable')"
          >
            {{ isActionBusy(row.key, 'enable') ? '处理中...' : '启用' }}
          </button>
          <button
            class="action-btn warn"
            :disabled="loading || isActionBusy(row.key, 'disable')"
            @tap="handleStatusAction(row, 'disable')"
          >
            {{ isActionBusy(row.key, 'disable') ? '处理中...' : '禁用' }}
          </button>
          <button
            class="action-btn"
            :disabled="loading || isActionBusy(row.key, 'unban')"
            @tap="handleStatusAction(row, 'unban')"
          >
            {{ isActionBusy(row.key, 'unban') ? '处理中...' : '解除封禁' }}
          </button>
          <button
            class="action-btn ghost"
            :disabled="loading || isActionBusy(row.key, 'unbind')"
            @tap="handleUnbind(row)"
          >
            {{ isActionBusy(row.key, 'unbind') ? '处理中...' : '解绑' }}
          </button>
        </view>
      </template>
    </DataTable>

    <view class="pagination glass-card" v-if="totalPages > 1 || cardTotal">
      <view class="page-info">第 {{ currentPage }} / {{ totalPages }} 页 · 共 {{ cardTotal }} 条</view>
      <view class="page-actions">
        <button class="page-btn" :disabled="currentPage <= 1 || loading" @tap="changePage(currentPage - 1)">上一页</button>
        <button class="page-btn" :disabled="currentPage >= totalPages || loading" @tap="changePage(currentPage + 1)">下一页</button>
      </view>
    </view>
    <view v-if="showCreateCardModal" class="create-card-modal" @tap="closeCreateCardModal">
      <view class="create-card-panel glass-card" @tap.stop="">
        <view class="create-card-header">
          <text class="create-card-title">批量生成卡密</text>
          <button class="create-card-close" :disabled="creatingCards" @tap="closeCreateCardModal">关闭</button>
        </view>
        <view class="create-card-body">
          <view class="form-field">
            <text class="form-label">卡密类型</text>
            <picker
              mode="selector"
              :range="cardTypePickerLabels"
              :value="createCardForm.cardTypeIndex"
              @change="onCreateCardTypeChange"
            >
              <view class="picker-display">{{ cardTypePickerLabels[createCardForm.cardTypeIndex] || '请选择卡密类型' }}</view>
            </picker>
          </view>
          <view class="form-field">
            <text class="form-label">生成数量</text>
            <input class="input" type="number" v-model="createCardForm.quantity" placeholder="1-500" />
          </view>
          <view class="form-field">
            <text class="form-label">备注（可选）</text>
            <textarea
              class="textarea"
              v-model="createCardForm.remarks"
              placeholder="可填写备注信息"
              auto-height
            ></textarea>
          </view>
          <view class="form-field">
            <text class="form-label">自定义前缀（可选）</text>
            <input
              class="input"
              v-model="createCardForm.customPrefix"
              placeholder="最多 32 位字符"
              maxlength="32"
            />
          </view>
        </view>
        <view class="create-card-actions">
          <button class="btn ghost" :disabled="creatingCards" @tap="closeCreateCardModal">取消</button>
          <button class="btn primary" :disabled="creatingCards" @tap="submitCreateCard">
            {{ creatingCards ? '生成中...' : '立即生成' }}
          </button>
        </view>
      </view>
    </view>

    <view v-if="showGeneratedModal" class="generated-modal" @tap="closeGeneratedModal">
      <view class="generated-panel glass-card" @tap.stop="">
        <view class="generated-header">
          <text class="generated-title">生成卡密（{{ generatedCards.length }}）</text>
          <button class="generated-close" @tap="closeGeneratedModal">关闭</button>
        </view>
        <scroll-view scroll-y class="generated-list" :show-scrollbar="false">
          <view
            v-for="card in generatedCards"
            :key="card"
            class="generated-item copyable"
            @longpress="copyValue(card)"
          >
            {{ card }}
          </view>
        </scroll-view>
        <view class="generated-actions">
          <button class="generated-btn" @tap="copyGeneratedCards" :disabled="!generatedCards.length">复制全部</button>
          <button class="generated-btn primary" @tap="exportGeneratedCards" :disabled="!generatedCards.length">导出 TXT</button>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import DataTable from '@/components/DataTable.vue';
import StatusTag from '@/components/StatusTag.vue';
import SoftwarePicker from '@/components/SoftwarePicker.vue';
import DateTimePicker from '@/components/DateTimePicker.vue';
import { useAppStore } from '@/stores/app';
import { downloadTextFile } from '@/utils/download';
import { formatDurationFromSeconds } from '@/utils/time';
import type { CardSearchFilters, CardTypeInfo } from '@/common/types';

type CardActionType = 'enable' | 'disable' | 'unban';

type TableRow = {
  key: string;
  fullKey: string;
  cardType: string;
  status: 'enabled' | 'disabled' | 'unknown';
  statusText: string;
  owner: string;
  createdAt: string;
  activatedAt: string;
  expireAt: string;
  remark: string;
  ip: string;
  machineCodes: string[];
};

const appStore = useAppStore();
const { cardKeys, cardTypes, cardTotal, selectedSoftware, agents } = storeToRefs(appStore);
const cardFilters = appStore.cardFilters;

const filtersForm = reactive({
  keyword: '',
  cardType: '',
  status: '',
  agent: '',
  includeDescendants: true,
  machineCode: '',
  ip: '',
  startTime: '',
  endTime: ''
});

const searchTypeOptions = [
  { label: '智能匹配', value: 0 },
  { label: '备注 / 机器码', value: 1 },
  { label: 'IP 地址', value: 2 },
  { label: '按卡种', value: 3 }
];

const statusOptions = [
  { label: '全部', value: '' },
  { label: '启用', value: 'enabled' },
  { label: '禁用', value: 'disabled' },
  { label: '未知', value: 'unknown' }
];

const pageSizeOptions = [20, 50, 100, 200];

const cardTypeIndex = ref(0);
const statusIndex = ref(0);
const agentIndex = ref(0);
const searchTypeIndex = ref(0);
const pageSizeIndex = ref(1);
const showGeneratedModal = ref(false);
const showCreateCardModal = ref(false);
const generatedCards = ref<string[]>([]);
const generatedCardType = ref('');
const creatingCards = ref(false);
const createCardForm = reactive({
  cardTypeIndex: 0,
  quantity: '1',
  remarks: '',
  customPrefix: ''
});

const loading = computed(() => appStore.loading.cards);
const cardTypeOptions = computed(() => ['全部', ...cardTypes.value.map((item) => item.name)]);
const agentOptions = computed(() => ['全部', ...agents.value.map((item) => item.username)]);
const statusLabels = computed(() => statusOptions.map((item) => item.label));
const searchTypeLabels = computed(() => searchTypeOptions.map((item) => item.label));
const pageSizeLabels = computed(() => pageSizeOptions.map((item) => `${item} 条/页`));
const cardTypePickerLabels = computed(() => cardTypes.value.map((item) => formatCardTypeLabel(item)));

const rows = computed<TableRow[]>(() =>
  cardKeys.value.map((item) => ({
    key: item.key,
    fullKey: item.key,
    cardType: item.cardType,
    status: item.status,
    statusText: item.statusText,
    owner: item.owner,
    createdAt: item.createdAt,
    activatedAt: item.activatedAt,
    expireAt: item.expireAt,
    remark: item.remark ?? '',
    ip: item.ip ?? '--',
    machineCodes: item.machineCodes || []
  }))
);

const currentPage = computed(() => cardFilters.page ?? 1);
const pageSize = computed(() => cardFilters.limit ?? pageSizeOptions[pageSizeIndex.value] ?? 50);
const totalPages = computed(() => {
  const size = pageSize.value || 1;
  return Math.max(1, Math.ceil((cardTotal.value || 0) / size));
});

const summaryText = computed(() => `共 ${cardTotal.value} 条记录`);

const statusText = {
  enabled: '启用',
  disabled: '禁用',
  unknown: '未知'
} as const;

const statusMap = {
  enabled: 'success',
  disabled: 'warning',
  unknown: 'info'
} as const;

const columns = [
  { key: 'key', label: '卡密编号', style: 'min-width:260rpx' },
  { key: 'status', label: '状态', style: 'min-width:160rpx' },
  { key: 'cardType', label: '卡密类型', style: 'min-width:200rpx' },
  { key: 'owner', label: '归属代理', style: 'min-width:180rpx' },
  { key: 'ip', label: '最近 IP', style: 'min-width:180rpx' },
  { key: 'machineCodes', label: '绑定机器码', style: 'min-width:260rpx' },
  { key: 'createdAt', label: '创建时间', style: 'min-width:220rpx' },
  { key: 'activatedAt', label: '最近激活', style: 'min-width:220rpx' },
  { key: 'expireAt', label: '到期时间', style: 'min-width:220rpx' },
  { key: 'remark', label: '备注信息', style: 'min-width:220rpx' },
  { key: 'fullKey', label: '完整卡密', style: 'min-width:280rpx' },
  { key: 'operations', label: '操作', style: 'min-width:320rpx' }
];

const actionState = reactive({ key: '', action: '' });

const disableDescendants = computed(() => !!filtersForm.agent);

function formatCardTypeLabel(type: CardTypeInfo): string {
  const durationText = formatDurationFromSeconds(type.duration);
  return durationText ? `${type.name}（${durationText}）` : type.name;
}

function isActionBusy(key: string, action: string) {
  return actionState.key === key && actionState.action === action;
}

function setActionBusy(key: string, action: string) {
  actionState.key = key;
  actionState.action = action;
}

function clearActionBusy() {
  actionState.key = '';
  actionState.action = '';
}

function openGeneratedModal(cards: string[], typeName: string) {
  generatedCards.value = cards;
  generatedCardType.value = typeName;
  showGeneratedModal.value = true;
}

function closeGeneratedModal() {
  showGeneratedModal.value = false;
}

function closeCreateCardModal() {
  if (creatingCards.value) return;
  showCreateCardModal.value = false;
}

function onCreateCardTypeChange(event: UniApp.PickerChangeEvent) {
  createCardForm.cardTypeIndex = Number(event.detail.value) || 0;
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

function copyGeneratedCards() {
  if (!generatedCards.value.length) {
    uni.showToast({ title: '无可复制内容', icon: 'none' });
    return;
  }
  copyValue(generatedCards.value.join('\n'));
}

function exportGeneratedCards() {
  if (!generatedCards.value.length) {
    uni.showToast({ title: '暂无卡密', icon: 'none' });
    return;
  }
  const filename = `${generatedCardType.value || 'cards'}-${Date.now()}.txt`;
  downloadTextFile(filename, generatedCards.value.join('\n'));
  uni.showToast({ title: '已导出', icon: 'success' });
}

function formatPickerValue(date: Date) {
  const pad = (input: number) => input.toString().padStart(2, '0');
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

function syncFormFromFilters() {
  const keywordSource = cardFilters.keyword || (Array.isArray(cardFilters.keywords) ? cardFilters.keywords.join(' ') : '');
  filtersForm.keyword = keywordSource;
  filtersForm.cardType = cardFilters.cardType || '';
  filtersForm.status = cardFilters.status || '';
  filtersForm.agent = cardFilters.agent || '';
  filtersForm.includeDescendants = cardFilters.agent ? false : cardFilters.includeDescendants ?? true;
  filtersForm.machineCode = cardFilters.machineCode || '';
  filtersForm.ip = cardFilters.ip || '';
  filtersForm.startTime = normalizePickerValue(cardFilters.startTime);
  filtersForm.endTime = normalizePickerValue(cardFilters.endTime);

  const typeName = filtersForm.cardType || '全部';
  const typeIndex = cardTypeOptions.value.indexOf(typeName);
  cardTypeIndex.value = typeIndex >= 0 ? typeIndex : 0;

  const statusIdx = statusOptions.findIndex((item) => item.value === (filtersForm.status || ''));
  statusIndex.value = statusIdx >= 0 ? statusIdx : 0;

  const agentName = filtersForm.agent || '全部';
  const agentIdx = agentOptions.value.indexOf(agentName);
  agentIndex.value = agentIdx >= 0 ? agentIdx : 0;

  const searchIdx = searchTypeOptions.findIndex((item) => item.value === (typeof cardFilters.searchType === 'number' ? cardFilters.searchType : 0));
  searchTypeIndex.value = searchIdx >= 0 ? searchIdx : 0;

  const sizeIdx = pageSizeOptions.findIndex((value) => value === (cardFilters.limit ?? 50));
  pageSizeIndex.value = sizeIdx >= 0 ? sizeIdx : 1;
}
function onCardTypeChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value);
  cardTypeIndex.value = index;
  filtersForm.cardType = index === 0 ? '' : cardTypeOptions.value[index] || '';
}

function onStatusChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value);
  statusIndex.value = index;
  filtersForm.status = statusOptions[index]?.value ?? '';
}

function onAgentChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value);
  agentIndex.value = index;
  filtersForm.agent = agentOptions.value[index] === '全部' ? '' : agentOptions.value[index] || '';
  if (filtersForm.agent) {
    filtersForm.includeDescendants = false;
  } else {
    filtersForm.includeDescendants = true;
  }
}

function onSearchTypeChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value);
  searchTypeIndex.value = index;
}

function onDescendantChange(event: UniApp.SwitchChangeEvent) {
  if (disableDescendants.value) {
    filtersForm.includeDescendants = false;
    return;
  }
  filtersForm.includeDescendants = event.detail.value;
}

async function onPageSizeChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value);
  pageSizeIndex.value = index;
  const size = pageSizeOptions[index] ?? 50;
  try {
    await appStore.loadCardKeys({ limit: size, page: 1 });
  } catch (error) {
    console.error('onPageSizeChange error', error);
  }
}

async function submitSearch() {
  if (!selectedSoftware.value) {
    uni.showToast({ title: '请先选择软件位', icon: 'none' });
    return;
  }

  const searchType = searchTypeOptions[searchTypeIndex.value]?.value ?? 0;
  const startTime = normalizePickerValue(filtersForm.startTime);
  const endTime = normalizePickerValue(filtersForm.endTime);

  const payload: Partial<CardSearchFilters> = {
    keyword: filtersForm.keyword.trim(),
    cardType: filtersForm.cardType,
    status: (filtersForm.status as CardSearchFilters['status']) || '',
    agent: filtersForm.agent,
    includeDescendants: filtersForm.agent ? false : filtersForm.includeDescendants,
    machineCode: filtersForm.machineCode.trim(),
    ip: filtersForm.ip.trim(),
    startTime: startTime || undefined,
    endTime: endTime || undefined,
    searchType,
    page: 1,
    limit: pageSizeOptions[pageSizeIndex.value] ?? cardFilters.limit
  };

  try {
    await appStore.loadCardKeys(payload);
    uni.showToast({ title: '查询完成', icon: 'success' });
  } catch (error) {
    console.error('submitSearch error', error);
    uni.showToast({ title: '查询失败', icon: 'none' });
  }
}

async function resetFilters() {
  filtersForm.keyword = '';
  filtersForm.cardType = '';
  filtersForm.status = '';
  filtersForm.agent = '';
  filtersForm.includeDescendants = true;
  filtersForm.machineCode = '';
  filtersForm.ip = '';
  filtersForm.startTime = '';
  filtersForm.endTime = '';
  cardTypeIndex.value = 0;
  statusIndex.value = 0;
  agentIndex.value = 0;
  searchTypeIndex.value = 0;
  pageSizeIndex.value = pageSizeOptions.indexOf(50) >= 0 ? pageSizeOptions.indexOf(50) : 0;
  await submitSearch();
}

async function refreshList() {
  try {
    await appStore.loadCardKeys({ page: cardFilters.page, limit: cardFilters.limit });
    uni.showToast({ title: '已刷新', icon: 'none' });
  } catch (error) {
    console.error('refreshList error', error);
  }
}

async function changePage(page: number) {
  if (page < 1 || page > totalPages.value) return;
  try {
    await appStore.loadCardKeys({ page });
  } catch (error) {
    console.error('changePage error', error);
  }
}

function confirmAction(message: string) {
  return new Promise<boolean>((resolve) => {
    uni.showModal({
      title: '确认操作',
      content: message,
      confirmText: '确定',
      cancelText: '取消',
      success: (result) => resolve(!!result.confirm),
      fail: () => resolve(false)
    });
  });
}

async function handleStatusAction(row: TableRow, action: CardActionType) {
  if (!row?.key) return;
  const labels: Record<CardActionType, string> = {
    enable: '启用',
    disable: '禁用',
    unban: '解除封禁'
  };
  const confirmed = await confirmAction(`确认${labels[action]}卡密：${row.key}？`);
  if (!confirmed) {
    return;
  }
  setActionBusy(row.key, action);
  try {
    const message = await appStore.updateCardStatus({ cardKey: row.key, action });
    uni.showToast({ title: message || `${labels[action]}成功`, icon: 'success' });
  } catch (error) {
    console.error('handleStatusAction error', error);
    uni.showToast({ title: `${labels[action]}失败`, icon: 'none' });
  } finally {
    clearActionBusy();
  }
}

async function handleUnbind(row: TableRow) {
  if (!row?.key) return;
  const confirmed = await confirmAction(`确认解绑卡密：${row.key}？`);
  if (!confirmed) {
    return;
  }
  setActionBusy(row.key, 'unbind');
  try {
    const message = await appStore.unbindCard(row.key);
    uni.showToast({ title: message || '解绑成功', icon: 'success' });
  } catch (error) {
    console.error('handleUnbind error', error);
    uni.showToast({ title: '解绑失败', icon: 'none' });
  } finally {
    clearActionBusy();
  }
}

async function createCard() {
  await appStore.ensureReady();
  if (!selectedSoftware.value) {
    uni.showToast({ title: '请先选择软件位', icon: 'none' });
    return;
  }

  const types = cardTypes.value.length ? cardTypes.value : await appStore.loadCardTypes(true);
  if (!types || !types.length) {
    uni.showToast({ title: '未获取到卡密类型', icon: 'none' });
    return;
  }
  createCardForm.cardTypeIndex = Math.min(createCardForm.cardTypeIndex, Math.max(0, types.length - 1));
  createCardForm.quantity = '1';
  createCardForm.remarks = '';
  createCardForm.customPrefix = '';
  showCreateCardModal.value = true;
}

async function submitCreateCard() {
  if (creatingCards.value) return;

  const type = cardTypes.value[createCardForm.cardTypeIndex];
  if (!type) {
    uni.showToast({ title: '请选择卡密类型', icon: 'none' });
    return;
  }

  const quantityNumber = Number(createCardForm.quantity);
  const normalizedQuantity = Math.min(500, Math.max(1, Math.floor(quantityNumber)));
  if (!Number.isFinite(quantityNumber) || quantityNumber <= 0) {
    uni.showToast({ title: '请输入 1-500 的数量', icon: 'none' });
    return;
  }

  creatingCards.value = true;
  uni.showLoading({ title: '生成中...', mask: true });
  try {
    createCardForm.quantity = normalizedQuantity.toString();
    const result = await appStore.generateCards({
      cardType: type.name,
      quantity: normalizedQuantity,
      remarks: createCardForm.remarks.trim(),
      customPrefix: createCardForm.customPrefix.trim() || undefined
    });
    const generated = (result.generatedCards && result.generatedCards.length
      ? result.generatedCards
      : result.sampleCards || []) as string[];
    if (generated.length) {
      openGeneratedModal(generated, type.name || 'cards');
    }
    uni.showToast({ title: '生成成功', icon: 'success' });
    showCreateCardModal.value = false;
    createCardForm.remarks = '';
    createCardForm.customPrefix = '';
  } catch (error) {
    console.error('submitCreateCard error', error);
    uni.showToast({ title: '生成失败', icon: 'none' });
  } finally {
    creatingCards.value = false;
    uni.hideLoading();
  }
}

function exportCard() {
  if (!rows.value.length) {
    uni.showToast({ title: '暂无卡密可导出', icon: 'none' });
    return;
  }

  const header = ['卡密编号', '卡密类型', '状态', '归属代理', '最近IP', '绑定机器码', '创建时间', '最近激活', '到期时间', '备注信息'];
  const csvRows = rows.value.map((row) =>
    [
      row.key,
      row.cardType,
      row.statusText || statusText[row.status],
      row.owner,
      row.ip || '',
      row.machineCodes.join('|'),
      row.createdAt,
      row.activatedAt,
      row.expireAt,
      row.remark
    ]
      .map((value) => `"${(value ?? '').toString().replace(/"/g, '""')}"`)
      .join(',')
  );
  const content = ['\ufeff' + header.join(','), ...csvRows].join('\n');
  const filename = `cards-${Date.now()}.csv`;
  downloadTextFile(filename, content, 'text/csv;charset=utf-8');
  uni.showToast({ title: '导出成功', icon: 'success' });
}

watch(
  () => [
    cardFilters.keyword,
    cardFilters.cardType,
    cardFilters.status,
    cardFilters.agent,
    cardFilters.includeDescendants,
    cardFilters.machineCode,
    cardFilters.ip,
    cardFilters.startTime,
    cardFilters.endTime,
    cardFilters.searchType,
    cardFilters.limit
  ],
  () => {
    syncFormFromFilters();
  },
  { immediate: true }
);

watch(cardTypeOptions, () => syncFormFromFilters());
watch(agentOptions, () => syncFormFromFilters());

watch(
  () => cardTypes.value,
  (next) => {
    if (!next || !next.length) {
      showCreateCardModal.value = false;
      createCardForm.cardTypeIndex = 0;
      return;
    }
    if (createCardForm.cardTypeIndex >= next.length) {
      createCardForm.cardTypeIndex = 0;
    }
  },
  { immediate: true, deep: false }
);

onMounted(async () => {
  await appStore.ensureReady();
  try {
    await Promise.all([
      appStore.loadCardKeys(),
      appStore.loadCardTypes(),
      appStore.loadAgents({ limit: 200 })
    ]);
  } catch (error) {
    console.error('cards/onMounted error', error);
  } finally {
    syncFormFromFilters();
  }
});

watch(
  () => selectedSoftware.value,
  async (next, prev) => {
    if (!next || next === prev) return;
    filtersForm.keyword = '';
    filtersForm.cardType = '';
    filtersForm.status = '';
    filtersForm.agent = '';
    filtersForm.includeDescendants = true;
    filtersForm.machineCode = '';
    filtersForm.ip = '';
    filtersForm.startTime = '';
    filtersForm.endTime = '';
    cardTypeIndex.value = 0;
    statusIndex.value = 0;
    agentIndex.value = 0;
    searchTypeIndex.value = 0;
    pageSizeIndex.value = pageSizeOptions.indexOf(50) >= 0 ? pageSizeOptions.indexOf(50) : 0;
    try {
      await Promise.all([
        appStore.loadCardKeys({ page: 1 }),
        appStore.loadCardTypes(true),
        appStore.loadAgents({ limit: 200, page: 1, keyword: '' })
      ]);
    } catch (error) {
      console.error('selectedSoftware change error', error);
    } finally {
      syncFormFromFilters();
    }
  }
);
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
  gap: 18rpx;

  @media (min-width: 768px) {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
  }
}

.header-main {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
}

.title {
  font-size: 42rpx;
  font-weight: 600;
  color: #f8fafc;
  color: var(--text-primary);
}

.subtitle {
  font-size: 26rpx;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
}

.header-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 16rpx;
  align-items: center;
}

.header-btn {
  padding: 16rpx 30rpx;
  border-radius: 999rpx;
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.85), rgba(99, 102, 241, 0.85));
  background: var(--accent-gradient);
  color: #04101c;
  font-size: 26rpx;
  font-weight: 600;
  border: none;
}

.header-btn.ghost {
  background: transparent;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
}

.header-btn:disabled {
  opacity: 0.6;
}

.filters {
  padding: 32rpx;
  display: flex;
  flex-direction: column;
  gap: 28rpx;
}

.filters-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220rpx, 1fr));
  gap: 20rpx;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.field--full {
  grid-column: 1 / -1;
}

.label {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
}

.input,
.textarea,
.picker-display {
  width: 100%;
  padding: 20rpx 24rpx;
  border-radius: 18rpx;
  background: rgba(12, 19, 33, 0.55);
  background: var(--surface-light);
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  color: #f8fafc;
  color: var(--text-primary);
  font-size: 26rpx;
}

.textarea {
  min-height: 120rpx;
}

.picker-display {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.switch-field {
  flex-direction: row;
  align-items: center;
  gap: 18rpx;
}

.hint {
  font-size: 22rpx;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
}

.filter-actions {
  display: flex;
  gap: 16rpx;
  flex-wrap: wrap;
}

.btn {
  padding: 18rpx 36rpx;
  border-radius: 999rpx;
  border: none;
  font-size: 26rpx;
  font-weight: 600;
}

.btn.primary {
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.85), rgba(99, 102, 241, 0.85));
  background: var(--accent-gradient);
  color: #04101c;
}

.btn.ghost {
  background: transparent;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
}

.mini-btn {
  padding: 14rpx 28rpx;
  border-radius: 999rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: transparent;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
  font-size: 24rpx;
}

.mini-btn.ghost {
  color: #f8fafc;
  color: var(--text-primary);
}

.filters .btn:disabled,
.mini-btn:disabled {
  opacity: 0.6;
}

.machine-list {
  display: flex;
  flex-direction: column;
  gap: 10rpx;
}

.machine-item {
  padding: 10rpx 16rpx;
  border-radius: 12rpx;
  background: rgba(12, 19, 33, 0.55);
  background: var(--surface-light);
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  font-size: 24rpx;
  color: #f8fafc;
  color: var(--text-primary);
  word-break: break-all;
  line-height: 1.45;
}

.placeholder {
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
  font-size: 24rpx;
}

.key-header {
  display: flex;
  align-items: center;
  gap: 12rpx;
  min-width: 0;
}

.copy-text {
  display: inline-block;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: #f8fafc;
  color: var(--text-primary);
}

.full-key {
  color: #f8fafc;
  color: var(--text-primary);
  font-size: 26rpx;
  line-height: 1.5;
  word-break: break-all;
}

.remark {
  color: #f8fafc;
  color: var(--text-primary);
  font-size: 24rpx;
  line-height: 1.5;
}

.action-group {
  display: flex;
  flex-wrap: wrap;
  gap: 12rpx;
}

.action-btn {
  padding: 12rpx 22rpx;
  border-radius: 12rpx;
  font-size: 24rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: rgba(12, 19, 33, 0.55);
  background: var(--surface-light);
  color: #f8fafc;
  color: var(--text-primary);
}

.action-btn.primary {
  border-color: rgba(59, 130, 246, 0.4);
  background: rgba(59, 130, 246, 0.16);
  color: #dbeafe;
}

.action-btn.warn {
  border-color: rgba(248, 113, 113, 0.45);
  background: rgba(248, 113, 113, 0.12);
  color: #f87171;
  color: var(--danger-color);
}

.action-btn.ghost {
  background: transparent;
}

.pagination {
  padding: 26rpx 32rpx;
  display: flex;
  flex-direction: column;
  gap: 18rpx;

  @media (min-width: 768px) {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
  }
}

.page-info {
  font-size: 26rpx;
  color: #f8fafc;
  color: var(--text-primary);
}

.page-actions {
  display: flex;
  gap: 16rpx;
}

.page-btn {
  padding: 16rpx 36rpx;
  border-radius: 999rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: transparent;
  color: #f8fafc;
  color: var(--text-primary);
  font-size: 24rpx;
}

.create-card-modal {
  position: fixed;
  inset: 0;
  background: rgba(5, 7, 15, 0.65);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40rpx;
  z-index: 998;
}

.create-card-panel {
  width: 100%;
  max-width: 640rpx;
  display: flex;
  flex-direction: column;
  gap: 24rpx;
}

.create-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.create-card-title {
  font-size: 32rpx;
  font-weight: 600;
  color: #f8fafc;
  color: var(--text-primary);
}

.create-card-close {
  padding: 10rpx 22rpx;
  border-radius: 999rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: transparent;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
  font-size: 24rpx;
}

.create-card-body {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
}

.create-card-actions {
  display: flex;
  justify-content: flex-end;
  gap: 16rpx;
}

.generated-modal {
  position: fixed;
  inset: 0;
  background: rgba(5, 7, 15, 0.65);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40rpx;
  z-index: 999;
}

.generated-panel {
  width: 100%;
  max-width: 680rpx;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  gap: 24rpx;
}

.generated-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.generated-title {
  font-size: 32rpx;
  font-weight: 600;
  color: #f8fafc;
  color: var(--text-primary);
}

.generated-close {
  padding: 10rpx 22rpx;
  border-radius: 999rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: transparent;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
  font-size: 24rpx;
}

.generated-list {
  max-height: 480rpx;
}

.generated-item {
  padding: 16rpx 20rpx;
  border-radius: 14rpx;
  background: rgba(12, 19, 33, 0.55);
  background: var(--surface-light);
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  font-size: 26rpx;
  color: #f8fafc;
  color: var(--text-primary);
  word-break: break-all;
}

.generated-item + .generated-item {
  margin-top: 12rpx;
}

.generated-actions {
  display: flex;
  gap: 16rpx;
  justify-content: flex-end;
}

.generated-btn {
  padding: 14rpx 28rpx;
  border-radius: 999rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: transparent;
  color: #f8fafc;
  color: var(--text-primary);
  font-size: 24rpx;
}

.generated-btn.primary {
  border-color: rgba(59, 130, 246, 0.35);
  background: rgba(59, 130, 246, 0.14);
  color: #60a5fa;
}
</style>