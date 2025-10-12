<template>
  <view class="page">
    <view class="header glass-card">
      <view class="header-main">
        <text class="title">代理管理</text>
        <text class="subtitle">掌握渠道余额、库存及启用状态</text>
      </view>
      <view class="header-actions">
        <SoftwarePicker />
        <button class="btn ghost" :disabled="loading" @tap="refreshList">刷新</button>
        <button class="btn primary" :disabled="creating" @tap="openCreateAgent">{{ creating ? '创建中...' : '添加代理' }}</button>
      </view>
    </view>

    <view class="filters glass-card">
      <view class="filters-grid">
        <view class="field keyword-field">
          <text class="label">搜索关键字</text>
          <input
            class="input"
            v-model="filtersForm.keyword"
            placeholder="用户名 / 备注关键字"
            confirm-type="search"
            @confirm="searchAgents"
          />
        </view>
        <view class="field">
          <text class="label">状态筛选</text>
          <picker mode="selector" :range="statusLabels" :value="statusIndex" @change="onStatusChange">
            <view class="picker-display">{{ statusLabels[statusIndex] }}</view>
          </picker>
        </view>
        <view class="field">
          <text class="label">每页条数</text>
          <picker mode="selector" :range="pageSizeLabels" :value="pageSizeIndex" @change="onPageSizeChange">
            <view class="picker-display">{{ pageSizeLabels[pageSizeIndex] }}</view>
          </picker>
        </view>
      </view>
      <view class="filter-actions">
        <button class="btn primary" :disabled="loading" @tap="searchAgents">{{ loading ? '查询中...' : '搜索' }}</button>
        <button class="btn ghost" :disabled="loading" @tap="resetFilters">重置</button>
      </view>
    </view>

    <DataTable
      title="子代理列表"
      :subtitle="summaryText"
      :columns="columns"
      :rows="rows"
      :loading="loading"
      layout="stack"
      primary-column="username"
      operations-column="operations"
    >
      <template #username="{ row }">
        <view class="agent-header">
          <text class="agent-name copyable" @longpress="copyValue(row.username)">{{ row.username }}</text>
          <StatusTag :status="statusMap[row.status]" :label="statusText[row.status]" />
        </view>
      </template>
      <template #status="{ row }">
        <StatusTag :status="statusMap[row.status]" :label="statusText[row.status]" />
      </template>
      <template #cardTypesDisplay="{ row }">
        <view v-if="row.cardTypes.length" class="tag-list">
          <view v-for="item in row.cardTypes" :key="item" class="tag">{{ item }}</view>
        </view>
        <text v-else class="placeholder">默认全部</text>
      </template>
      <template #remark="{ row }">
        <text v-if="row.remark" class="remark">{{ row.remark }}</text>
        <text v-else class="placeholder">—</text>
      </template>
      <template #operations="{ row }">
        <view class="action-group">
          <button
            class="action-btn"
            :disabled="loading || isActionBusy(row.username, 'enable')"
            @tap="toggleStatus(row, true)"
          >
            {{ isActionBusy(row.username, 'enable') ? '处理中...' : '启用' }}
          </button>
          <button
            class="action-btn warn"
            :disabled="loading || isActionBusy(row.username, 'disable')"
            @tap="toggleStatus(row, false)"
          >
            {{ isActionBusy(row.username, 'disable') ? '处理中...' : '禁用' }}
          </button>
          <button
            class="action-btn"
            :disabled="loading || isActionBusy(row.username, 'adjust')"
            @tap="openAdjust(row)"
          >
            {{ isActionBusy(row.username, 'adjust') ? '处理中...' : '加款/加时' }}
          </button>
          <button
            class="action-btn"
            :disabled="loading || isActionBusy(row.username, 'remark')"
            @tap="openRemark(row)"
          >
            {{ isActionBusy(row.username, 'remark') ? '处理中...' : '备注' }}
          </button>
          <button
            class="action-btn"
            :disabled="loading || isActionBusy(row.username, 'password')"
            @tap="openPassword(row)"
          >
            {{ isActionBusy(row.username, 'password') ? '处理中...' : '重置密码' }}
          </button>
          <button
            class="action-btn"
            :disabled="loading || isActionBusy(row.username, 'cardTypes')"
            @tap="openAssignCardTypes(row)"
          >
            {{ isActionBusy(row.username, 'cardTypes') ? '处理中...' : '卡种配置' }}
          </button>
          <button
            class="action-btn danger"
            :disabled="loading || isActionBusy(row.username, 'delete')"
            @tap="deleteAgent(row)"
          >
            {{ isActionBusy(row.username, 'delete') ? '处理中...' : '删除' }}
          </button>
        </view>
      </template>
    </DataTable>

    <view class="pagination glass-card" v-if="totalPages > 1 || agentTotal">
      <view class="page-info">第 {{ currentPage }} / {{ totalPages }} 页 · 共 {{ agentTotal }} 条</view>
      <view class="page-actions">
        <button class="page-btn" :disabled="currentPage <= 1 || loading" @tap="changePage(currentPage - 1)">上一页</button>
        <button class="page-btn" :disabled="currentPage >= totalPages || loading" @tap="changePage(currentPage + 1)">下一页</button>
      </view>
    </view>

    <view v-if="showCreateAgentModal" class="agent-create-modal" @tap="closeCreateAgentModal">
      <view class="agent-create-panel glass-card" @tap.stop="">
        <view class="agent-create-header">
          <text class="agent-create-title">创建代理</text>
          <button class="agent-create-close" :disabled="creating" @tap="closeCreateAgentModal">关闭</button>
        </view>
        <view class="agent-create-body">
          <view class="agent-create-field">
            <text class="agent-create-label">代理账号</text>
            <input class="agent-create-input" v-model="createAgentForm.username" placeholder="请输入代理账号" />
          </view>
          <view class="agent-create-field">
            <text class="agent-create-label">初始密码</text>
            <input class="agent-create-input" password v-model="createAgentForm.password" placeholder="请输入初始密码" />
          </view>
          <view class="agent-create-grid">
            <view class="agent-create-field">
              <text class="agent-create-label">初始余额(元)</text>
              <input class="agent-create-input" type="digit" v-model="createAgentForm.balance" placeholder="如 0" />
            </view>
            <view class="agent-create-field">
              <text class="agent-create-label">时间库存(小时)</text>
              <input class="agent-create-input" type="digit" v-model="createAgentForm.timeStock" placeholder="如 0" />
            </view>
            <view class="agent-create-field">
              <text class="agent-create-label">折扣(默认100)</text>
              <input class="agent-create-input" type="number" v-model="createAgentForm.parities" placeholder="100 表示不打折" />
            </view>
          </view>
          <view class="agent-create-field">
            <text class="agent-create-label">备注信息</text>
            <textarea
              class="agent-create-textarea"
              v-model="createAgentForm.remark"
              placeholder="可填写备注，留空则不设置"
              auto-height
            ></textarea>
          </view>
          <view class="agent-create-field">
            <text class="agent-create-label">授权卡种</text>
            <view class="agent-create-checkboxes">
              <text v-if="!cardTypeChoices.length" class="agent-create-hint">暂无卡种，可稍后在详情中配置</text>
              <checkbox-group v-else :value="createAgentForm.cardTypes" @change="onAgentCardTypesChange">
                <label v-for="item in cardTypeChoices" :key="item.name" class="agent-create-checkbox">
                  <checkbox :value="item.name" />
                  <text class="agent-create-checkbox-label">{{ item.label }}</text>
                </label>
              </checkbox-group>
            </view>
          </view>
        </view>
        <view class="agent-create-actions">
          <button class="btn ghost" :disabled="creating" @tap="closeCreateAgentModal">取消</button>
          <button class="btn primary" :disabled="creating" @tap="submitCreateAgent">
            {{ creating ? '创建中...' : '确认创建' }}
          </button>
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
import { useAppStore } from '@/stores/app';
import { formatDurationFromSeconds } from '@/utils/time';
import type { CardTypeInfo } from '@/common/types';

