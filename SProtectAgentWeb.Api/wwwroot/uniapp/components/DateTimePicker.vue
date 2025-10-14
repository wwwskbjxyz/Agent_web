<template>
  <view class="picker-wrapper">
    <picker
      mode="multiSelector"
      :range="rangeColumns"
      :value="columnIndex"
      @change="onChange"
      @columnchange="onColumnChange"
    >
      <view class="picker-display" :class="{ placeholder: !displayValue }">
        {{ displayValue || placeholder }}
      </view>
    </picker>
    <button v-if="clearable && modelValue" class="clear-btn" @tap.stop="clearValue">清除</button>
  </view>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';

interface ColumnChangeDetail {
  column: number;
  value: number;
}

const props = withDefaults(
  defineProps<{
    modelValue?: string;
    placeholder?: string;
    clearable?: boolean;
    startYear?: number;
    endYear?: number;
  }>(),
  {
    modelValue: '',
    placeholder: '请选择时间',
    clearable: true,
    startYear: undefined,
    endYear: undefined
  }
);

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void;
  (e: 'change', value: string): void;
}>();

const now = new Date();
const columnIndex = ref<[number, number, number, number, number]>([0, 0, 0, 0, 0]);
const yearOptions = ref<number[]>([]);
const dayOptions = ref<number[]>([]);

const months = Array.from({ length: 12 }, (_, index) => index + 1);
const hours = Array.from({ length: 24 }, (_, index) => index);
const minutes = Array.from({ length: 60 }, (_, index) => index);

const rangeColumns = computed(() => [
  yearOptions.value.map((value) => `${value}年`),
  months.map((value) => `${value.toString().padStart(2, '0')}月`),
  dayOptions.value.map((value) => `${value.toString().padStart(2, '0')}日`),
  hours.map((value) => `${value.toString().padStart(2, '0')}时`),
  minutes.map((value) => `${value.toString().padStart(2, '0')}分`)
]);

const displayValue = computed(() => {
  const value = props.modelValue?.trim();
  if (!value) {
    return '';
  }
  const normalized = value.replace('T', ' ').replace(/\//g, '-');
  if (/^\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}$/.test(normalized)) {
    return normalized;
  }
  const parsed = new Date(normalized);
  if (Number.isNaN(parsed.getTime())) {
    return '';
  }
  return formatValue(parsed.getFullYear(), parsed.getMonth() + 1, parsed.getDate(), parsed.getHours(), parsed.getMinutes());
});

function formatValue(year: number, month: number, day: number, hour: number, minute: number) {
  const pad = (input: number) => input.toString().padStart(2, '0');
  return `${year}-${pad(month)}-${pad(day)} ${pad(hour)}:${pad(minute)}`;
}

function resolveYearRange(targetYear: number) {
  const current = now.getFullYear();
  const start = props.startYear ?? current - 5;
  const end = props.endYear ?? current + 5;
  const min = Math.min(start, targetYear);
  const max = Math.max(end, targetYear);
  const years: number[] = [];
  for (let year = min; year <= max; year += 1) {
    years.push(year);
  }
  yearOptions.value = years;
}

function updateDayOptions(year: number, month: number) {
  const daysInMonth = new Date(year, month, 0).getDate();
  dayOptions.value = Array.from({ length: daysInMonth }, (_, index) => index + 1);
}

function syncFromValue(value?: string) {
  const base = value?.trim();
  let target = new Date(base ? base.replace('T', ' ') : '');
  if (!base || Number.isNaN(target.getTime())) {
    target = now;
  }

  resolveYearRange(target.getFullYear());
  const yearIndex = Math.max(0, yearOptions.value.findIndex((item) => item === target.getFullYear()));
  updateDayOptions(yearOptions.value[yearIndex] ?? target.getFullYear(), target.getMonth() + 1);

  const nextIndex: [number, number, number, number, number] = [
    yearIndex,
    Math.max(0, target.getMonth()),
    Math.max(0, Math.min(dayOptions.value.length - 1, target.getDate() - 1)),
    Math.max(0, target.getHours()),
    Math.max(0, target.getMinutes())
  ];
  columnIndex.value = nextIndex;
}

function emitValue(indexes: number[]) {
  const [yearIdx, monthIdx, dayIdx, hourIdx, minuteIdx] = indexes;
  const year = yearOptions.value[yearIdx] ?? yearOptions.value[0];
  const month = months[monthIdx] ?? months[0];
  updateDayOptions(year, month);
  const day = dayOptions.value[dayIdx] ?? dayOptions.value[dayOptions.value.length - 1] ?? 1;
  const hour = hours[hourIdx] ?? 0;
  const minute = minutes[minuteIdx] ?? 0;
  const formatted = formatValue(year, month, day, hour, minute);
  emit('update:modelValue', formatted);
  emit('change', formatted);
}

function onChange(event: UniApp.MultiSelectorPickerChangeEvent) {
  const indexes = event.detail.value as number[];
  if (!Array.isArray(indexes) || indexes.length < 5) {
    return;
  }
  columnIndex.value = [indexes[0], indexes[1], indexes[2], indexes[3], indexes[4]];
  emitValue(indexes);
}

function onColumnChange(event: { detail: ColumnChangeDetail }) {
  const { column, value } = event.detail;
  const next = [...columnIndex.value] as number[];
  next[column] = value;
  if (column === 0 || column === 1) {
    const year = yearOptions.value[next[0]] ?? yearOptions.value[0];
    const month = months[next[1]] ?? months[0];
    updateDayOptions(year, month);
    if (next[2] >= dayOptions.value.length) {
      next[2] = Math.max(0, dayOptions.value.length - 1);
    }
  }
  columnIndex.value = [next[0], next[1], next[2], next[3], next[4]];
}

function clearValue() {
  emit('update:modelValue', '');
  emit('change', '');
}

watch(
  () => props.modelValue,
  (value) => {
    syncFromValue(value);
  },
  { immediate: true }
);
</script>

<style scoped lang="scss">
.picker-wrapper {
  position: relative;
  display: flex;
  align-items: center;
}

.picker-display {
  width: 100%;
  padding: 20rpx 24rpx;
  border-radius: 18rpx;
  background: rgba(15, 23, 42, 0.55);
  border: 1rpx solid rgba(59, 130, 246, 0.15);
  color: #e2e8f0;
  font-size: 26rpx;
}

.picker-display.placeholder {
  color: rgba(148, 163, 184, 0.65);
}

.clear-btn {
  position: absolute;
  right: 16rpx;
  padding: 8rpx 18rpx;
  border-radius: 999rpx;
  border: none;
  background: rgba(15, 23, 42, 0.68);
  color: rgba(148, 163, 184, 0.78);
  font-size: 22rpx;
}
</style>
