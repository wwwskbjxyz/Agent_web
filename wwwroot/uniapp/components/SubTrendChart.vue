<template>
  <view class="chart-card glass-card">
    <view class="chart-header">
      <view>
        <text class="chart-title">{{ title }}</text>
        <text v-if="subtitle" class="chart-subtitle">{{ subtitle }}</text>
      </view>
      <text v-if="summaryText" class="chart-summary">{{ summaryText }}</text>
    </view>
    <view class="chart-body">
      <canvas
        type="2d"
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
        <text class="tooltip-title">{{ tooltip.category }}</text>
        <view class="tooltip-list">
          <view v-for="item in tooltip.items" :key="item.name" class="tooltip-row">
            <view class="tooltip-dot" :style="{ background: item.color }"></view>
            <text class="tooltip-name">{{ item.name }}</text>
            <text class="tooltip-value">{{ item.value }}</text>
          </view>
        </view>
      </view>
    </view>
    <view v-if="displayedSeries.length" class="legend">
      <view v-for="(item, index) in displayedSeries" :key="item.name" class="legend-item">
        <view class="legend-dot" :style="{ background: palette[index % palette.length] }"></view>
        <text class="legend-name">{{ item.name }}</text>
        <text class="legend-value">{{ item.total }}</text>
      </view>
    </view>
    <view v-else class="empty">暂无子代理激活记录</view>
  </view>
</template>

<script setup lang="ts">
import { computed, getCurrentInstance, nextTick, onMounted, reactive, ref, watch } from 'vue';

interface SeriesItem {
  name: string;
  values: number[];
  total: number;
}

const props = withDefaults(
  defineProps<{
    title: string;
    subtitle?: string;
    categories: string[];
    series: SeriesItem[];
    total?: number;
  }>(),
  {
    subtitle: '',
    categories: () => [],
    series: () => [],
    total: undefined
  }
);

const canvasId = `sub-trend-${Math.random().toString(36).slice(2)}`;
const canvasWidth = ref(640);
const canvasHeight = ref(360);
const displayWidth = ref(640);
const displayHeight = ref(360);
const pixelRatio = ref(1);
let lastScale = 1;
const paddingX = 60;
const paddingY = 60;
const palette = ['#38bdf8', '#8b5cf6', '#f97316', '#22d3ee', '#f472b6', '#34d399', '#a855f7'];

const instance = getCurrentInstance();
const contextReady = ref(false);
const canvasRect = ref<{ width: number; height: number; left: number; top: number } | null>(null);
const categoryPoints = ref<
  {
    x: number;
    label: string;
    anchorY: number;
    items: { name: string; value: number; color: string; y: number }[];
  }[]
>([]);
const tooltip = reactive({
  visible: false,
  x: 0,
  y: 0,
  category: '',
  items: [] as { name: string; value: number; color: string }[]
});
const tooltipStyle = computed(() => ({ left: `${tooltip.x}px`, top: `${tooltip.y}px` }));

const displayedSeries = computed(() => props.series);
const summaryText = computed(() => {
  if (props.total == null) return '';
  return `最近子代理7天合计：${props.total}`;
});

function draw() {
  if (!contextReady.value) return;
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
  const categories = Array.isArray(props.categories) ? props.categories : [];
  const series = displayedSeries.value;

  if (!categories.length || !series.length) {
    ctx.draw();
    categoryPoints.value = [];
    tooltip.visible = false;
    return;
  }

  const maxValue = Math.max(
    ...series.map((item) => {
      const values = item.values?.length ? item.values.map((value) => Number(value ?? 0)) : [0];
      return Math.max(...values);
    }),
    0
  );
  const upperBound = maxValue > 0 ? Math.ceil(maxValue * 1.1) : 1;
  const effectiveRange = upperBound || 1;
  const gridLines = 4;
  const adjust = (value: number) => (ratio !== 1 ? value / ratio : value);

  ctx.setStrokeStyle('rgba(148, 163, 184, 0.18)');
  ctx.setLineWidth(adjust(1));
  for (let i = 0; i <= gridLines; i += 1) {
    const y = paddingY + ((height - paddingY * 2) / gridLines) * i;
    ctx.beginPath();
    ctx.moveTo(paddingX, y);
    ctx.lineTo(width - paddingX, y);
    ctx.stroke();
  }

  ctx.setStrokeStyle('rgba(148, 163, 184, 0.3)');
  ctx.setLineWidth(adjust(1.5));
  ctx.beginPath();
  ctx.moveTo(paddingX, paddingY);
  ctx.lineTo(paddingX, height - paddingY);
  ctx.lineTo(width - paddingX, height - paddingY);
  ctx.stroke();

  ctx.setFillStyle('rgba(226, 232, 240, 0.85)');
  ctx.setFontSize(adjust(16));
  for (let i = 0; i <= gridLines; i += 1) {
    const value = Math.round((upperBound / gridLines) * (gridLines - i));
    const y = paddingY + ((height - paddingY * 2) / gridLines) * i;
    ctx.fillText(`${value}`, 18, y + 6);
  }

  const stepX =
    categories.length > 1 ? (width - paddingX * 2) / (categories.length - 1) : 0;
  const points = categories.map((label, index) => {
    const items = series.map((item, seriesIndex) => {
      const raw = Number(item.values?.[index] ?? 0);
      const value = Number.isFinite(raw) ? raw : 0;
      const y =
        height -
        paddingY -
        (value / effectiveRange) * (height - paddingY * 2);
      return { name: item.name, value, color: palette[seriesIndex % palette.length], y };
    });
    const anchorY = items.length
      ? Math.min(...items.map((entry) => entry.y))
      : height - paddingY;
    return {
      x: paddingX + stepX * index,
      label,
      anchorY,
      items
    };
  });

  series.forEach((item, seriesIndex) => {
    const color = palette[seriesIndex % palette.length];
    ctx.setStrokeStyle(color);
    ctx.setLineWidth(adjust(2.5));
    ctx.setLineJoin('round');
    ctx.setLineCap('round');
    ctx.beginPath();
    points.forEach((point, idx) => {
      const entry = point.items[seriesIndex];
      const y = entry ? entry.y : height - paddingY;
      if (idx === 0) {
        ctx.moveTo(point.x, y);
      } else {
        ctx.lineTo(point.x, y);
      }
    });
    ctx.stroke();

    points.forEach((point) => {
      const entry = point.items[seriesIndex];
      if (!entry) return;
      ctx.beginPath();
      ctx.setFillStyle(color);
      ctx.arc(point.x, entry.y, adjust(3.5), 0, Math.PI * 2);
      ctx.fill();
    });
  });

  ctx.setFillStyle('rgba(226, 232, 240, 0.78)');
  ctx.setFontSize(adjust(18));
  points.forEach((point, index) => {
    if (index % 2 === 0 || index === points.length - 1) {
      const y = height - paddingY + 26;
      ctx.fillText(point.label, point.x - 24, y);
    }
  });

  ctx.draw();
  categoryPoints.value = points;
  nextTick(() => {
    refreshCanvasRect();
  });
}

