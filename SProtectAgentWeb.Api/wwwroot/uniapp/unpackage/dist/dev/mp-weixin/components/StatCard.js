"use strict";
const common_vendor = require("../common/vendor.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "StatCard",
  props: {
    title: {},
    value: {},
    delta: {},
    trend: {}
  },
  setup(__props) {
    const props = __props;
    const deltaClass = common_vendor.computed(() => {
      if (props.trend === "up")
        return "delta-up";
      if (props.trend === "down")
        return "delta-down";
      return "delta-flat";
    });
    const deltaValue = common_vendor.computed(() => `${Math.abs(props.delta)}%`);
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: common_vendor.t(_ctx.title),
        b: common_vendor.t(_ctx.value),
        c: _ctx.trend === "up"
      }, _ctx.trend === "up" ? {} : _ctx.trend === "down" ? {} : {}, {
        d: _ctx.trend === "down",
        e: common_vendor.t(deltaValue.value),
        f: common_vendor.n(deltaClass.value)
      });
    };
  }
});
const Component = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-25df140b"]]);
wx.createComponent(Component);
//# sourceMappingURL=../../.sourcemap/mp-weixin/components/StatCard.js.map
