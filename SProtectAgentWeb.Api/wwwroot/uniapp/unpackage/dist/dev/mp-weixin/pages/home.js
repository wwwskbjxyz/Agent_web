"use strict";
const common_vendor = require("../common/vendor.js");
const stores_platform = require("../stores/platform.js");
const stores_app = require("../stores/app.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "home",
  setup(__props) {
    const platform = stores_platform.usePlatformStore();
    const appStore = stores_app.useAppStore();
    common_vendor.onMounted(async () => {
      if (!platform.isAuthenticated) {
        return;
      }
      if (!platform.bindings.length) {
        await platform.fetchAgentProfile();
      }
      const active = platform.selectedBinding || platform.bindings[0];
      if (active) {
        appStore.login({ username: active.authorAccount, password: active.authorPassword }).catch(() => {
        });
      }
    });
    function select(item) {
      platform.selectBinding(item);
      appStore.login({ username: item.authorAccount, password: item.authorPassword }).then(() => {
        common_vendor.index.showToast({ title: "已切换 " + item.softwareCode, icon: "success" });
      }).catch((error) => {
        const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "自动登录作者端失败，请手动登录";
        common_vendor.index.showToast({ title: message, icon: "none" });
      });
    }
    function goBind() {
      common_vendor.index.navigateTo({ url: "/pages/agent/bind" });
    }
    function goLogin() {
      common_vendor.index.reLaunch({ url: "/pages/login/agent" });
    }
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: common_vendor.unref(platform).isAuthenticated
      }, common_vendor.unref(platform).isAuthenticated ? {
        b: common_vendor.o(goBind),
        c: common_vendor.f(common_vendor.unref(platform).bindings, (item, k0, i0) => {
          var _a;
          return {
            a: common_vendor.t(item.authorDisplayName || "未命名软件"),
            b: common_vendor.t(item.softwareType),
            c: common_vendor.t(item.apiAddress),
            d: common_vendor.t(item.apiPort),
            e: common_vendor.t(item.authorAccount),
            f: item.bindingId,
            g: common_vendor.n(((_a = common_vendor.unref(platform).selectedBinding) == null ? void 0 : _a.bindingId) === item.bindingId ? "active" : ""),
            h: common_vendor.o(($event) => select(item), item.bindingId)
          };
        })
      } : {
        d: common_vendor.o(goLogin)
      });
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-41317691"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../.sourcemap/mp-weixin/pages/home.js.map
