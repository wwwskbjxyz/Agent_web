"use strict";
const common_vendor = require("../../common/vendor.js");
const stores_platform = require("../../stores/platform.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "agent",
  setup(__props) {
    const platform = stores_platform.usePlatformStore();
    const mode = common_vendor.ref("login");
    const form = common_vendor.reactive({
      account: "",
      username: "",
      email: "",
      password: "",
      softwareCode: "",
      authorAccount: "",
      authorPassword: ""
    });
    const isWeChatMiniProgram = common_vendor.ref(false);
    isWeChatMiniProgram.value = true;
    const isBusy = common_vendor.computed(
      () => mode.value === "login" ? platform.loading.loginAgent : platform.loading.registerAgent
    );
    const wechatLoginBusy = common_vendor.computed(() => platform.loading.loginAgentWeChat);
    async function handleSubmit() {
      var _a;
      if (mode.value === "register") {
        if (!form.username || !form.email || !form.password || !form.softwareCode || !form.authorAccount || !form.authorPassword) {
          common_vendor.index.showToast({ title: "请完整填写注册信息", icon: "none" });
          return;
        }
        try {
          await platform.registerAgent({
            username: form.username.trim(),
            email: form.email.trim(),
            password: form.password,
            softwareCode: form.softwareCode.trim().toUpperCase(),
            authorAccount: form.authorAccount.trim(),
            authorPassword: form.authorPassword
          });
          common_vendor.index.showToast({ title: "注册成功，请登录", icon: "success" });
          form.account = form.email.trim();
          form.password = "";
          form.softwareCode = "";
          form.authorAccount = "";
          form.authorPassword = "";
          mode.value = "login";
          return;
        } catch (error) {
          const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "注册失败，请稍后重试";
          common_vendor.index.showToast({ title: message, icon: "none" });
          return;
        }
      }
      if (!form.account || !form.password) {
        common_vendor.index.showToast({ title: "请完整填写信息", icon: "none" });
        return;
      }
      try {
        const session = await platform.loginAgent({
          account: form.account.trim(),
          password: form.password
        });
        if ((_a = session == null ? void 0 : session.bindings) == null ? void 0 : _a.length) {
          common_vendor.index.reLaunch({ url: "/pages/index/index" });
        } else {
          common_vendor.index.showModal({
            title: "登录成功",
            content: "您还没有绑定任何作者软件码，是否现在绑定？",
            success(res) {
              if (res.confirm) {
                common_vendor.index.navigateTo({ url: "/pages/agent/bind" });
              } else {
                common_vendor.index.reLaunch({ url: "/pages/index/index" });
              }
            }
          });
        }
      } catch (error) {
        const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "操作失败，请稍后重试";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    function back() {
      common_vendor.index.reLaunch({ url: "/pages/login/index" });
    }
    async function handleWeChatLogin() {
      if (!isWeChatMiniProgram.value) {
        common_vendor.index.showToast({ title: "请在微信小程序内使用微信登录", icon: "none" });
        return;
      }
      if (wechatLoginBusy.value) {
        return;
      }
      try {
        const jsCode = await new Promise((resolve, reject) => {
          common_vendor.index.login({
            provider: "weixin",
            onlyAuthorize: true,
            success: (res) => {
              if (res.code) {
                resolve(res.code);
              } else {
                reject(new Error("未获取到登录凭证"));
              }
            },
            fail: (error) => reject(error)
          });
        });
        await platform.loginAgentWithWeChat(jsCode);
        common_vendor.index.reLaunch({ url: "/pages/index/index" });
      } catch (error) {
        const message = (error == null ? void 0 : error.errMsg) || (error == null ? void 0 : error.message) || "微信登录失败，请稍后再试";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: common_vendor.n(mode.value === "login" ? "active" : ""),
        b: common_vendor.o(($event) => mode.value = "login"),
        c: common_vendor.n(mode.value === "register" ? "active" : ""),
        d: common_vendor.o(($event) => mode.value = "register"),
        e: mode.value === "login"
      }, mode.value === "login" ? {
        f: form.account,
        g: common_vendor.o(($event) => form.account = $event.detail.value)
      } : {
        h: form.username,
        i: common_vendor.o(($event) => form.username = $event.detail.value)
      }, {
        j: mode.value === "register"
      }, mode.value === "register" ? {
        k: form.email,
        l: common_vendor.o(($event) => form.email = $event.detail.value)
      } : {}, {
        m: form.password,
        n: common_vendor.o(($event) => form.password = $event.detail.value),
        o: mode.value === "register"
      }, mode.value === "register" ? {
        p: form.softwareCode,
        q: common_vendor.o(($event) => form.softwareCode = $event.detail.value),
        r: form.authorAccount,
        s: common_vendor.o(($event) => form.authorAccount = $event.detail.value),
        t: form.authorPassword,
        v: common_vendor.o(($event) => form.authorPassword = $event.detail.value)
      } : {}, {
        w: common_vendor.t(mode.value === "login" ? isBusy.value ? "登录中..." : "立即登录" : isBusy.value ? "注册中..." : "完成注册"),
        x: isBusy.value,
        y: common_vendor.o(handleSubmit),
        z: common_vendor.o(back),
        A: common_vendor.t(wechatLoginBusy.value ? "微信登录中..." : "微信快捷登录"),
        B: wechatLoginBusy.value,
        C: common_vendor.o(handleWeChatLogin)
      });
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-84aa991c"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/login/agent.js.map
