"use strict";
const common_vendor = require("../../common/vendor.js");
const stores_platform = require("../../stores/platform.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "dashboard",
  setup(__props) {
    const platform = stores_platform.usePlatformStore();
    const { authorWeChatBinding, authorWechatSubscriptions, wechatTemplates, wechatTemplatePreviews } = common_vendor.storeToRefs(platform);
    const profile = common_vendor.computed(() => platform.authorProfile);
    const softwares = common_vendor.computed(() => {
      var _a;
      return ((_a = profile.value) == null ? void 0 : _a.softwares) ?? [];
    });
    const selectedSoftwareId = common_vendor.computed({
      get: () => platform.selectedAuthorSoftwareId,
      set: (value) => platform.selectAuthorSoftware(value)
    });
    const currentSoftware = common_vendor.computed(() => {
      if (!softwares.value.length) {
        return null;
      }
      const matched = softwares.value.find((item) => item.softwareId === selectedSoftwareId.value);
      return matched ?? softwares.value[0] ?? null;
    });
    const isWeChatMiniProgram = common_vendor.ref(false);
    isWeChatMiniProgram.value = true;
    const isWeChatBound = common_vendor.computed(() => Boolean(authorWeChatBinding.value));
    const wechatBusy = common_vendor.computed(
      () => platform.loading.wechatBinding || platform.loading.wechatBind || platform.loading.wechatUnbind
    );
    const authorWechatTemplateOptions = common_vendor.computed(() => {
      var _a, _b, _c, _d, _e, _f;
      const config = wechatTemplates.value;
      if (!config) {
        return [];
      }
      const candidates = [
        {
          key: "instant",
          title: "即时沟通提醒",
          description: "代理或客户有新留言时提醒",
          templateId: ((_a = config.instantCommunication) == null ? void 0 : _a.trim()) ?? "",
          preview: ((_b = wechatTemplatePreviews.value) == null ? void 0 : _b["instant"]) ?? null
        },
        {
          key: "blacklist",
          title: "黑名单严重警告",
          description: "黑名单触发时快速通知",
          templateId: ((_c = config.blacklistAlert) == null ? void 0 : _c.trim()) ?? "",
          preview: ((_d = wechatTemplatePreviews.value) == null ? void 0 : _d["blacklist"]) ?? null
        },
        {
          key: "settlement",
          title: "结算账单通知",
          description: "结算周期到期时提醒处理账单",
          templateId: ((_e = config.settlementNotice) == null ? void 0 : _e.trim()) ?? "",
          preview: ((_f = wechatTemplatePreviews.value) == null ? void 0 : _f["settlement"]) ?? null
        }
      ];
      return candidates.filter((item) => item.templateId.length >= 10 && !item.templateId.includes("..."));
    });
    const subscriptionState = common_vendor.computed(() => authorWechatSubscriptions.value ?? {});
    const showSubscriptionPanel = common_vendor.computed(() => isWeChatBound.value && authorWechatTemplateOptions.value.length > 0);
    function maskOpenId(value) {
      if (!value) {
        return "";
      }
      if (value.length <= 6) {
        return value;
      }
      const prefix = value.slice(0, 4);
      const suffix = value.slice(-3);
      return `${prefix}***${suffix}`;
    }
    const wechatDisplayName = common_vendor.computed(() => {
      var _a;
      const binding = authorWeChatBinding.value;
      if (!binding) {
        return "";
      }
      const nickname = (_a = binding.nickname) == null ? void 0 : _a.trim();
      if (nickname) {
        return nickname;
      }
      return maskOpenId(binding.openId);
    });
    const wechatBindLabel = common_vendor.computed(() => {
      if (wechatBusy.value) {
        return "处理中...";
      }
      return isWeChatBound.value ? "重新绑定" : "绑定微信";
    });
    const wechatStatusText = common_vendor.computed(() => {
      if (!isWeChatMiniProgram.value) {
        return "仅微信小程序支持";
      }
      if (wechatBusy.value && !authorWeChatBinding.value) {
        return "加载中...";
      }
      return isWeChatBound.value ? "已绑定" : "未绑定";
    });
    const wechatSubscribeBusy = common_vendor.ref(false);
    function onSubscriptionToggle(key, value) {
      platform.setAuthorWechatSubscription(key, value);
    }
    async function applySubscriptionRequests(auto = false) {
      if (!isWeChatMiniProgram.value) {
        if (!auto) {
          common_vendor.index.showToast({ title: "请在微信小程序内操作", icon: "none" });
        }
        return;
      }
      await platform.fetchWeChatTemplates().catch(() => void 0);
      const selected = authorWechatTemplateOptions.value.filter((option) => subscriptionState.value[option.key] ?? true);
      if (!selected.length) {
        if (!auto) {
          common_vendor.index.showToast({ title: "请至少开启一个提醒类型", icon: "none" });
        }
        return;
      }
      const tmplIds = selected.map((option) => option.templateId);
      wechatSubscribeBusy.value = true;
      try {
        await new Promise((resolve, reject) => {
          common_vendor.index.requestSubscribeMessage({
            tmplIds,
            success: () => resolve(),
            fail: (error) => reject(error)
          });
        });
        if (!auto) {
          common_vendor.index.showToast({ title: "已提交订阅", icon: "success" });
        }
        let previewDelivered = false;
        for (const option of selected) {
          const delivered = await platform.sendWeChatPreview("author", option.key, auto);
          previewDelivered = previewDelivered || delivered;
        }
        if (!auto && previewDelivered) {
          common_vendor.index.showToast({ title: "已发送测试提醒", icon: "success" });
        }
      } catch (error) {
        const rawMessage = (error == null ? void 0 : error.errMsg) || (error == null ? void 0 : error.message) || "";
        if (typeof rawMessage === "string" && rawMessage.includes("No template data return")) {
          common_vendor.index.showModal({
            title: "订阅失败",
            content: "微信返回“未找到模板”，请确认小程序已配置对应的订阅消息模板并在平台中填写正确的模板 ID。",
            showCancel: false
          });
        } else if (!auto) {
          const message = rawMessage || "订阅失败，请稍后重试";
          common_vendor.index.showToast({ title: message, icon: "none" });
        }
      } finally {
        wechatSubscribeBusy.value = false;
      }
    }
    function seedDefaultSubscriptions() {
      if (!isWeChatBound.value || !authorWechatTemplateOptions.value.length) {
        return;
      }
      const current = subscriptionState.value;
      if (current && Object.keys(current).length > 0) {
        return;
      }
      authorWechatTemplateOptions.value.forEach((option) => {
        platform.setAuthorWechatSubscription(option.key, true);
      });
    }
    function mpLogin() {
      return new Promise((resolve, reject) => {
        common_vendor.index.login({
          provider: "weixin",
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
    }
    async function ensureWeChatBinding() {
      if (!isWeChatMiniProgram.value || !platform.isAuthorAuthenticated) {
        return;
      }
      await platform.fetchWeChatBinding("author").catch(() => void 0);
      platform.fetchWeChatTemplates().catch(() => void 0);
    }
    common_vendor.watch(
      () => {
        var _a;
        return [((_a = authorWeChatBinding.value) == null ? void 0 : _a.openId) ?? null, authorWechatTemplateOptions.value.length];
      },
      () => {
        seedDefaultSubscriptions();
      },
      { immediate: true }
    );
    async function handleWeChatBind() {
      var _a;
      if (!isWeChatMiniProgram.value) {
        common_vendor.index.showToast({ title: "请在微信小程序内操作", icon: "none" });
        return;
      }
      try {
        const code = await mpLogin();
        let nickname;
        try {
          if (typeof common_vendor.index.getUserProfile === "function") {
            const profile2 = await new Promise((resolve, reject) => {
              common_vendor.index.getUserProfile({
                desc: "用于展示绑定的微信昵称",
                success: (res) => resolve(res),
                fail: (error) => reject(error)
              });
            });
            nickname = (_a = profile2.userInfo) == null ? void 0 : _a.nickName;
          }
        } catch (error) {
          common_vendor.index.__f__("warn", "at pages/author/dashboard.vue:418", "获取作者昵称失败，继续绑定", error);
        }
        await platform.bindWeChat("author", code, nickname);
        await platform.fetchWeChatTemplates().catch(() => void 0);
        seedDefaultSubscriptions();
        common_vendor.index.showToast({ title: "绑定成功", icon: "success" });
        if (showSubscriptionPanel.value) {
          common_vendor.index.showModal({
            title: "订阅提醒",
            content: "是否立即授权接收微信通知？",
            confirmText: "立即授权",
            cancelText: "稍后",
            success: (res) => {
              if (res.confirm) {
                applySubscriptionRequests(true).catch(() => void 0);
              }
            }
          });
        }
      } catch (error) {
        const message = (error == null ? void 0 : error.errMsg) || (error == null ? void 0 : error.message) || "绑定失败，请稍后重试";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    async function handleWeChatUnbind() {
      if (!isWeChatMiniProgram.value || !isWeChatBound.value) {
        return;
      }
      const confirmed = await new Promise((resolve) => {
        common_vendor.index.showModal({
          title: "确认解绑",
          content: "解绑后将无法接收订阅提醒，是否继续？",
          success: (res) => resolve(res.confirm),
          fail: () => resolve(false)
        });
      });
      if (!confirmed) {
        return;
      }
      try {
        await platform.unbindWeChat("author");
        common_vendor.index.showToast({ title: "已解绑", icon: "success" });
      } catch (error) {
        const message = (error == null ? void 0 : error.errMsg) || (error == null ? void 0 : error.message) || "解绑失败，请稍后重试";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    const isCreating = common_vendor.ref(false);
    const form = common_vendor.reactive({
      softwareId: 0,
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
    const currentSoftwareType = common_vendor.computed(
      () => softwareTypes.find((item) => item.value === form.softwareType) ?? softwareTypes[0]
    );
    const saving = common_vendor.computed(
      () => platform.loading.updateAuthor || platform.loading.updateAuthorSoftware || platform.loading.createAuthorSoftware
    );
    common_vendor.watch(
      currentSoftware,
      (software) => {
        if (!software || isCreating.value) {
          return;
        }
        form.softwareId = software.softwareId;
        form.displayName = software.displayName;
        form.apiAddress = software.apiAddress;
        form.apiPort = software.apiPort;
        form.softwareType = software.softwareType;
      },
      { immediate: true }
    );
    common_vendor.onMounted(async () => {
      if (!platform.isAuthorAuthenticated) {
        common_vendor.index.reLaunch({ url: "/pages/login/author" });
        return;
      }
      if (!profile.value) {
        try {
          await platform.fetchAuthorProfile();
        } catch (error) {
          common_vendor.index.showToast({ title: "加载作者信息失败", icon: "none" });
        }
      }
      await ensureWeChatBinding();
    });
    function handleTypeChange(event) {
      const index = Number(event.detail.value ?? 0);
      const target = softwareTypes[index] ?? softwareTypes[0];
      form.softwareType = target.value;
    }
    function handleSoftwareChange(event) {
      const index = Number(event.detail.value ?? 0);
      const target = softwares.value[index];
      if (target) {
        isCreating.value = false;
        platform.selectAuthorSoftware(target.softwareId);
      }
    }
    function copySoftwareCode(code) {
      if (!code) {
        return;
      }
      common_vendor.index.setClipboardData({
        data: code,
        success: () => {
          common_vendor.index.showToast({ title: "软件码已复制", icon: "none" });
        },
        fail: () => {
          common_vendor.index.showToast({ title: "复制失败", icon: "none" });
        }
      });
    }
    function selectSoftware(software) {
      isCreating.value = false;
      platform.selectAuthorSoftware(software.softwareId);
    }
    function editSoftware(software) {
      selectSoftware(software);
      form.softwareId = software.softwareId;
      form.displayName = software.displayName;
      form.apiAddress = software.apiAddress;
      form.apiPort = software.apiPort;
      form.softwareType = software.softwareType;
      isCreating.value = false;
    }
    function resolveTypeLabel(type) {
      const target = softwareTypes.find((item) => item.value === type);
      return (target == null ? void 0 : target.label) ?? type;
    }
    function startCreate() {
      isCreating.value = true;
      form.softwareId = 0;
      form.displayName = "";
      form.apiAddress = "";
      form.apiPort = 8080;
      form.softwareType = "SP";
    }
    function cancelCreate() {
      isCreating.value = false;
      if (currentSoftware.value) {
        form.softwareId = currentSoftware.value.softwareId;
        form.displayName = currentSoftware.value.displayName;
        form.apiAddress = currentSoftware.value.apiAddress;
        form.apiPort = currentSoftware.value.apiPort;
        form.softwareType = currentSoftware.value.softwareType;
      }
    }
    async function handleSave() {
      if (!form.displayName || !form.apiAddress) {
        common_vendor.index.showToast({ title: "请完善信息", icon: "none" });
        return;
      }
      try {
        if (isCreating.value) {
          const software = await platform.createAuthorSoftware({
            displayName: form.displayName.trim(),
            apiAddress: form.apiAddress.trim(),
            apiPort: form.apiPort,
            softwareType: form.softwareType
          });
          isCreating.value = false;
          common_vendor.index.showToast({ title: `已新增：${software.softwareCode}`, icon: "none" });
        } else if (form.softwareId) {
          await platform.updateAuthorSoftware(form.softwareId, {
            displayName: form.displayName.trim(),
            apiAddress: form.apiAddress.trim(),
            apiPort: form.apiPort,
            softwareType: form.softwareType
          });
          common_vendor.index.showToast({ title: "已保存", icon: "success" });
        }
      } catch (error) {
        const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "操作失败，请稍后重试";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    async function handleRegenerate() {
      if (!currentSoftware.value) {
        return;
      }
      try {
        const code = await platform.regenerateAuthorCode(currentSoftware.value.softwareId);
        common_vendor.index.showToast({ title: `新软件码：${code}`, icon: "none" });
        await platform.fetchAuthorProfile();
      } catch (error) {
        const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "生成失败，请稍后重试";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    function handleLogout() {
      platform.logoutAuthor();
      common_vendor.index.reLaunch({ url: "/pages/login/author" });
    }
    function handleDeleteAccount() {
      common_vendor.index.showModal({
        title: "确认注销作者账号",
        content: "该操作不可恢复，确认继续？",
        success: async (res) => {
          if (!res.confirm)
            return;
          try {
            await platform.deleteAuthorAccount();
            common_vendor.index.showToast({ title: "账号已注销", icon: "none" });
            common_vendor.index.reLaunch({ url: "/pages/login/author" });
          } catch (error) {
            const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "操作失败，请稍后重试";
            common_vendor.index.showToast({ title: message, icon: "none" });
          }
        }
      });
    }
    function handleDeleteSoftware() {
      if (!currentSoftware.value || softwares.value.length <= 1) {
        return;
      }
      common_vendor.index.showModal({
        title: "确认删除软件码",
        content: "删除后代理无法再使用此软件码，是否继续？",
        success: async (res) => {
          if (!res.confirm || !currentSoftware.value)
            return;
          try {
            await platform.deleteAuthorSoftware(currentSoftware.value.softwareId);
            common_vendor.index.showToast({ title: "已删除", icon: "success" });
          } catch (error) {
            const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "删除失败，请稍后重试";
            common_vendor.index.showToast({ title: message, icon: "none" });
          }
        }
      });
    }
    return (_ctx, _cache) => {
      var _a;
      return common_vendor.e({
        a: profile.value
      }, profile.value ? common_vendor.e({
        b: common_vendor.o(handleLogout),
        c: common_vendor.t(common_vendor.unref(platform).loading.deleteAuthor ? "注销中..." : "注销账号"),
        d: common_vendor.unref(platform).loading.deleteAuthor,
        e: common_vendor.o(handleDeleteAccount),
        f: isWeChatMiniProgram.value
      }, isWeChatMiniProgram.value ? common_vendor.e({
        g: common_vendor.t(wechatStatusText.value),
        h: wechatDisplayName.value
      }, wechatDisplayName.value ? {
        i: common_vendor.t(wechatDisplayName.value)
      } : {}, {
        j: common_vendor.t(wechatBindLabel.value),
        k: wechatBusy.value,
        l: common_vendor.o(handleWeChatBind),
        m: isWeChatBound.value
      }, isWeChatBound.value ? {
        n: wechatBusy.value,
        o: common_vendor.o(handleWeChatUnbind)
      } : {}, {
        p: showSubscriptionPanel.value
      }, showSubscriptionPanel.value ? {
        q: common_vendor.f(authorWechatTemplateOptions.value, (option, k0, i0) => {
          return {
            a: common_vendor.t(option.title),
            b: common_vendor.t(option.description),
            c: subscriptionState.value[option.key] !== false,
            d: common_vendor.o(($event) => onSubscriptionToggle(option.key, $event.detail.value), option.key),
            e: option.key
          };
        }),
        r: common_vendor.t(wechatSubscribeBusy.value ? "授权中..." : "保存并请求订阅"),
        s: wechatSubscribeBusy.value,
        t: common_vendor.o(($event) => applySubscriptionRequests())
      } : common_vendor.e({
        v: isWeChatBound.value
      }, isWeChatBound.value ? {} : {})) : {}, {
        w: common_vendor.t(common_vendor.unref(platform).loading.createAuthorSoftware ? "处理中..." : "新增软件码"),
        x: common_vendor.o(startCreate),
        y: common_vendor.unref(platform).loading.createAuthorSoftware,
        z: softwares.value.length
      }, softwares.value.length ? common_vendor.e({
        A: softwares.value.length > 1
      }, softwares.value.length > 1 ? {
        B: common_vendor.t(((_a = currentSoftware.value) == null ? void 0 : _a.displayName) || "选择软件"),
        C: softwares.value,
        D: common_vendor.o(handleSoftwareChange)
      } : {}) : {}, {
        E: softwares.value.length
      }, softwares.value.length ? {
        F: common_vendor.f(softwares.value, (software, k0, i0) => {
          var _a2, _b, _c;
          return {
            a: common_vendor.t(software.displayName),
            b: common_vendor.t(resolveTypeLabel(software.softwareType)),
            c: common_vendor.t(software.softwareCode),
            d: common_vendor.o(($event) => copySoftwareCode(software.softwareCode), software.softwareId),
            e: common_vendor.t(software.apiAddress),
            f: common_vendor.t(software.apiPort),
            g: common_vendor.t(((_a2 = currentSoftware.value) == null ? void 0 : _a2.softwareId) === software.softwareId ? "当前使用" : "设为当前"),
            h: ((_b = currentSoftware.value) == null ? void 0 : _b.softwareId) === software.softwareId,
            i: common_vendor.o(($event) => selectSoftware(software), software.softwareId),
            j: common_vendor.o(($event) => editSoftware(software), software.softwareId),
            k: software.softwareId,
            l: common_vendor.n(((_c = currentSoftware.value) == null ? void 0 : _c.softwareId) === software.softwareId ? "active" : "")
          };
        })
      } : {}, {
        G: currentSoftware.value
      }, currentSoftware.value ? {
        H: common_vendor.t(currentSoftware.value.softwareCode),
        I: common_vendor.o(($event) => copySoftwareCode(currentSoftware.value.softwareCode)),
        J: common_vendor.t(common_vendor.unref(platform).loading.regenerateAuthorCode ? "生成中..." : "刷新软件码"),
        K: common_vendor.o(handleRegenerate),
        L: common_vendor.unref(platform).loading.regenerateAuthorCode,
        M: common_vendor.o(handleDeleteSoftware),
        N: common_vendor.unref(platform).loading.deleteAuthorSoftware || softwares.value.length <= 1
      } : {}, {
        O: isCreating.value || currentSoftware.value
      }, isCreating.value || currentSoftware.value ? common_vendor.e({
        P: common_vendor.t(isCreating.value ? "新增软件配置" : "更新接口配置"),
        Q: form.displayName,
        R: common_vendor.o(($event) => form.displayName = $event.detail.value),
        S: form.apiAddress,
        T: common_vendor.o(($event) => form.apiAddress = $event.detail.value),
        U: form.apiPort,
        V: common_vendor.o(common_vendor.m(($event) => form.apiPort = $event.detail.value, {
          number: true
        })),
        W: common_vendor.t(currentSoftwareType.value.label),
        X: softwareTypes,
        Y: common_vendor.o(handleTypeChange),
        Z: common_vendor.t(saving.value ? "保存中..." : isCreating.value ? "提交新增" : "保存修改"),
        aa: saving.value,
        ab: common_vendor.o(handleSave),
        ac: isCreating.value
      }, isCreating.value ? {
        ad: common_vendor.o(cancelCreate)
      } : {}) : {}, {
        ae: !isCreating.value && currentSoftware.value
      }, !isCreating.value && currentSoftware.value ? {} : {}) : {});
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-b793f51d"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/author/dashboard.js.map
