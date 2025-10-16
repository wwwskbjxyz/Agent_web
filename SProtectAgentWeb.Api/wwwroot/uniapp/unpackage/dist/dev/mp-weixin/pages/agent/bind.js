"use strict";
const common_vendor = require("../../common/vendor.js");
const stores_platform = require("../../stores/platform.js");
const stores_app = require("../../stores/app.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "bind",
  setup(__props) {
    const platform = stores_platform.usePlatformStore();
    const appStore = stores_app.useAppStore();
    const form = common_vendor.reactive({
      softwareCode: "",
      authorAccount: "",
      authorPassword: ""
    });
    const isSubmitting = common_vendor.computed(() => platform.loading.createBinding);
    const bindings = common_vendor.computed(() => platform.bindings);
    const editorLoading = common_vendor.computed(() => platform.loading.updateBinding);
    const showEditor = common_vendor.ref(false);
    const editor = common_vendor.reactive({
      bindingId: 0,
      authorAccount: "",
      authorPassword: ""
    });
    async function loginWithBinding(binding, options) {
      if (!binding.authorAccount || !binding.authorPassword) {
        return;
      }
      try {
        await appStore.login(
          { username: binding.authorAccount, password: binding.authorPassword },
          { skipBootstrap: false }
        );
      } catch (error) {
        if (!(options == null ? void 0 : options.silent)) {
          const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "自动登录作者端失败，请稍后重试";
          common_vendor.index.showToast({ title: message, icon: "none" });
        }
        throw error;
      }
    }
    common_vendor.onMounted(async () => {
      if (!platform.isAuthenticated) {
        common_vendor.index.reLaunch({ url: "/pages/login/agent" });
        return;
      }
      if (!platform.bindings.length) {
        await platform.fetchAgentProfile();
      }
    });
    async function handleBind() {
      if (!form.softwareCode || !form.authorAccount || !form.authorPassword) {
        common_vendor.index.showToast({ title: "请完整填写信息", icon: "none" });
        return;
      }
      try {
        const result = await platform.createBinding({ ...form });
        common_vendor.index.showToast({ title: "绑定成功", icon: "success" });
        platform.selectBinding(result);
        try {
          await loginWithBinding(result, { silent: true });
        } catch (error) {
          common_vendor.index.__f__("warn", "at pages/agent/bind.vue:142", "登录作者端失败", error);
        }
        form.softwareCode = "";
        form.authorAccount = "";
        form.authorPassword = "";
      } catch (error) {
        const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "绑定失败，请检查软件码";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    function select(item) {
      platform.selectBinding(item);
      loginWithBinding(item, { silent: true }).then(() => {
        common_vendor.index.showToast({ title: "已切换到 " + item.softwareCode, icon: "success" });
        common_vendor.index.reLaunch({ url: "/pages/index/index" });
      }).catch(() => {
        common_vendor.index.reLaunch({ url: "/pages/index/index" });
      });
    }
    function edit(item) {
      editor.bindingId = item.bindingId;
      editor.authorAccount = item.authorAccount;
      editor.authorPassword = item.authorPassword;
      showEditor.value = true;
    }
    function closeEditor() {
      showEditor.value = false;
    }
    async function submitEdit() {
      if (!editor.authorAccount || !editor.authorPassword) {
        common_vendor.index.showToast({ title: "请输入完整信息", icon: "none" });
        return;
      }
      const target = bindings.value.find((item) => item.bindingId === editor.bindingId);
      if (!target) {
        common_vendor.index.showToast({ title: "未找到绑定记录", icon: "none" });
        return;
      }
      try {
        await platform.updateBinding(editor.bindingId, {
          softwareCode: target.softwareCode,
          authorAccount: editor.authorAccount.trim(),
          authorPassword: editor.authorPassword
        });
        common_vendor.index.showToast({ title: "已更新", icon: "success" });
        closeEditor();
      } catch (error) {
        const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "更新失败，请稍后重试";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    async function remove(bindingId) {
      common_vendor.index.showModal({
        title: "确认删除",
        content: "删除后将无法通过主控访问该作者接口，确定删除吗？",
        success: async (res) => {
          if (!res.confirm)
            return;
          await platform.deleteBinding(bindingId);
          common_vendor.index.showToast({ title: "已删除", icon: "success" });
        }
      });
    }
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: form.softwareCode,
        b: common_vendor.o(($event) => form.softwareCode = $event.detail.value),
        c: form.authorAccount,
        d: common_vendor.o(($event) => form.authorAccount = $event.detail.value),
        e: form.authorPassword,
        f: common_vendor.o(($event) => form.authorPassword = $event.detail.value),
        g: common_vendor.t(isSubmitting.value ? "绑定中..." : "立即绑定"),
        h: isSubmitting.value,
        i: common_vendor.o(handleBind),
        j: !bindings.value.length
      }, !bindings.value.length ? {} : {}, {
        k: common_vendor.f(bindings.value, (item, k0, i0) => {
          return {
            a: common_vendor.t(item.authorDisplayName),
            b: common_vendor.t(item.softwareCode),
            c: common_vendor.t(item.apiAddress),
            d: common_vendor.t(item.apiPort),
            e: common_vendor.o(($event) => edit(item), item.bindingId),
            f: common_vendor.o(($event) => select(item), item.bindingId),
            g: common_vendor.o(($event) => remove(item.bindingId), item.bindingId),
            h: item.bindingId
          };
        }),
        l: common_vendor.unref(platform).loading.deleteBinding,
        m: showEditor.value
      }, showEditor.value ? {
        n: editor.authorAccount,
        o: common_vendor.o(($event) => editor.authorAccount = $event.detail.value),
        p: editor.authorPassword,
        q: common_vendor.o(($event) => editor.authorPassword = $event.detail.value),
        r: common_vendor.o(closeEditor),
        s: common_vendor.t(editorLoading.value ? "保存中..." : "保存修改"),
        t: editorLoading.value,
        v: common_vendor.o(submitEdit),
        w: common_vendor.o(closeEditor)
      } : {});
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-b0b3ed2a"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/agent/bind.js.map
