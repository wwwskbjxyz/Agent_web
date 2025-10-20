"use strict";
const common_vendor = require("../../common/vendor.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "index",
  setup(__props) {
    function goAgent() {
      common_vendor.index.navigateTo({ url: "/pages/login/agent" });
    }
    function goAuthor() {
      common_vendor.index.navigateTo({ url: "/pages/login/author" });
    }
    return (_ctx, _cache) => {
      return {
        a: common_vendor.o(goAgent),
        b: common_vendor.o(goAuthor)
      };
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-d08ef7d4"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/login/index.js.map
