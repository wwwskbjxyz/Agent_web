
<template>
  <view class="chart-card glass-card">
    <view class="chart-header">
      <view>
        <text class="chart-title">{{ title }}</text>
        <text class="chart-subtitle" v-if="subtitle">{{ subtitle }}</text>
      </view>
      <view class="chart-extra">
        <slot name="extra" />
        <text v-if="summaryText" class="chart-summary">{{ summaryText }}</text>
      </view>
    </view>
    <view class="chart-body">
      <canvas
        :canvas-id="canvasId"
        :id="canvasId"
        class="chart-canvas"
        :width="canvasWidth"
        :height="canvasHeight"
        @touchstart.stop="handlePointer"
        @touchmove.stop="handlePointer"
        @touchend="hideTooltip"
        @touchcancel="hideTooltip"
        @mousemove="handlePointer"
        @mouseleave="hideTooltip"
      ></canvas>
      <view v-if="tooltip.visible" class="chart-tooltip" :style="tooltipStyle">
        <text class="tooltip-title">{{ tooltip.label }}</text>
        <text class="tooltip-value">{{ tooltip.value }}</text>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, getCurrentInstance, nextTick, onMounted, reactive, ref, watch } from 'vue';
import type { TrendPoint } from '../common/types';

const props = withDefaults(
  defineProps<{
    title: string;
    subtitle?: string;
    points: TrendPoint[];
    total?: number;
    totalLabel?: string;
    maxValue?: number;
  }>(),
  {
    subtitle: '',
    total: undefined,
    totalLabel: undefined,
    maxValue: undefined
  }
);

const canvasId = `trend-${Math.random().toString(36).slice(2)}`;
const canvasWidth = ref(640);
const canvasHeight = ref(320);
const displayWidth = ref(640);
const displayHeight = ref(320);
const pixelRatio = ref(1);

let preferHighDpiScaling = true;
// #ifdef H5
preferHighDpiScaling = false;
// #endif
// #ifdef MP
preferHighDpiScaling = false;
// #endif
if (typeof wx !== 'undefined') {
  preferHighDpiScaling = false;
}
const systemInfo = uni.getSystemInfoSync?.();
if (systemInfo) {
  const runtime = `${systemInfo.uniPlatform || ''}`.toLowerCase();
  const appName = `${systemInfo.appName || ''}`.toLowerCase();
  if (runtime.includes('mp') || runtime.includes('wechat') || appName.includes('wechat')) {
    preferHighDpiScaling = false;
  }
}
let lastScale = 1;
const contextReady = ref(false);
const instance = getCurrentInstance();
const canvasRect = ref<{ width: number; height: number; left: number; top: number } | null>(null);
const pointPositions = ref<{ x: number; y: number; value: number; label: string }[]>([]);
const tooltip = reactive({ visible: false, x: 0, y: 0, label: '', value: 0 });
const tooltipStyle = computed(() => ({ left: `${tooltip.x}px`, top: `${tooltip.y}px` }));

function formatAxisLabel(date: string): string {
  if (!date) {
    return '';
  }

  const trimmed = date.trim();
  if (!trimmed) {
    return '';
  }

  const withoutTime = trimmed.split('T')[0].split(' ')[0];
  const match = withoutTime.match(/^(\d{4})[-/](\d{1,2})[-/](\d{1,2})$/);
  if (match) {
    const [, , month, day] = match;
    return `${month.padStart(2, '0')}-${day.padStart(2, '0')}`;
  }

  const segments = withoutTime.split(/[-/]/).filter(Boolean);
  if (segments.length >= 2) {
    const month = segments[segments.length - 2]?.padStart(2, '0');
    const day = segments[segments.length - 1]?.padStart(2, '0');
    if (month && day) {
      return `${month}-${day}`;
    }
  }

  return trimmed;
}

const summaryText = computed(() => {
  if (props.total == null) {
    return '';
  }
  const label = props.totalLabel || '累计总计';
  return `${label}：${props.total}`;
});