type AgentRow = {
  username: string;
  status: 'enabled' | 'disabled';
  balance: string;
  timeStock: number;
  parities: number;
  totalParities: number;
  remark: string;
  cardTypes: string[];
  cardTypesDisplay: string;
};

interface PromptOptions {
  title: string;
  placeholder?: string;
  defaultValue?: string;
  optional?: boolean;
}

const appStore = useAppStore();
const { agents, selectedSoftware, agentTotal, cardTypes } = storeToRefs(appStore);
const agentFilters = appStore.agentFilters;

const filtersForm = reactive({ keyword: '' });
const statusOptions = [
  { label: '全部', value: 'all' },
  { label: '启用', value: 'enabled' },
  { label: '禁用', value: 'disabled' }
];
const statusIndex = ref(0);

const pageSizeOptions = [20, 50, 100];
const pageSizeIndex = ref(1);

const loading = computed(() => appStore.loading.agents);
const statusLabels = computed(() => statusOptions.map((item) => item.label));
const pageSizeLabels = computed(() => pageSizeOptions.map((size) => `${size} 条/页`));

const actionState = reactive({ username: '', type: '' });
const creating = ref(false);
const showCreateAgentModal = ref(false);
const createAgentForm = reactive({
  username: '',
  password: '',
  balance: '',
  timeStock: '',
  parities: '100',
  remark: '',
  cardTypes: [] as string[]
});
const cardTypeChoices = computed(() => cardTypes.value.map((item) => ({
  name: item.name,
  label: formatAgentCardTypeLabel(item)
})));

