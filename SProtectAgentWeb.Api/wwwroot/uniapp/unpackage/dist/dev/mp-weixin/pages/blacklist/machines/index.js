"use strict";
const common_vendor = require("../../../common/vendor.js");
const stores_app = require("../../../stores/app.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "index",
  setup(__props) {
    const appStore = stores_app.useAppStore();
    const { blacklistMachines, selectedSoftware } = common_vendor.storeToRefs(appStore);
    const loading = common_vendor.computed(() => appStore.loading.blacklistMachines);
    const machines = common_vendor.computed(() => blacklistMachines.value);
    const submitting = common_vendor.ref(false);
    const typeOptions = [
      { label: "机器码 (2)", value: 2 },
      { label: "IP (1)", value: 1 },
      { label: "其他 (0)", value: 0 }
    ];
    const form = common_vendor.reactive({
      value: "",
      typeIndex: 0,
      remark: ""
    });
    function onTypeChange(event) {
      form.typeIndex = Number(event.detail.value) || 0;
    }
    async function load() {
      await appStore.loadBlacklistMachines();
    }
    async function refresh() {
      await load();
    }
    function formatType(type) {
      const match = typeOptions.find((item) => item.value === type);
      if (match)
        return match.label.replace(/ \(\d+\)/, "");
      return "其他";
    }
    async function create() {
      var _a;
      if (!form.value.trim()) {
        common_vendor.index.showToast({ title: "请输入机器码", icon: "none" });
        return;
      }
      submitting.value = true;
      try {
        await appStore.createBlacklistMachine({
          value: form.value.trim(),
          type: ((_a = typeOptions[form.typeIndex]) == null ? void 0 : _a.value) ?? 2,
          remarks: form.remark.trim() || void 0
        });
        form.value = "";
        form.remark = "";
        common_vendor.index.showToast({ title: "已加入黑名单", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/blacklist/machines/index.vue:101", "create blacklist machine error", error);
        common_vendor.index.showToast({ title: "操作失败", icon: "none" });
      } finally {
        submitting.value = false;
      }
    }
    async function remove(value) {
      if (!value)
        return;
      try {
        await appStore.deleteBlacklistMachines([value]);
        common_vendor.index.showToast({ title: "已删除", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/blacklist/machines/index.vue:114", "delete blacklist machine error", error);
        common_vendor.index.showToast({ title: "删除失败", icon: "none" });
      }
    }
    common_vendor.onMounted(async () => {
      await appStore.ensureReady();
      await load();
    });
    common_vendor.watch(
      () => selectedSoftware.value,
      async (next, prev) => {
        if (!next || next === prev)
          return;
        await load();
      }
    );
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: form.value,
        b: common_vendor.o(($event) => form.value = $event.detail.value),
        c: common_vendor.t(typeOptions[form.typeIndex].label),
        d: typeOptions.map((item) => item.label),
        e: form.typeIndex,
        f: common_vendor.o(onTypeChange),
        g: form.remark,
        h: common_vendor.o(($event) => form.remark = $event.detail.value),
        i: common_vendor.t(submitting.value ? "提交中..." : "加入黑名单"),
        j: !form.value.trim() || submitting.value,
        k: common_vendor.o(create),
        l: loading.value,
        m: common_vendor.o(refresh),
        n: loading.value
      }, loading.value ? {} : !machines.value.length ? {} : {
        p: common_vendor.f(machines.value, (item, k0, i0) => {
          return {
            a: common_vendor.t(item.value),
            b: common_vendor.t(formatType(item.type)),
            c: common_vendor.t(item.remarks || "-"),
            d: common_vendor.o(($event) => remove(item.value), item.value),
            e: item.value
          };
        })
      }, {
        o: !machines.value.length
      });
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-e0faa947"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../../.sourcemap/mp-weixin/pages/blacklist/machines/index.js.map