function draw(points: TrendPoint[]) {
  if (!points.length) return;
  const ctx = uni.createCanvasContext(canvasId, instance?.proxy);
  const ratio = pixelRatio.value || 1;
  if (typeof ctx.scale === 'function' && Math.abs(lastScale - 1) > 0.001) {
    ctx.scale(1 / lastScale, 1 / lastScale);
    lastScale = 1;
  }
  ctx.clearRect(0, 0, canvasWidth.value, canvasHeight.value);
  if (ratio !== 1 && typeof ctx.scale === 'function') {
    ctx.scale(ratio, ratio);
    lastScale = ratio;
  } else {
    lastScale = 1;
  }
  const width = displayWidth.value || canvasWidth.value / ratio;
  const height = displayHeight.value || canvasHeight.value / ratio;
  const paddingX = 60;
  const paddingY = 50;
  const adjust = (value: number) => (ratio !== 1 ? value / ratio : value);

  const values = points.map((item) => Number(item.value ?? 0));
  const min = Math.min(...values);
  const max = Math.max(...values);
  const providedMax = typeof props.maxValue === 'number' && props.maxValue > 0 ? props.maxValue : max;
  const upperBound = Math.max(providedMax, max);
  const baseMin = Math.min(min, 0);
  const range = upperBound - baseMin;
  const effectiveRange = range === 0 ? 1 : range;
  const denominator = Math.max(points.length - 1, 1);
  const coordinates = points.map((point, index) => {
    const x = paddingX + (index / denominator) * (width - paddingX * 2);
    const y = height - paddingY - ((point.value - baseMin) / effectiveRange) * (height - paddingY * 2);
    const value = Number(point.value ?? 0);
    return { x, y, value, label: point.date };
  });

  // background grid
  const gridLines = 4;
  ctx.setStrokeStyle('rgba(148, 163, 184, 0.18)');
  ctx.setLineWidth(adjust(1));
  for (let i = 0; i <= gridLines; i += 1) {
    const y = paddingY + ((height - paddingY * 2) / gridLines) * i;
    ctx.beginPath();
    ctx.moveTo(paddingX, y);
    ctx.lineTo(width - paddingX, y);
    ctx.stroke();
  }

  // axes
  ctx.setStrokeStyle('rgba(148, 163, 184, 0.28)');
  ctx.setLineWidth(adjust(1.5));
  ctx.beginPath();
  ctx.moveTo(paddingX, paddingY);
  ctx.lineTo(paddingX, height - paddingY);
  ctx.lineTo(width - paddingX, height - paddingY);
  ctx.stroke();

  // y-axis labels
  ctx.setFillStyle('rgba(226, 232, 240, 0.8)');
  ctx.setFontSize(adjust(16));
  for (let i = 0; i <= gridLines; i += 1) {
    const value = Math.round((upperBound / gridLines) * (gridLines - i));
    const y = paddingY + ((height - paddingY * 2) / gridLines) * i;
    ctx.fillText(`${value}`, 16, y + 6);
  }

  ctx.beginPath();
  const gradient = ctx.createLinearGradient(0, 0, width, 0);
  gradient.addColorStop(0, '#38bdf8');
  gradient.addColorStop(1, '#6366f1');

  ctx.setLineWidth(adjust(3));
  ctx.setStrokeStyle(gradient);
  ctx.setLineJoin('round');
  ctx.setLineCap('round');

  coordinates.forEach((coord, index) => {
    if (index === 0) {
      ctx.moveTo(coord.x, coord.y);
    } else {
      ctx.lineTo(coord.x, coord.y);
    }
  });
  ctx.stroke();

  const fillGradient = ctx.createLinearGradient(0, paddingY, 0, height - paddingY);
  fillGradient.addColorStop(0, 'rgba(56, 189, 248, 0.32)');
  fillGradient.addColorStop(1, 'rgba(15, 23, 42, 0.05)');
  ctx.setFillStyle(fillGradient);
  ctx.beginPath();
  coordinates.forEach((coord, index) => {
    if (index === 0) {
      ctx.moveTo(coord.x, coord.y);
    } else {
      ctx.lineTo(coord.x, coord.y);
    }
  });
  ctx.lineTo(width - paddingX, height - paddingY);
  ctx.lineTo(paddingX, height - paddingY);
  ctx.closePath();
  ctx.fill();

  ctx.setFillStyle('rgba(226, 232, 240, 0.65)');
  ctx.setFontSize(adjust(18));
  const previousTextAlign = (ctx as any).textAlign;
  if (typeof ctx.setTextAlign === 'function') {
    ctx.setTextAlign('center');
  }
  coordinates.forEach((coord, index) => {
    if (index % 2 === 0 || index === points.length - 1) {
      const y = height - paddingY + 24;
      const label = formatAxisLabel(points[index].date);
      ctx.fillText(label, coord.x, y);
    }
  });
  if (typeof ctx.setTextAlign === 'function') {
    const fallback = typeof previousTextAlign === 'string' ? previousTextAlign : 'left';
    ctx.setTextAlign(fallback);
  }

  coordinates.forEach((coord) => {
    ctx.beginPath();
    ctx.setFillStyle('#38bdf8');
    ctx.arc(coord.x, coord.y, adjust(4), 0, Math.PI * 2);
    ctx.fill();
  });

  ctx.draw();
  pointPositions.value = coordinates;
  nextTick(() => {
    refreshCanvasRect();
  });
}

function render() {
  if (!contextReady.value) return;
  draw(props.points);
}