const rows = computed<AgentRow[]>(() => {
  const statusValue = statusOptions[statusIndex.value]?.value ?? 'all';
  return agents.value
    .filter((item) => (statusValue === 'all' ? true : item.status === statusValue))
    .map((item) => ({
      username: item.username,
      status: item.status,
      balance: item.balance.toFixed(2),
      timeStock: item.timeStock,
      parities: item.parities,
      totalParities: item.totalParities,
      remark: item.remark ?? '',
      cardTypes: item.cardTypes ?? [],
      cardTypesDisplay: item.cardTypes && item.cardTypes.length ? item.cardTypes.join('、') : '默认全部'
    }));
});

const summaryText = computed(() => {
  const total = agentTotal.value || 0;
  const filtered = rows.value.length;
  return filtered === total ? `共 ${total} 条记录` : `共 ${total} 条记录 · 当前筛选 ${filtered} 条`;
});

const columns = [
  { key: 'username', label: '代理账号', style: 'min-width:200rpx' },
  { key: 'status', label: '状态', style: 'min-width:160rpx' },
  { key: 'balance', label: '余额(元)', style: 'min-width:160rpx' },
  { key: 'timeStock', label: '时间库存(小时)', style: 'min-width:220rpx' },
  { key: 'parities', label: '剩余授权', style: 'min-width:180rpx' },
  { key: 'totalParities', label: '累计授权', style: 'min-width:200rpx' },
  { key: 'cardTypesDisplay', label: '可售卡种', style: 'min-width:240rpx' },
  { key: 'remark', label: '备注信息', style: 'min-width:240rpx' },
  { key: 'operations', label: '操作', style: 'min-width:360rpx' }
];

const currentPage = computed(() => agentFilters.page ?? 1);
const pageSize = computed(() => agentFilters.limit ?? pageSizeOptions[pageSizeIndex.value] ?? 50);
const totalPages = computed(() => {
  const size = pageSize.value || 1;
  return Math.max(1, Math.ceil((agentTotal.value || 0) / size));
});

const statusText = {
  enabled: '启用',
  disabled: '禁用'
} as const;

const statusMap = {
  enabled: 'success',
  disabled: 'warning'
} as const;

