"use strict";
const common_vendor = require("../common/vendor.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "TrendChart",
  props: {
    title: {},
    subtitle: { default: "" },
    points: {},
    total: { default: void 0 },
    totalLabel: { default: void 0 },
    maxValue: { default: void 0 }
  },
  setup(__props) {
    var _a, _b;
    const props = __props;
    const canvasId = `trend-${Math.random().toString(36).slice(2)}`;
    const canvasWidth = common_vendor.ref(640);
    const canvasHeight = common_vendor.ref(320);
    const displayWidth = common_vendor.ref(640);
    const displayHeight = common_vendor.ref(320);
    const pixelRatio = common_vendor.ref(1);
    let preferHighDpiScaling = true;
    preferHighDpiScaling = false;
    if (typeof common_vendor.wx$1 !== "undefined") {
      preferHighDpiScaling = false;
    }
    const systemInfo = (_b = (_a = common_vendor.index).getSystemInfoSync) == null ? void 0 : _b.call(_a);
    if (systemInfo) {
      const runtime = `${systemInfo.uniPlatform || ""}`.toLowerCase();
      const appName = `${systemInfo.appName || ""}`.toLowerCase();
      if (runtime.includes("mp") || runtime.includes("wechat") || appName.includes("wechat")) {
        preferHighDpiScaling = false;
      }
    }
    let lastScale = 1;
    const contextReady = common_vendor.ref(false);
    const instance = common_vendor.getCurrentInstance();
    const canvasRect = common_vendor.ref(null);
    const pointPositions = common_vendor.ref([]);
    const tooltip = common_vendor.reactive({ visible: false, x: 0, y: 0, label: "", value: 0 });
    const tooltipStyle = common_vendor.computed(() => ({ left: `${tooltip.x}px`, top: `${tooltip.y}px` }));
    const summaryText = common_vendor.computed(() => {
      if (props.total == null) {
        return "";
      }
      const label = props.totalLabel || "累计总计";
      return `${label}：${props.total}`;
    });
    function draw(points) {
      if (!points.length)
        return;
      const ctx = common_vendor.index.createCanvasContext(canvasId, instance == null ? void 0 : instance.proxy);
      const ratio = pixelRatio.value || 1;
      if (typeof ctx.scale === "function" && Math.abs(lastScale - 1) > 1e-3) {
        ctx.scale(1 / lastScale, 1 / lastScale);
        lastScale = 1;
      }
      ctx.clearRect(0, 0, canvasWidth.value, canvasHeight.value);
      if (ratio !== 1 && typeof ctx.scale === "function") {
        ctx.scale(ratio, ratio);
        lastScale = ratio;
      } else {
        lastScale = 1;
      }
      const width = displayWidth.value || canvasWidth.value / ratio;
      const height = displayHeight.value || canvasHeight.value / ratio;
      const paddingX = 60;
      const paddingY = 50;
      const adjust = (value) => ratio !== 1 ? value / ratio : value;
      const values = points.map((item) => Number(item.value ?? 0));
      const min = Math.min(...values);
      const max = Math.max(...values);
      const providedMax = typeof props.maxValue === "number" && props.maxValue > 0 ? props.maxValue : max;
      const upperBound = Math.max(providedMax, max);
      const baseMin = Math.min(min, 0);
      const range = upperBound - baseMin;
      const effectiveRange = range === 0 ? 1 : range;
      const denominator = Math.max(points.length - 1, 1);
      const coordinates = points.map((point, index) => {
        const x = paddingX + index / denominator * (width - paddingX * 2);
        const y = height - paddingY - (point.value - baseMin) / effectiveRange * (height - paddingY * 2);
        const value = Number(point.value ?? 0);
        return { x, y, value, label: point.date };
      });
      const gridLines = 4;
      ctx.setStrokeStyle("rgba(148, 163, 184, 0.18)");
      ctx.setLineWidth(adjust(1));
      for (let i = 0; i <= gridLines; i += 1) {
        const y = paddingY + (height - paddingY * 2) / gridLines * i;
        ctx.beginPath();
        ctx.moveTo(paddingX, y);
        ctx.lineTo(width - paddingX, y);
        ctx.stroke();
      }
      ctx.setStrokeStyle("rgba(148, 163, 184, 0.28)");
      ctx.setLineWidth(adjust(1.5));
      ctx.beginPath();
      ctx.moveTo(paddingX, paddingY);
      ctx.lineTo(paddingX, height - paddingY);
      ctx.lineTo(width - paddingX, height - paddingY);
      ctx.stroke();
      ctx.setFillStyle("rgba(226, 232, 240, 0.8)");
      ctx.setFontSize(adjust(16));
      for (let i = 0; i <= gridLines; i += 1) {
        const value = Math.round(upperBound / gridLines * (gridLines - i));
        const y = paddingY + (height - paddingY * 2) / gridLines * i;
        ctx.fillText(`${value}`, 16, y + 6);
      }
      ctx.beginPath();
      const gradient = ctx.createLinearGradient(0, 0, width, 0);
      gradient.addColorStop(0, "#38bdf8");
      gradient.addColorStop(1, "#6366f1");
      ctx.setLineWidth(adjust(3));
      ctx.setStrokeStyle(gradient);
      ctx.setLineJoin("round");
      ctx.setLineCap("round");
      coordinates.forEach((coord, index) => {
        if (index === 0) {
          ctx.moveTo(coord.x, coord.y);
        } else {
          ctx.lineTo(coord.x, coord.y);
        }
      });
      ctx.stroke();
      const fillGradient = ctx.createLinearGradient(0, paddingY, 0, height - paddingY);
      fillGradient.addColorStop(0, "rgba(56, 189, 248, 0.32)");
      fillGradient.addColorStop(1, "rgba(15, 23, 42, 0.05)");
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
      ctx.setFillStyle("rgba(226, 232, 240, 0.65)");
      ctx.setFontSize(adjust(18));
      coordinates.forEach((coord, index) => {
        if (index % 2 === 0 || index === points.length - 1) {
          const y = height - paddingY + 24;
          ctx.fillText(points[index].date, coord.x - 24, y);
        }
      });
      coordinates.forEach((coord) => {
        ctx.beginPath();
        ctx.setFillStyle("#38bdf8");
        ctx.arc(coord.x, coord.y, adjust(4), 0, Math.PI * 2);
        ctx.fill();
      });
      ctx.draw();
      pointPositions.value = coordinates;
      common_vendor.nextTick$1(() => {
        refreshCanvasRect();
      });
    }
    function render() {
      if (!contextReady.value)
        return;
      draw(props.points);
    }
    function refreshCanvasRect() {
      common_vendor.index.createSelectorQuery().in(instance == null ? void 0 : instance.proxy).select(`#${canvasId}`).boundingClientRect((rect) => {
        var _a2, _b2;
        if (rect) {
          const info = (_b2 = (_a2 = common_vendor.index).getSystemInfoSync) == null ? void 0 : _b2.call(_a2);
          let ratio = 1;
          if (info && typeof info.pixelRatio === "number") {
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
            common_vendor.nextTick$1(() => {
              render();
            });
          }
        }
      }).exec();
    }
    function resolvePointerPosition(event) {
      if ((event == null ? void 0 : event.detail) && typeof event.detail.x === "number") {
        return { x: event.detail.x, y: event.detail.y };
      }
      if ((event == null ? void 0 : event.changedTouches) && event.changedTouches.length) {
        const touch = event.changedTouches[0];
        if (typeof touch.x === "number" && typeof touch.y === "number") {
          return { x: touch.x, y: touch.y };
        }
        if (canvasRect.value && typeof touch.pageX === "number" && typeof touch.pageY === "number") {
          return {
            x: touch.pageX - canvasRect.value.left,
            y: touch.pageY - canvasRect.value.top
          };
        }
      }
      if (typeof (event == null ? void 0 : event.offsetX) === "number" && typeof (event == null ? void 0 : event.offsetY) === "number") {
        return { x: event.offsetX, y: event.offsetY };
      }
      return null;
    }
    function findNearestPoint(canvasX) {
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
    function handlePointer(event) {
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
      const displayWidthValue = (rect == null ? void 0 : rect.width) ?? width;
      const displayHeightValue = (rect == null ? void 0 : rect.height) ?? height;
      tooltip.visible = true;
      tooltip.label = target.label;
      tooltip.value = `数量：${target.value}`;
      tooltip.x = target.x / width * displayWidthValue;
      tooltip.y = target.y / height * displayHeightValue;
    }
    function hideTooltip() {
      tooltip.visible = false;
    }
    common_vendor.onMounted(async () => {
      await common_vendor.nextTick$1();
      contextReady.value = true;
      render();
      refreshCanvasRect();
    });
    common_vendor.watch(
      () => props.points,
      (value) => {
        if (!value.length)
          return;
        common_vendor.nextTick$1(() => {
          render();
          refreshCanvasRect();
        });
      },
      { deep: true }
    );
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: common_vendor.t(_ctx.title),
        b: _ctx.subtitle
      }, _ctx.subtitle ? {
        c: common_vendor.t(_ctx.subtitle)
      } : {}, {
        d: summaryText.value
      }, summaryText.value ? {
        e: common_vendor.t(summaryText.value)
      } : {}, {
        f: canvasId,
        g: canvasId,
        h: canvasWidth.value,
        i: canvasHeight.value,
        j: common_vendor.o(handlePointer),
        k: common_vendor.o(handlePointer),
        l: common_vendor.o(hideTooltip),
        m: common_vendor.o(hideTooltip),
        n: common_vendor.o(handlePointer),
        o: common_vendor.o(hideTooltip),
        p: tooltip.visible
      }, tooltip.visible ? {
        q: common_vendor.t(tooltip.label),
        r: common_vendor.t(tooltip.value),
        s: common_vendor.s(tooltipStyle.value)
      } : {});
    };
  }
});
const Component = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-8e3f355a"]]);
wx.createComponent(Component);
//# sourceMappingURL=../../.sourcemap/mp-weixin/components/TrendChart.js.map
