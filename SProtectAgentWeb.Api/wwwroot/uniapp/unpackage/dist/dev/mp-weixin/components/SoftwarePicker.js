"use strict";
const common_vendor = require("../common/vendor.js");
const stores_app = require("../stores/app.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "SoftwarePicker",
  setup(__props) {
    const appStore = stores_app.useAppStore();
    const { softwareList, selectedSoftware } = common_vendor.storeToRefs(appStore);
    const options = common_vendor.computed(() => softwareList.value.map((item) => item.softwareName));
    const currentIndex = common_vendor.computed(() => {
      const index = options.value.findIndex((item) => item === selectedSoftware.value);
      return index >= 0 ? index : 0;
    });
    const currentLabel = common_vendor.computed(() => selectedSoftware.value || options.value[currentIndex.value] || "请选择软件");
    function onChange(event) {
      var _a;
      const index = Number(((_a = event == null ? void 0 : event.detail) == null ? void 0 : _a.value) ?? currentIndex.value);
      const value = options.value[index];
      if (value) {
        appStore.setSelectedSoftware(value);
      }
    }
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: options.value.length
      }, options.value.length ? {
        b: common_vendor.t(currentLabel.value),
        c: options.value,
        d: currentIndex.value,
        e: common_vendor.o(onChange)
      } : {});
    };
  }
});
const Component = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-167f4521"]]);
wx.createComponent(Component);
//# sourceMappingURL=../../.sourcemap/mp-weixin/components/SoftwarePicker.js.map
