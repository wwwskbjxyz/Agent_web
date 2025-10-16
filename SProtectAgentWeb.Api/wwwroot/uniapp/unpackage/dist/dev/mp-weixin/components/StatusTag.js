"use strict";
const common_vendor = require("../common/vendor.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "StatusTag",
  props: {
    status: {},
    label: {}
  },
  setup(__props) {
    const props = __props;
    const variantClass = common_vendor.computed(() => {
      switch (props.status) {
        case "success":
          return "status-success";
        case "warning":
          return "status-warning";
        case "error":
          return "status-error";
        default:
          return "status-info";
      }
    });
    return (_ctx, _cache) => {
      return {
        a: common_vendor.t(_ctx.label),
        b: common_vendor.n(variantClass.value)
      };
    };
  }
});
const Component = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-b93f6f4d"]]);
wx.createComponent(Component);
//# sourceMappingURL=../../.sourcemap/mp-weixin/components/StatusTag.js.map