function formatAgentCardTypeLabel(type: CardTypeInfo): string {
  const durationText = formatDurationFromSeconds(type.duration);
  return durationText ? `${type.name}（${durationText}）` : type.name;
}

function resetCreateAgentForm() {
  createAgentForm.username = '';
  createAgentForm.password = '';
  createAgentForm.balance = '0';
  createAgentForm.timeStock = '0';
  createAgentForm.parities = '100';
  createAgentForm.remark = '';
  createAgentForm.cardTypes = [];
}

function closeCreateAgentModal() {
  if (creating.value) return;
  showCreateAgentModal.value = false;
  resetCreateAgentForm();
}

function onAgentCardTypesChange(event: UniApp.CheckboxGroupChangeEvent) {
  const values = Array.isArray(event.detail.value) ? (event.detail.value as string[]) : [];
  createAgentForm.cardTypes = Array.from(new Set(values));
}
function isActionBusy(username: string, type: string) {
  return actionState.username === username && actionState.type === type;
}

function setActionBusy(username: string, type: string) {
  actionState.username = username;
  actionState.type = type;
}

function clearActionBusy() {
  actionState.username = '';
  actionState.type = '';
}

async function promptInput(options: PromptOptions) {
  const { title, placeholder = '', defaultValue = '', optional = false } = options;
  const result = await new Promise<UniApp.ShowModalRes>((resolve) => {
    uni.showModal({
      title,
      content: defaultValue,
      editable: true,
      placeholderText: placeholder,
      confirmText: '确定',
      cancelText: optional ? '跳过' : '取消',
      success: resolve,
      fail: () => resolve({ confirm: false, cancel: true } as UniApp.ShowModalRes)
    });
  });
  if (!result.confirm) {
    return optional ? '' : null;
  }
  return (result.content ?? '').trim();
}

function syncFiltersFromStore() {
  filtersForm.keyword = agentFilters.keyword ?? '';
  const sizeIdx = pageSizeOptions.findIndex((value) => value === (agentFilters.limit ?? 50));
  pageSizeIndex.value = sizeIdx >= 0 ? sizeIdx : 1;
}

async function searchAgents() {
  try {
    await appStore.loadAgents({
      keyword: filtersForm.keyword.trim(),
      page: 1,
      limit: pageSizeOptions[pageSizeIndex.value] ?? agentFilters.limit
    });
    uni.showToast({ title: '查询完成', icon: 'success' });
  } catch (error) {
    console.error('searchAgents error', error);
    uni.showToast({ title: '查询失败', icon: 'none' });
  }
}

async function resetFilters() {
  filtersForm.keyword = '';
  statusIndex.value = 0;
  pageSizeIndex.value = pageSizeOptions.indexOf(50) >= 0 ? pageSizeOptions.indexOf(50) : 0;
  await searchAgents();
}

async function refreshList() {
  try {
    await appStore.loadAgents({
      page: agentFilters.page,
      limit: agentFilters.limit,
      keyword: agentFilters.keyword
    });
    uni.showToast({ title: '已刷新', icon: 'none' });
  } catch (error) {
    console.error('refreshList error', error);
  }
}

async function changePage(page: number) {
  if (page < 1 || page > totalPages.value) return;
  try {
    await appStore.loadAgents({ page });
  } catch (error) {
    console.error('changePage error', error);
  }
}

async function onPageSizeChange(event: UniApp.PickerChangeEvent) {
  const index = Number(event.detail.value);
  pageSizeIndex.value = index;
  const size = pageSizeOptions[index] ?? 50;
  try {
    await appStore.loadAgents({ limit: size, page: 1, keyword: filtersForm.keyword.trim() });
  } catch (error) {
    console.error('onPageSizeChange error', error);
  }
}

