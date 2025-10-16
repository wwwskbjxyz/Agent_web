"use strict";
const common_vendor = require("../common/vendor.js");
const paddingX = 60;
const paddingY = 60;
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "SubTrendChart",
  props: {
    title: {},
    subtitle: { default: "" },
    categories: { default: () => [] },
    series: { default: () => [] },
    total: { default: void 0 }
  },
  setup(__props) {
    var _a, _b;
    const props = __props;
    const canvasId = `sub-trend-${Math.random().toString(36).slice(2)}`;
    const canvasWidth = common_vendor.ref(640);
    const canvasHeight = common_vendor.ref(360);
    const displayWidth = common_vendor.ref(640);
    const displayHeight = common_vendor.ref(360);
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
    const palette = ["#38bdf8", "#8b5cf6", "#f97316", "#22d3ee", "#f472b6", "#34d399", "#a855f7"];
    const instance = common_vendor.getCurrentInstance();
    const contextReady = common_vendor.ref(false);
    const canvasRect = common_vendor.ref(null);
    const categoryPoints = common_vendor.ref([]);
    const tooltip = common_vendor.reactive({
      visible: false,
      x: 0,
      y: 0,
      category: "",
      items: []
    });
    const tooltipStyle = common_vendor.computed(() => ({ left: `${tooltip.x}px`, top: `${tooltip.y}px` }));
    const displayedSeries = common_vendor.computed(() => props.series);
    const summaryText = common_vendor.computed(() => {
      if (props.total == null)
        return "";
      return `最近子代理7天合计：${props.total}`;
    });
    function draw() {
      if (!contextReady.value)
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
          var _a2;
          const values = ((_a2 = item.values) == null ? void 0 : _a2.length) ? item.values.map((value) => Number(value ?? 0)) : [0];
          return Math.max(...values);
        }),
        0
      );
      const upperBound = maxValue > 0 ? Math.ceil(maxValue * 1.1) : 1;
      const effectiveRange = upperBound || 1;
      const gridLines = 4;
      const adjust = (value) => ratio !== 1 ? value / ratio : value;
      ctx.setStrokeStyle("rgba(148, 163, 184, 0.18)");
      ctx.setLineWidth(adjust(1));
      for (let i = 0; i <= gridLines; i += 1) {
        const y = paddingY + (height - paddingY * 2) / gridLines * i;
        ctx.beginPath();
        ctx.moveTo(paddingX, y);
        ctx.lineTo(width - paddingX, y);
        ctx.stroke();
      }
      ctx.setStrokeStyle("rgba(148, 163, 184, 0.3)");
      ctx.setLineWidth(adjust(1.5));
      ctx.beginPath();
      ctx.moveTo(paddingX, paddingY);
      ctx.lineTo(paddingX, height - paddingY);
      ctx.lineTo(width - paddingX, height - paddingY);
      ctx.stroke();
      ctx.setFillStyle("rgba(226, 232, 240, 0.85)");
      ctx.setFontSize(adjust(16));
      for (let i = 0; i <= gridLines; i += 1) {
        const value = Math.round(upperBound / gridLines * (gridLines - i));
        const y = paddingY + (height - paddingY * 2) / gridLines * i;
        ctx.fillText(`${value}`, 18, y + 6);
      }
      const stepX = categories.length > 1 ? (width - paddingX * 2) / (categories.length - 1) : 0;
      const points = categories.map((label, index) => {
        const items = series.map((item, seriesIndex) => {
          var _a2;
          const raw = Number(((_a2 = item.values) == null ? void 0 : _a2[index]) ?? 0);
          const value = Number.isFinite(raw) ? raw : 0;
          const y = height - paddingY - value / effectiveRange * (height - paddingY * 2);
          return { name: item.name, value, color: palette[seriesIndex % palette.length], y };
        });
        const anchorY = items.length ? Math.min(...items.map((entry) => entry.y)) : height - paddingY;
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
        ctx.setLineJoin("round");
        ctx.setLineCap("round");
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
          if (!entry)
            return;
          ctx.beginPath();
          ctx.setFillStyle(color);
          ctx.arc(point.x, entry.y, adjust(3.5), 0, Math.PI * 2);
          ctx.fill();
        });
      });
      ctx.setFillStyle("rgba(226, 232, 240, 0.78)");
      ctx.setFontSize(adjust(18));
      points.forEach((point, index) => {
        if (index % 2 === 0 || index === points.length - 1) {
          const y = height - paddingY + 26;
          ctx.fillText(point.label, point.x - 24, y);
        }
      });
      ctx.draw();
      categoryPoints.value = points;
      common_vendor.nextTick$1(() => {
        refreshCanvasRect();
      });
    }
    common_vendor.onMounted(async () => {
      await common_vendor.nextTick$1();
      contextReady.value = true;
      draw();
      refreshCanvasRect();
    });
    common_vendor.watch(
      () => [props.categories, displayedSeries.value],
      () => {
        common_vendor.nextTick$1(() => {
          draw();
          refreshCanvasRect();
        });
      },
      { deep: true }
    );
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
              draw();
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
    function findNearestCategory(canvasX) {
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
      const tolerance = categoryPoints.value.length > 1 ? Math.max(Math.abs(categoryPoints.value[1].x - categoryPoints.value[0].x) / 2, 40) : 60;
      return minDistance <= tolerance ? nearest : null;
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
      const target = findNearestCategory(canvasX);
      if (!target) {
        tooltip.visible = false;
        return;
      }
      const displayWidthValue = (rect == null ? void 0 : rect.width) ?? width;
      const displayHeightValue = (rect == null ? void 0 : rect.height) ?? height;
      tooltip.visible = true;
      tooltip.category = target.label;
      tooltip.items = target.items.slice().sort((a, b) => b.value - a.value).map((item) => ({ name: item.name, value: `数量：${item.value}`, color: item.color }));
      tooltip.x = target.x / width * displayWidthValue;
      tooltip.y = target.anchorY / height * displayHeightValue;
    }
    function hideTooltip() {
      tooltip.visible = false;
    }
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
        q: common_vendor.t(tooltip.category),
        r: common_vendor.f(tooltip.items, (item, k0, i0) => {
          return {
            a: item.color,
            b: common_vendor.t(item.name),
            c: common_vendor.t(item.value),
            d: item.name
          };
        }),
        s: common_vendor.s(tooltipStyle.value)
      } : {}, {
        t: displayedSeries.value.length
      }, displayedSeries.value.length ? {
        v: common_vendor.f(displayedSeries.value, (item, index, i0) => {
          return {
            a: palette[index % palette.length],
            b: common_vendor.t(item.name),
            c: common_vendor.t(item.total),
            d: item.name
          };
        })
      } : {});
    };
  }
});
const Component = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-b3322790"]]);
wx.createComponent(Component);
//# sourceMappingURL=../../.sourcemap/mp-weixin/components/SubTrendChart.js.map