onMounted(async () => {
  await nextTick();
  contextReady.value = true;
  draw();
  refreshCanvasRect();
});

watch(
  () => [props.categories, displayedSeries.value],
  () => {
    nextTick(() => {
      draw();
      refreshCanvasRect();
    });
  },
  { deep: true }
);

function refreshCanvasRect() {
  uni.createSelectorQuery()
    .in(instance?.proxy)
    .select(`#${canvasId}`)
    .boundingClientRect((rect) => {
      if (rect) {
        const info = uni.getSystemInfoSync?.();
        const ratio = info && typeof info.pixelRatio === 'number' ? Math.max(info.pixelRatio, 1) : 1;
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
            draw();
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

function findNearestCategory(canvasX: number) {
  if (!categoryPoints.value.length) {
    return null;
  }
  let nearest = categoryPoints.value[0];
  let minDistance = Math.abs(nearest.x - canvasX);
  categoryPoints.value.forEach((point) => {
    const distance = Math.abs(point.x - canvasX);
    if (distance < minDistance) {
      nearest = point;
      minDistance = distance;
    }
  });
  const tolerance = categoryPoints.value.length > 1
    ? Math.max(Math.abs(categoryPoints.value[1].x - categoryPoints.value[0].x) / 2, 40)
    : 60;
  return minDistance <= tolerance ? nearest : null;
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
  const target = findNearestCategory(canvasX);
  if (!target) {
    tooltip.visible = false;
    return;
  }
  const displayWidthValue = rect?.width ?? width;
  const displayHeightValue = rect?.height ?? height;
  tooltip.visible = true;
  tooltip.category = target.label;
  tooltip.items = target.items
    .slice()
    .sort((a, b) => b.value - a.value)
    .map((item) => ({ name: item.name, value: `数量：${item.value}`, color: item.color }));
  tooltip.x = (target.x / width) * displayWidthValue;
  tooltip.y = (target.anchorY / height) * displayHeightValue;
}

function hideTooltip() {
  tooltip.visible = false;
}
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

.chart-summary {
  font-size: 26rpx;
  color: rgba(148, 163, 184, 0.85);
}

.chart-body {
  position: relative;
}

.chart-canvas {
  width: 100%;
  height: 360rpx;
  background: radial-gradient(circle at top, rgba(56, 189, 248, 0.12), transparent 60%), rgba(15, 23, 42, 0.65);
  border-radius: 28rpx;
}

.chart-tooltip {
  position: absolute;
  min-width: 200rpx;
  padding: 18rpx 22rpx;
  border-radius: 20rpx;
  background: rgba(15, 23, 42, 0.94);
  border: 1rpx solid rgba(56, 189, 248, 0.35);
  transform: translate(-50%, -120%);
  display: flex;
  flex-direction: column;
  gap: 10rpx;
  pointer-events: none;
  z-index: 3;
}

.tooltip-title {
  font-size: 26rpx;
  color: rgba(226, 232, 240, 0.95);
  font-weight: 600;
}

.tooltip-list {
  display: flex;
  flex-direction: column;
  gap: 8rpx;
}

.tooltip-row {
  display: flex;
  align-items: center;
  gap: 10rpx;
  font-size: 24rpx;
  color: rgba(226, 232, 240, 0.88);
}

.tooltip-dot {
  width: 14rpx;
  height: 14rpx;
  border-radius: 50%;
}

.tooltip-name {
  flex: 1;
}

.tooltip-value {
  font-weight: 600;
  color: #38bdf8;
}

.legend {
  display: flex;
  flex-wrap: wrap;
  gap: 16rpx 24rpx;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 10rpx;
  color: rgba(226, 232, 240, 0.85);
  font-size: 24rpx;
}

.legend-dot {
  width: 16rpx;
  height: 16rpx;
  border-radius: 50%;
}

.legend-name {
  font-weight: 500;
}

.legend-value {
  color: rgba(148, 163, 184, 0.8);
}

.empty {
  text-align: center;
  color: rgba(148, 163, 184, 0.6);
  padding: 20rpx 0;
}
</style>