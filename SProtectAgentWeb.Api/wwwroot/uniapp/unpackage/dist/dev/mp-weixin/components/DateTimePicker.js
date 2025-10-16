"use strict";
const common_vendor = require("../common/vendor.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "DateTimePicker",
  props: {
    modelValue: { default: "" },
    placeholder: { default: "请选择时间" },
    clearable: { type: Boolean, default: true },
    startYear: { default: void 0 },
    endYear: { default: void 0 }
  },
  emits: ["update:modelValue", "change"],
  setup(__props, { emit: __emit }) {
    const props = __props;
    const emit = __emit;
    const now = /* @__PURE__ */ new Date();
    const columnIndex = common_vendor.ref([0, 0, 0, 0, 0]);
    const yearOptions = common_vendor.ref([]);
    const dayOptions = common_vendor.ref([]);
    const months = Array.from({ length: 12 }, (_, index) => index + 1);
    const hours = Array.from({ length: 24 }, (_, index) => index);
    const minutes = Array.from({ length: 60 }, (_, index) => index);
    const rangeColumns = common_vendor.computed(() => [
      yearOptions.value.map((value) => `${value}年`),
      months.map((value) => `${value.toString().padStart(2, "0")}月`),
      dayOptions.value.map((value) => `${value.toString().padStart(2, "0")}日`),
      hours.map((value) => `${value.toString().padStart(2, "0")}时`),
      minutes.map((value) => `${value.toString().padStart(2, "0")}分`)
    ]);
    const displayValue = common_vendor.computed(() => {
      var _a;
      const value = (_a = props.modelValue) == null ? void 0 : _a.trim();
      if (!value) {
        return "";
      }
      const normalized = value.replace("T", " ").replace(/\//g, "-");
      if (/^\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}$/.test(normalized)) {
        return normalized;
      }
      const parsed = new Date(normalized);
      if (Number.isNaN(parsed.getTime())) {
        return "";
      }
      return formatValue(parsed.getFullYear(), parsed.getMonth() + 1, parsed.getDate(), parsed.getHours(), parsed.getMinutes());
    });
    function formatValue(year, month, day, hour, minute) {
      const pad = (input) => input.toString().padStart(2, "0");
      return `${year}-${pad(month)}-${pad(day)} ${pad(hour)}:${pad(minute)}`;
    }
    function resolveYearRange(targetYear) {
      const current = now.getFullYear();
      const start = props.startYear ?? current - 5;
      const end = props.endYear ?? current + 5;
      const min = Math.min(start, targetYear);
      const max = Math.max(end, targetYear);
      const years = [];
      for (let year = min; year <= max; year += 1) {
        years.push(year);
      }
      yearOptions.value = years;
    }
    function updateDayOptions(year, month) {
      const daysInMonth = new Date(year, month, 0).getDate();
      dayOptions.value = Array.from({ length: daysInMonth }, (_, index) => index + 1);
    }
    function syncFromValue(value) {
      const base = value == null ? void 0 : value.trim();
      let target = new Date(base ? base.replace("T", " ") : "");
      if (!base || Number.isNaN(target.getTime())) {
        target = now;
      }
      resolveYearRange(target.getFullYear());
      const yearIndex = Math.max(0, yearOptions.value.findIndex((item) => item === target.getFullYear()));
      updateDayOptions(yearOptions.value[yearIndex] ?? target.getFullYear(), target.getMonth() + 1);
      const nextIndex = [
        yearIndex,
        Math.max(0, target.getMonth()),
        Math.max(0, Math.min(dayOptions.value.length - 1, target.getDate() - 1)),
        Math.max(0, target.getHours()),
        Math.max(0, target.getMinutes())
      ];
      columnIndex.value = nextIndex;
    }
    function emitValue(indexes) {
      const [yearIdx, monthIdx, dayIdx, hourIdx, minuteIdx] = indexes;
      const year = yearOptions.value[yearIdx] ?? yearOptions.value[0];
      const month = months[monthIdx] ?? months[0];
      updateDayOptions(year, month);
      const day = dayOptions.value[dayIdx] ?? dayOptions.value[dayOptions.value.length - 1] ?? 1;
      const hour = hours[hourIdx] ?? 0;
      const minute = minutes[minuteIdx] ?? 0;
      const formatted = formatValue(year, month, day, hour, minute);
      emit("update:modelValue", formatted);
      emit("change", formatted);
    }
    function onChange(event) {
      const indexes = event.detail.value;
      if (!Array.isArray(indexes) || indexes.length < 5) {
        return;
      }
      columnIndex.value = [indexes[0], indexes[1], indexes[2], indexes[3], indexes[4]];
      emitValue(indexes);
    }
    function onColumnChange(event) {
      const { column, value } = event.detail;
      const next = [...columnIndex.value];
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
      emit("update:modelValue", "");
      emit("change", "");
    }
    common_vendor.watch(
      () => props.modelValue,
      (value) => {
        syncFromValue(value);
      },
      { immediate: true }
    );
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: common_vendor.t(displayValue.value || _ctx.placeholder),
        b: !displayValue.value ? 1 : "",
        c: rangeColumns.value,
        d: columnIndex.value,
        e: common_vendor.o(onChange),
        f: common_vendor.o(onColumnChange),
        g: _ctx.clearable && _ctx.modelValue
      }, _ctx.clearable && _ctx.modelValue ? {
        h: common_vendor.o(clearValue)
      } : {});
    };
  }
});
const Component = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-c262edfd"]]);
wx.createComponent(Component);
//# sourceMappingURL=../../.sourcemap/mp-weixin/components/DateTimePicker.js.map
