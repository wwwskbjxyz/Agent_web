<template>
  <view class="table-container glass-card" :class="layoutClass">
    <view class="table-header">
      <view>
        <text class="table-title">{{ title }}</text>
        <text class="table-subtitle" v-if="subtitle">{{ subtitle }}</text>
      </view>
      <view class="table-actions">
        <slot name="actions" />
      </view>
    </view>
    <scroll-view v-if="layout === 'table'" scroll-x class="table-scroll" :show-scrollbar="false">
      <view class="table-wrapper" :style="wrapperStyle">
        <view class="table-row table-row--head" :style="rowStyle">
          <view v-for="column in columns" :key="column.key" class="table-cell" :style="column.style">
            {{ column.label }}
          </view>
        </view>
        <view v-if="loading" class="table-empty">加载中...</view>
        <view v-else-if="!rows.length" class="table-empty">暂无数据</view>
        <template v-else>
          <view v-for="(row, rowIndex) in rows" :key="rowIndex" class="table-row" :style="rowStyle">
            <view v-for="column in columns" :key="column.key" class="table-cell" :style="column.style">
              <slot :name="column.key" :row="row">
                {{ formatValue(row[column.key]) }}
              </slot>
            </view>
          </view>
        </template>
      </view>
    </scroll-view>
    <view v-else class="stack-list">
      <view v-if="loading" class="table-empty">加载中...</view>
      <view v-else-if="!rows.length" class="table-empty">暂无数据</view>
      <template v-else>
        <view
          v-for="(row, rowIndex) in rows"
          :key="getRowId(row, rowIndex)"
          class="stack-row glass-light"
        >
          <view class="stack-header">
            <view class="stack-title">
              <slot :name="primaryKey" :row="row">
                {{ formatValue(primaryKey ? row[primaryKey] : '') }}
              </slot>
            </view>
            <button
              v-if="detailColumns.length && collapseDetails"
              class="stack-toggle"
              @tap="toggleRow(getRowId(row, rowIndex))"
            >
              {{ isExpanded(getRowId(row, rowIndex)) ? '收起' : '详情' }}
            </button>
          </view>
        <view v-if="hasOperationsSlot" class="stack-operations">
          <slot
            v-if="operationsKey === 'operations'"
            name="operations"
            :row="row"
          >
            {{ formatValue(row[operationsKey]) }}
          </slot>
          <slot v-else :name="operationsKey" :row="row">
            {{ formatValue(row[operationsKey]) }}
          </slot>
        </view>
          <view
            v-if="!collapseDetails || isExpanded(getRowId(row, rowIndex))"
            class="stack-body"
          >
            <view v-for="column in detailColumns" :key="column.key" class="stack-field">
              <text class="stack-label">{{ column.label }}</text>
              <view class="stack-value">
                <slot :name="column.key" :row="row">
                  {{ formatValue(row[column.key]) }}
                </slot>
              </view>
            </view>
          </view>
        </view>
      </template>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, ref, watch, useSlots } from 'vue';

interface TableColumn {
  key: string;
  label: string;
  style?: string;
}

const props = withDefaults(
  defineProps<{
    title: string;
    subtitle?: string;
    columns: TableColumn[];
    rows: Record<string, any>[];
    loading?: boolean;
    layout?: 'table' | 'stack';
    primaryColumn?: string;
    operationsColumn?: string;
    collapseDetails?: boolean;
  }>(),
  {
    loading: false,
    subtitle: '',
    layout: 'table',
    primaryColumn: '',
    operationsColumn: '',
    collapseDetails: true
  }
);

const columnCount = computed(() => (props.columns?.length ? props.columns.length : 1));
const rowStyle = computed(() => ({
  gridTemplateColumns: `repeat(${columnCount.value}, minmax(200rpx, auto))`
}));
const wrapperStyle = computed(() => ({
  '--column-count': columnCount.value,
  minWidth: `${Math.max(columnCount.value * 220, 600)}rpx`
}));

const layout = computed(() => props.layout ?? 'table');
const layoutClass = computed(() => (layout.value === 'stack' ? 'table--stack' : 'table--grid'));

const primaryKey = computed(() => {
  if (props.primaryColumn) {
    return props.primaryColumn;
  }
  return props.columns?.[0]?.key ?? '';
});

const operationsKey = computed(() => {
  if (props.operationsColumn) {
    return props.operationsColumn;
  }
  const hasOperations = props.columns?.some((column) => column.key === 'operations');
  return hasOperations ? 'operations' : '';
});