function refreshCanvasRect() {
  uni.createSelectorQuery()
    .in(instance?.proxy)
    .select(`#${canvasId}`)
    .boundingClientRect((rect) => {
      if (rect) {
        const info = uni.getSystemInfoSync?.();
        let ratio = 1;
        if (info && typeof info.pixelRatio === 'number') {
          ratio = preferHighDpiScaling ? Math.max(info.pixelRatio, 1) : 1;
        }
        const width = Math.max(rect.width, 1);
        const height = Math.max(rect.height, 1);
        const scaledWidth = width * ratio;
        const scaledHeight = height * ratio;

        canvasRect.value = {
          width,
          height,
          left: rect.left,
          top: rect.top
        };

        let sizeChanged = false;
        if (Math.abs(canvasWidth.value - scaledWidth) > 1) {
          canvasWidth.value = scaledWidth;
          sizeChanged = true;
        }
        if (Math.abs(canvasHeight.value - scaledHeight) > 1) {
          canvasHeight.value = scaledHeight;
          sizeChanged = true;
        }
        if (Math.abs(displayWidth.value - width) > 0.5 || Math.abs(displayHeight.value - height) > 0.5) {
          displayWidth.value = width;
          displayHeight.value = height;
          sizeChanged = true;
        }
        if (Math.abs(pixelRatio.value - ratio) > 0.01) {
          pixelRatio.value = ratio;
          sizeChanged = true;
        }

        if (sizeChanged) {
          nextTick(() => {
            render();
          });
        }
      }
    })
    .exec();
}

function resolvePointerPosition(event: any): { x: number; y: number } | null {
  if (event?.detail && typeof event.detail.x === 'number') {
    return { x: event.detail.x, y: event.detail.y };
  }
  if (event?.changedTouches && event.changedTouches.length) {
    const touch = event.changedTouches[0];
    if (typeof touch.x === 'number' && typeof touch.y === 'number') {
      return { x: touch.x, y: touch.y };
    }
    if (canvasRect.value && typeof touch.pageX === 'number' && typeof touch.pageY === 'number') {
      return {
        x: touch.pageX - canvasRect.value.left,
        y: touch.pageY - canvasRect.value.top
      };
    }
  }
  if (typeof event?.offsetX === 'number' && typeof event?.offsetY === 'number') {
    return { x: event.offsetX, y: event.offsetY };
  }
  return null;
}

function findNearestPoint(canvasX: number) {
  if (!pointPositions.value.length) {
    return null;
  }
  let nearest = pointPositions.value[0];
  let minDistance = Math.abs(nearest.x - canvasX);
  pointPositions.value.forEach((point) => {
    const distance = Math.abs(point.x - canvasX);
    if (distance < minDistance) {
      nearest = point;
      minDistance = distance;
    }
  });
  return minDistance <= 40 ? nearest : null;
}

function handlePointer(event: any) {
  const pointer = resolvePointerPosition(event);
  if (!pointer) {
    return;
  }
  let rect = canvasRect.value;
  if (!rect) {
    refreshCanvasRect();
    rect = canvasRect.value;
  }
  const ratio = pixelRatio.value || 1;
  const width = displayWidth.value || canvasWidth.value / ratio;
  const height = displayHeight.value || canvasHeight.value / ratio;
  const scaleX = rect && rect.width ? width / rect.width : 1;
  const canvasX = pointer.x * scaleX;
  const target = findNearestPoint(canvasX);
  if (!target) {
    tooltip.visible = false;
    return;
  }
  const displayWidthValue = rect?.width ?? width;
  const displayHeightValue = rect?.height ?? height;
  tooltip.visible = true;
  tooltip.label = target.label;
  tooltip.value = `数量：${target.value}`;
  tooltip.x = (target.x / width) * displayWidthValue;
  tooltip.y = (target.y / height) * displayHeightValue;
}

function hideTooltip() {
  tooltip.visible = false;
}

onMounted(async () => {
  await nextTick();
  contextReady.value = true;
  render();
  refreshCanvasRect();
});

watch(
  () => props.points,
  (value) => {
    if (!value.length) return;
    nextTick(() => {
      render();
      refreshCanvasRect();
    });
  },
  { deep: true }
);
</script>

<style scoped lang="scss">
.chart-card {
  padding: 32rpx;
  display: flex;
  flex-direction: column;
  gap: 24rpx;
}

.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.chart-title {
  font-size: 32rpx;
  font-weight: 600;
  color: #f8fafc;
}

.chart-subtitle {
  font-size: 24rpx;
  color: rgba(148, 163, 184, 0.78);
  margin-top: 6rpx;
}

.chart-extra {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
  align-items: flex-end;
}

.chart-summary {
  font-size: 26rpx;
  color: rgba(148, 163, 184, 0.85);
}

.chart-body {
  position: relative;
}

.chart-canvas {
  width: 100%;
  height: 320rpx;
  background: radial-gradient(circle at top, rgba(56, 189, 248, 0.15), transparent 60%),
    rgba(15, 23, 42, 0.65);
  border-radius: 28rpx;
}

.chart-tooltip {
  position: absolute;
  min-width: 160rpx;
  padding: 16rpx 20rpx;
  border-radius: 18rpx;
  background: rgba(15, 23, 42, 0.92);
  border: 1rpx solid rgba(56, 189, 248, 0.35);
  transform: translate(-50%, -120%);
  display: flex;
  flex-direction: column;
  gap: 6rpx;
  pointer-events: none;
  z-index: 3;
}

.tooltip-title {
  font-size: 24rpx;
  color: rgba(226, 232, 240, 0.92);
}

.tooltip-value {
  font-size: 28rpx;
  font-weight: 600;
  color: #38bdf8;
}
</style>