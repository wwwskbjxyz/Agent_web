"use strict";
const common_vendor = require("../../common/vendor.js");
const stores_platform = require("../../stores/platform.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "author",
  setup(__props) {
    const platform = stores_platform.usePlatformStore();
    const mode = common_vendor.ref("login");
    const profile = common_vendor.ref(null);
    const form = common_vendor.reactive({
      account: "",
      username: "",
      email: "",
      password: "",
      displayName: "",
      apiAddress: "",
      apiPort: 8080,
      softwareType: "SP"
    });
    const softwareTypes = [
      { value: "SP", label: "SProtect (默认)" },
      { value: "QP", label: "QProtect" },
      { value: "API", label: "自定义 API" }
    ];
    const currentSoftwareType = common_vendor.computed(() => {
      return softwareTypes.find((item) => item.value === form.softwareType) ?? softwareTypes[0];
    });
    const isBusy = common_vendor.computed(
      () => mode.value === "login" ? platform.loading.loginAuthor : platform.loading.registerAuthor
    );
    function handleTypeChange(event) {
      const index = Number(event.detail.value ?? 0);
      const target = softwareTypes[index] ?? softwareTypes[0];
      form.softwareType = target.value;
    }
    async function handleSubmit() {
      if (mode.value === "register") {
        if (!form.username || !form.email || !form.password || !form.displayName || !form.apiAddress) {
          common_vendor.index.showToast({ title: "请完整填写注册信息", icon: "none" });
          return;
        }
        try {
          const result = await platform.registerAuthor({
            username: form.username.trim(),
            email: form.email.trim(),
            password: form.password,
            displayName: form.displayName.trim(),
            apiAddress: form.apiAddress.trim(),
            apiPort: form.apiPort,
            softwareType: form.softwareType
          });
          profile.value = result;
          common_vendor.index.showToast({ title: "注册成功，请登录", icon: "success" });
          form.account = form.email.trim();
          form.password = "";
          mode.value = "login";
          return;
        } catch (error) {
          const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "操作失败，请稍后重试";
          common_vendor.index.showToast({ title: message, icon: "none" });
          return;
        }
      }
      if (!form.account || !form.password) {
        common_vendor.index.showToast({ title: "请完整填写信息", icon: "none" });
        return;
      }
      try {
        const result = await platform.loginAuthor({ account: form.account.trim(), password: form.password });
        profile.value = result;
        common_vendor.index.showToast({ title: "登录成功", icon: "success" });
        await platform.fetchAuthorProfile();
        common_vendor.index.reLaunch({ url: "/pages/author/dashboard" });
      } catch (error) {
        const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "操作失败，请稍后重试";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    function back() {
      common_vendor.index.reLaunch({ url: "/pages/login/index" });
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
        p: form.displayName,
        q: common_vendor.o(($event) => form.displayName = $event.detail.value)
      } : {}, {
        r: mode.value === "register"
      }, mode.value === "register" ? {
        s: form.apiAddress,
        t: common_vendor.o(($event) => form.apiAddress = $event.detail.value)
      } : {}, {
        v: mode.value === "register"
      }, mode.value === "register" ? {
        w: form.apiPort,
        x: common_vendor.o(common_vendor.m(($event) => form.apiPort = $event.detail.value, {
          number: true
        }))
      } : {}, {
        y: mode.value === "register"
      }, mode.value === "register" ? {
        z: common_vendor.t(currentSoftwareType.value.label),
        A: softwareTypes,
        B: common_vendor.o(handleTypeChange)
      } : {}, {
        C: common_vendor.t(mode.value === "login" ? isBusy.value ? "登录中..." : "立即登录" : isBusy.value ? "注册中..." : "提交注册"),
        D: isBusy.value,
        E: common_vendor.o(handleSubmit),
        F: mode.value === "login" && profile.value
      }, mode.value === "login" && profile.value ? {
        G: common_vendor.t(profile.value.softwareCode),
        H: common_vendor.t(profile.value.apiAddress),
        I: common_vendor.t(profile.value.apiPort)
      } : {}, {
        J: common_vendor.o(back)
      });
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-3c84a564"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/login/author.js.map