function onStatusChange(event: UniApp.PickerChangeEvent) {
  statusIndex.value = Number(event.detail.value);
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

function copyValue(value?: string) {
  const text = (value ?? '').toString().trim();
  if (!text) {
    uni.showToast({ title: '无可复制内容', icon: 'none' });
    return;
  }
  uni.setClipboardData({
    data: text,
    success: () => uni.showToast({ title: '已复制', icon: 'success', duration: 800 })
  });
}

async function toggleStatus(row: AgentRow, enable: boolean) {
  const label = enable ? '启用' : '禁用';
  const confirmed = await confirmAction(`确认${label}代理：${row.username}？`);
  if (!confirmed) return;
  setActionBusy(row.username, enable ? 'enable' : 'disable');
  try {
    const message = await appStore.toggleAgentStatus({ usernames: [row.username], enable });
    uni.showToast({ title: message || `${label}成功`, icon: 'success' });
  } catch (error) {
    console.error('toggleStatus error', error);
    uni.showToast({ title: `${label}失败`, icon: 'none' });
  } finally {
    clearActionBusy();
  }
}

async function openAdjust(row: AgentRow) {
  const balanceInput = await promptInput({ title: '加款金额 (元)', placeholder: '可填写正负数', defaultValue: '0' });
  if (balanceInput === null) return;
  const timeInput = await promptInput({ title: '加时长 (小时)', placeholder: '可填写正负数', defaultValue: '0' });
  if (timeInput === null) return;
  const balance = parseFloat(balanceInput) || 0;
  const timeStock = parseFloat(timeInput) || 0;
  if (!balance && !timeStock) {
    uni.showToast({ title: '请至少输入一项调整数值', icon: 'none' });
    return;
  }
  setActionBusy(row.username, 'adjust');
  try {
    const message = await appStore.adjustAgentBalance({ username: row.username, balance, timeStock });
    uni.showToast({ title: message || '调整成功', icon: 'success' });
  } catch (error) {
    console.error('openAdjust error', error);
    uni.showToast({ title: '调整失败', icon: 'none' });
  } finally {
    clearActionBusy();
  }
}

async function openRemark(row: AgentRow) {
  const remarkInput = await promptInput({ title: '修改备注', placeholder: '请输入备注内容', defaultValue: row.remark, optional: true });
  if (remarkInput === null) return;
  setActionBusy(row.username, 'remark');
  try {
    const message = await appStore.updateAgentRemark({ username: row.username, remark: remarkInput });
    uni.showToast({ title: message || '备注已更新', icon: 'success' });
  } catch (error) {
    console.error('openRemark error', error);
    uni.showToast({ title: '备注更新失败', icon: 'none' });
  } finally {
    clearActionBusy();
  }
}

async function openPassword(row: AgentRow) {
  const passwordInput = await promptInput({ title: '重置密码', placeholder: '请输入新的登录密码' });
  if (passwordInput === null) return;
  if (!passwordInput) {
    uni.showToast({ title: '密码不能为空', icon: 'none' });
    return;
  }
  setActionBusy(row.username, 'password');
  try {
    const message = await appStore.updateAgentPassword({ username: row.username, password: passwordInput });
    uni.showToast({ title: message || '密码已重置', icon: 'success' });
  } catch (error) {
    console.error('openPassword error', error);
    uni.showToast({ title: '密码重置失败', icon: 'none' });
  } finally {
    clearActionBusy();
  }
}

async function openAssignCardTypes(row: AgentRow) {
  try {
    if (!cardTypes.value.length) {
      await appStore.loadCardTypes();
    }
  } catch (error) {
    console.error('loadCardTypes error', error);
  }
  const candidates = cardTypes.value.map((item) => item.name);
  const hint = candidates.length ? `可选：${candidates.slice(0, 6).join('、')}` : '请输入卡种名称，逗号分隔';
  const input = await promptInput({
    title: '配置授权卡种',
    placeholder: hint,
    defaultValue: row.cardTypes.join(','),
    optional: true
  });
  if (input === null) return;
  const cardTypeList = input
    ? input
        .split(/[，,\s]+/)
        .map((item) => item.trim())
        .filter((item) => item.length > 0)
    : [];
  setActionBusy(row.username, 'cardTypes');
  try {
    const message = await appStore.assignAgentCardTypes({ username: row.username, cardTypes: cardTypeList });
    uni.showToast({ title: message || '卡种配置成功', icon: 'success' });
  } catch (error) {
    console.error('openAssignCardTypes error', error);
    uni.showToast({ title: '卡种配置失败', icon: 'none' });
  } finally {
    clearActionBusy();
  }
}

async function deleteAgent(row: AgentRow) {
  const confirmed = await confirmAction(`确认删除代理：${row.username}？此操作不可恢复。`);
  if (!confirmed) return;
  setActionBusy(row.username, 'delete');
  try {
    const message = await appStore.deleteAgents([row.username]);
    uni.showToast({ title: message || '已删除', icon: 'success' });
  } catch (error) {
    console.error('deleteAgent error', error);
    uni.showToast({ title: '删除失败', icon: 'none' });
  } finally {
    clearActionBusy();
  }
}

async function openCreateAgent() {
  await appStore.ensureReady();
  if (!selectedSoftware.value) {
    uni.showToast({ title: '请先选择软件位', icon: 'none' });
    return;
  }

  try {
    if (!cardTypes.value.length) {
      await appStore.loadCardTypes();
    }
  } catch (error) {
    console.error('loadCardTypes error', error);
  }

  resetCreateAgentForm();
  showCreateAgentModal.value = true;
}

async function submitCreateAgent() {
  if (creating.value) return;

  const username = createAgentForm.username.trim();
  if (!username) {
    uni.showToast({ title: '账号不能为空', icon: 'none' });
    return;
  }

  const password = createAgentForm.password.trim();
  if (!password) {
    uni.showToast({ title: '密码不能为空', icon: 'none' });
    return;
  }

  const balance = parseFloat(createAgentForm.balance || '0') || 0;
  const timeStock = parseFloat(createAgentForm.timeStock || '0') || 0;
  const parities = Math.max(0, parseFloat(createAgentForm.parities || '100') || 100);
  const remarks = createAgentForm.remark.trim();
  const cardTypeList = createAgentForm.cardTypes.slice();

  creating.value = true;
  try {
    const message = await appStore.createAgent({
      username,
      password,
      balance,
      timeStock,
      parities,
      totalParities: parities,
      remarks,
      cardTypes: cardTypeList
    });
    uni.showToast({ title: message || '创建成功', icon: 'success' });
    showCreateAgentModal.value = false;
    resetCreateAgentForm();
  } catch (error) {
    console.error('submitCreateAgent error', error);
    uni.showToast({ title: '创建失败', icon: 'none' });
  } finally {
    creating.value = false;
  }
}

watch(
  () => [agentFilters.keyword, agentFilters.limit],
  () => {
    syncFiltersFromStore();
  },
  { immediate: true }
);

watch(
  () => cardTypeChoices.value,
  (next) => {
    const available = new Set(next.map((item) => item.name));
    createAgentForm.cardTypes = createAgentForm.cardTypes.filter((name) => available.has(name));
  },
  { immediate: true }
);

onMounted(async () => {
  await appStore.ensureReady();
  try {
    await appStore.loadAgents();
  } catch (error) {
    console.error('agents/onMounted error', error);
  }
  syncFiltersFromStore();
});

watch(
  () => selectedSoftware.value,
  async (next, prev) => {
    if (!next || next === prev) return;
    filtersForm.keyword = '';
    statusIndex.value = 0;
    pageSizeIndex.value = pageSizeOptions.indexOf(50) >= 0 ? pageSizeOptions.indexOf(50) : 0;
    try {
      await appStore.loadAgents({ page: 1, keyword: '', limit: pageSizeOptions[pageSizeIndex.value] });
    } catch (error) {
      console.error('selectedSoftware change error', error);
    }
    syncFiltersFromStore();
  }
);
</script>

<style scoped lang="scss">
.page {
  display: flex;
  flex-direction: column;
  gap: 32rpx;
  padding: 36rpx 28rpx 72rpx;
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

.btn {
  padding: 16rpx 32rpx;
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

.btn:disabled {
  opacity: 0.6;
}

.filters {
  padding: 32rpx;
  display: flex;
  flex-direction: column;
  gap: 24rpx;
}

.filters-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240rpx, 1fr));
  gap: 20rpx;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.keyword-field {
  grid-column: 1 / -1;
}

.label {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
}

.input,
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

.picker-display {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.filter-actions {
  display: flex;
  gap: 16rpx;
  flex-wrap: wrap;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 10rpx;
}

.tag {
  padding: 8rpx 14rpx;
  border-radius: 12rpx;
  background: rgba(12, 19, 33, 0.55);
  background: var(--surface-light);
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  font-size: 24rpx;
  color: #f8fafc;
  color: var(--text-primary);
}

.placeholder {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
}

.agent-header {
  display: flex;
  align-items: center;
  gap: 12rpx;
  min-width: 0;
}

.agent-name {
  font-size: 28rpx;
  font-weight: 600;
  color: #f8fafc;
  color: var(--text-primary);
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.remark {
  font-size: 24rpx;
  color: #f8fafc;
  color: var(--text-primary);
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
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: rgba(12, 19, 33, 0.55);
  background: var(--surface-light);
  color: #f8fafc;
  color: var(--text-primary);
  font-size: 24rpx;
}

.action-btn.warn {
  border-color: rgba(248, 113, 113, 0.45);
  background: rgba(248, 113, 113, 0.12);
  color: #f87171;
  color: var(--danger-color);
}

.action-btn.danger {
  border-color: rgba(239, 68, 68, 0.45);
  background: rgba(239, 68, 68, 0.12);
  color: #f87171;
  color: var(--danger-color);
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

.agent-create-modal {
  position: fixed;
  inset: 0;
  background: rgba(5, 7, 15, 0.65);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40rpx;
  z-index: 998;
}

.agent-create-panel {
  width: 100%;
  max-width: 700rpx;
  display: flex;
  flex-direction: column;
  gap: 24rpx;
}

.agent-create-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.agent-create-title {
  font-size: 32rpx;
  font-weight: 600;
  color: #f8fafc;
  color: var(--text-primary);
}

.agent-create-close {
  padding: 10rpx 22rpx;
  border-radius: 999rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: transparent;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
  font-size: 24rpx;
}

.agent-create-body {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
}

.agent-create-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200rpx, 1fr));
  gap: 16rpx;
}

.agent-create-field {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}

.agent-create-label {
  font-size: 26rpx;
  color: rgba(226, 232, 240, 0.78);
  color: var(--text-secondary);
}

.agent-create-input {
  padding: 16rpx 22rpx;
  border-radius: 14rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: rgba(12, 19, 33, 0.55);
  background: var(--surface-light);
  color: #f8fafc;
  color: var(--text-primary);
  font-size: 26rpx;
}

.agent-create-textarea {
  padding: 16rpx 22rpx;
  border-radius: 14rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: rgba(12, 19, 33, 0.55);
  background: var(--surface-light);
  color: #f8fafc;
  color: var(--text-primary);
  font-size: 26rpx;
  min-height: 120rpx;
}

.agent-create-checkboxes {
  display: flex;
  flex-direction: column;
  gap: 16rpx;
}

.agent-create-hint {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
}

.agent-create-checkbox {
  display: flex;
  align-items: center;
  gap: 12rpx;
  padding: 12rpx 16rpx;
  border-radius: 12rpx;
  background: rgba(12, 19, 33, 0.55);
  background: var(--surface-light);
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
}

.agent-create-checkbox-label {
  font-size: 24rpx;
  color: #f8fafc;
  color: var(--text-primary);
}

.agent-create-actions {
  display: flex;
  justify-content: flex-end;
  gap: 16rpx;
}
</style>