const slots = useSlots();

const hasOperationsSlot = computed(() => {
  const key = operationsKey.value;
  if (!key) return false;
  return !!slots[key];
});

const detailColumns = computed(() =>
  (props.columns || []).filter(
    (column) => column.key !== primaryKey.value && column.key !== operationsKey.value
  )
);

const collapseDetails = computed(() => !!props.collapseDetails);
const expandedRows = ref<Record<string, boolean>>({});

watch(
  () => props.rows,
  () => {
    expandedRows.value = {};
  }
);

function getRowId(row: Record<string, any>, index: number) {
  const key = primaryKey.value;
  if (key && row && row[key] != null) {
    return String(row[key]);
  }
  return `row-${index}`;
}

function toggleRow(id: string) {
  expandedRows.value[id] = !expandedRows.value[id];
}

function isExpanded(id: string) {
  return !!expandedRows.value[id];
}

function formatValue(value: any) {
  if (value === undefined || value === null || value === '') {
    return '—';
  }
  if (typeof value === 'string' || typeof value === 'number') {
    return value;
  }
  if (Array.isArray(value)) {
    return value.join(', ');
  }
  if (typeof value === 'boolean') {
    return value ? '是' : '否';
  }
  return String(value);
}
</script>

<style scoped lang="scss">
.table-container {
  padding: 32rpx;
  display: flex;
  flex-direction: column;
  gap: 24rpx;
}

.table-header {
  display: flex;
  flex-direction: column;
  gap: 20rpx;

  @media (min-width: 768px) {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
  }
}

.table-title {
  font-size: 32rpx;
  font-weight: 600;
  color: var(--text-primary);
}

.table-subtitle {
  font-size: 24rpx;
  color: var(--text-muted);
  margin-top: 8rpx;
}

.table-actions {
  display: flex;
  align-items: center;
  gap: 16rpx;
}

.table-scroll {
  width: 100%;
}

.table-wrapper {
  min-width: 100%;
}

.table-row {
  display: grid;
  align-items: stretch;
  border-bottom: 1rpx solid rgba(148, 163, 184, 0.24);
  border-bottom: 1rpx solid var(--outline-color);
}

.table-row--head {
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
  font-size: 24rpx;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.table-cell {
  padding: 22rpx 18rpx;
  font-size: 26rpx;
  color: #f8fafc;
  color: var(--text-primary);
  white-space: normal;
  word-break: break-word;
  user-select: text;
  -webkit-user-select: text;
}

.table-row:not(.table-row--head):nth-child(even) {
  background: rgba(12, 19, 33, 0.55);
  background: var(--surface-light);
}

.table-empty {
  padding: 48rpx 24rpx;
  text-align: center;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
}

.table--stack {
  padding: 28rpx;
}

.stack-list {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
}

.stack-row {
  display: flex;
  flex-direction: column;
  gap: 18rpx;
  padding: 24rpx 22rpx;
  border-radius: 24rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: rgba(12, 19, 33, 0.55);
  background: var(--surface-light);
  backdrop-filter: blur(16rpx);

  @media (min-width: 768px) {
    padding: 28rpx 26rpx;
  }
}

.table--stack .stack-row {
  background: rgba(15, 23, 42, 0.72);
  background: var(--card-background);
}

.stack-header {
  display: flex;
  align-items: center;
  gap: 16rpx;
}

.stack-title {
  flex: 1;
  min-width: 0;
  font-size: 30rpx;
  font-weight: 600;
  color: #f8fafc;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.stack-toggle {
  padding: 10rpx 26rpx;
  border-radius: 999rpx;
  border: 1rpx solid rgba(148, 163, 184, 0.24);
  border: 1rpx solid var(--outline-color);
  background: transparent;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
  font-size: 24rpx;
}

.stack-operations {
  display: flex;
  flex-wrap: wrap;
  gap: 12rpx;
  border-top: 1rpx solid rgba(148, 163, 184, 0.24);
  border-top: 1rpx solid var(--outline-color);
  padding-top: 14rpx;
}

.stack-body {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240rpx, 1fr));
  gap: 16rpx;
}

.stack-field {
  display: flex;
  flex-direction: column;
  gap: 6rpx;
}

.stack-label {
  font-size: 22rpx;
  color: rgba(148, 163, 184, 0.75);
  color: var(--text-muted);
}

.stack-value {
  font-size: 26rpx;
  color: var(--text-primary);
  word-break: break-word;
  line-height: 1.5;
}
</style>