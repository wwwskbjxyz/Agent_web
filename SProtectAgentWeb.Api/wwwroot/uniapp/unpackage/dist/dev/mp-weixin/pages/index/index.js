"use strict";
const common_vendor = require("../../common/vendor.js");
const stores_app = require("../../stores/app.js");
const stores_platform = require("../../stores/platform.js");
const utils_time = require("../../utils/time.js");
if (!Math) {
  (SoftwarePicker + StatCard + TrendChart + SubTrendChart + DateTimePicker)();
}
const TrendChart = () => "../../components/TrendChart.js";
const SubTrendChart = () => "../../components/SubTrendChart.js";
const StatCard = () => "../../components/StatCard.js";
const SoftwarePicker = () => "../../components/SoftwarePicker.js";
const DateTimePicker = () => "../../components/DateTimePicker.js";
const SALES_PAGE_SIZE = 20;
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "index",
  setup(__props) {
    const appStore = stores_app.useAppStore();
    const {
      dashboard,
      systemStatus,
      profile,
      selectedSoftware,
      cardTypes,
      agents,
      theme,
      chatUnreadCount,
      hasNewBlacklistLogs,
      settlementHasReminder
    } = common_vendor.storeToRefs(appStore);
    const platformStore = stores_platform.usePlatformStore();
    const { agentWeChatBinding, agentWechatSubscriptions, wechatTemplates, wechatTemplatePreviews } = common_vendor.storeToRefs(platformStore);
    const isWeChatMiniProgram = common_vendor.ref(false);
    isWeChatMiniProgram.value = true;
    const isWeChatBound = common_vendor.computed(() => Boolean(agentWeChatBinding.value));
    const wechatBusy = common_vendor.computed(
      () => platformStore.loading.wechatBinding || platformStore.loading.wechatBind || platformStore.loading.wechatUnbind
    );
    const wechatTemplateOptions = common_vendor.computed(() => {
      var _a, _b, _c, _d, _e, _f;
      const config = wechatTemplates.value;
      if (!config) {
        return [];
      }
      const candidates = [
        {
          key: "instant",
          title: "即时沟通提醒",
          description: "客户有新留言或待处理消息时提醒",
          templateId: ((_a = config.instantCommunication) == null ? void 0 : _a.trim()) ?? "",
          preview: ((_b = wechatTemplatePreviews.value) == null ? void 0 : _b["instant"]) ?? null
        },
        {
          key: "blacklist",
          title: "黑名单严重警告",
          description: "黑名单记录触发时第一时间通知",
          templateId: ((_c = config.blacklistAlert) == null ? void 0 : _c.trim()) ?? "",
          preview: ((_d = wechatTemplatePreviews.value) == null ? void 0 : _d["blacklist"]) ?? null
        },
        {
          key: "settlement",
          title: "结算账单通知",
          description: "结算周期到期后提醒待对账金额",
          templateId: ((_e = config.settlementNotice) == null ? void 0 : _e.trim()) ?? "",
          preview: ((_f = wechatTemplatePreviews.value) == null ? void 0 : _f["settlement"]) ?? null
        }
      ];
      return candidates.filter((item) => item.templateId.length >= 10 && !item.templateId.includes("..."));
    });
    const subscriptionState = common_vendor.computed(() => agentWechatSubscriptions.value ?? {});
    const showSubscriptionPanel = common_vendor.computed(() => isWeChatBound.value && wechatTemplateOptions.value.length > 0);
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
      const binding = agentWeChatBinding.value;
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
      if (wechatBusy.value && !agentWeChatBinding.value) {
        return "加载中...";
      }
      return isWeChatBound.value ? "已绑定" : "未绑定";
    });
    const wechatSubscribeBusy = common_vendor.ref(false);
    function onSubscriptionToggle(key, value) {
      platformStore.setAgentWechatSubscription(key, value);
    }
    async function applySubscriptionRequests(auto = false) {
      if (!isWeChatMiniProgram.value) {
        if (!auto) {
          common_vendor.index.showToast({ title: "请在微信小程序内操作", icon: "none" });
        }
        return;
      }
      await platformStore.fetchWeChatTemplates().catch(() => void 0);
      const selected = wechatTemplateOptions.value.filter((option) => subscriptionState.value[option.key] ?? true);
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
          const delivered = await platformStore.sendWeChatPreview("agent", option.key, auto);
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
      if (!isWeChatBound.value || !wechatTemplateOptions.value.length) {
        return;
      }
      const current = subscriptionState.value;
      if (current && Object.keys(current).length > 0) {
        return;
      }
      wechatTemplateOptions.value.forEach((option) => {
        platformStore.setAgentWechatSubscription(option.key, true);
      });
    }
    const bindingLoginBusy = common_vendor.ref(false);
    const bindingLabels = common_vendor.computed(
      () => platformStore.bindings.map((item) => {
        var _a;
        const mainLabel = (_a = item.authorDisplayName) == null ? void 0 : _a.trim();
        if (mainLabel) {
          return mainLabel;
        }
        if (item.softwareType) {
          return `${item.softwareType} (${item.softwareCode})`;
        }
        return item.softwareCode;
      })
    );
    const bindingIndex = common_vendor.computed(() => {
      if (!platformStore.selectedBinding) {
        return 0;
      }
      const index = platformStore.bindings.findIndex(
        (binding) => {
          var _a;
          return binding.bindingId === ((_a = platformStore.selectedBinding) == null ? void 0 : _a.bindingId);
        }
      );
      return index >= 0 ? index : 0;
    });
    const currentBindingLabel = common_vendor.computed(() => {
      if (!platformStore.bindings.length) {
        return "尚未绑定软件码";
      }
      if (!platformStore.selectedBinding) {
        return bindingLabels.value[bindingIndex.value] ?? "请选择软件码";
      }
      if (!bindingLabels.value.length) {
        const fallback = platformStore.selectedBinding;
        if (!fallback) {
          return "请选择软件码";
        }
        return fallback.authorDisplayName || fallback.softwareCode;
      }
      return bindingLabels.value[bindingIndex.value] ?? bindingLabels.value[0];
    });
    const bindingDisabled = common_vendor.computed(
      () => !platformStore.bindings.length || bindingLoginBusy.value || platformStore.loading.bindings
    );
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
      if (!isWeChatMiniProgram.value || !platformStore.isAuthenticated) {
        return;
      }
      await platformStore.fetchWeChatBinding("agent").catch(() => void 0);
    }
    common_vendor.watch(
      () => {
        var _a;
        return [((_a = agentWeChatBinding.value) == null ? void 0 : _a.openId) ?? null, wechatTemplateOptions.value.length];
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
          common_vendor.index.__f__("warn", "at pages/index/index.vue:643", "获取用户昵称失败，继续绑定", error);
        }
        await platformStore.bindWeChat("agent", code, nickname);
        await platformStore.fetchWeChatTemplates().catch(() => void 0);
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
        await platformStore.unbindWeChat("agent");
        common_vendor.index.showToast({ title: "已解绑", icon: "success" });
      } catch (error) {
        const message = (error == null ? void 0 : error.errMsg) || (error == null ? void 0 : error.message) || "解绑失败，请稍后重试";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    const stats = common_vendor.computed(() => dashboard.value.stats ?? []);
    const displayStats = common_vendor.computed(
      () => stats.value.map((item) => ({
        key: item.key,
        label: item.label,
        value: item.value,
        delta: 0,
        trend: "flat"
      }))
    );
    const trendPoints = common_vendor.computed(() => dashboard.value.trend ?? []);
    const trendTotal = common_vendor.computed(() => dashboard.value.trendTotal ?? 0);
    const trendMax = common_vendor.computed(() => Math.max(0, Math.ceil(dashboard.value.trendMax ?? 0)));
    const subTrendData = common_vendor.computed(() => dashboard.value.subAgentTrend ?? { categories: [], series: [], total: 0 });
    const announcements = common_vendor.computed(() => dashboard.value.announcements ?? []);
    const heatmap = common_vendor.computed(() => dashboard.value.usageHeatmap ?? []);
    const profileInitial = common_vendor.computed(() => {
      var _a, _b;
      const name = (_b = (_a = profile.value) == null ? void 0 : _a.name) == null ? void 0 : _b.trim();
      if (!name) {
        return "访";
      }
      return name.charAt(0).toUpperCase();
    });
    const loadingDashboard = common_vendor.computed(() => appStore.loading.dashboard);
    const salesLoading = common_vendor.computed(() => appStore.loading.sales);
    const isLoggingOut = common_vendor.computed(() => appStore.loading.logout);
    const salesResult = common_vendor.computed(() => dashboard.value.salesResult ?? { count: 0, cards: [] });
    const salesSettlements = common_vendor.computed(() => salesResult.value.settlements ?? []);
    const salesTotalAmount = common_vendor.computed(() => Number(salesResult.value.totalAmount ?? 0));
    const salesPage = common_vendor.ref(1);
    const paginatedSales = common_vendor.computed(() => {
      const items = salesResult.value.cards || [];
      const start = (salesPage.value - 1) * SALES_PAGE_SIZE;
      return items.slice(start, start + SALES_PAGE_SIZE);
    });
    const salesTotalPages = common_vendor.computed(() => {
      var _a;
      const total = ((_a = salesResult.value.cards) == null ? void 0 : _a.length) ?? 0;
      return Math.max(1, Math.ceil(total / SALES_PAGE_SIZE));
    });
    const salesSummary = common_vendor.computed(() => {
      const filters = dashboard.value.salesFilters;
      if (!filters) {
        return "";
      }
      const rangeStart = filters.startTime ?? "-";
      const rangeEnd = filters.endTime ?? "-";
      const agent = filters.agent ? filters.agent : "全部";
      return `时间：${rangeStart || "-"} ~ ${rangeEnd || "-"}，代理：${agent || "全部"}`;
    });
    const cardTypeOptions = common_vendor.computed(() => ["全部", ...cardTypes.value.map((item) => item.name)]);
    const statusOptions = ["全部", "启用", "禁用"];
    const agentOptions = common_vendor.computed(() => ["全部", ...agents.value.map((item) => item.username)]);
    const cardTypeIndex = common_vendor.ref(0);
    const statusIndex = common_vendor.ref(0);
    const agentIndex = common_vendor.ref(0);
    const salesForm = common_vendor.reactive({
      cardType: "",
      status: "",
      agent: "",
      includeDescendants: true,
      startTime: "",
      endTime: ""
    });
    const themeOptions = [
      { label: "暗夜", value: "dark" },
      { label: "明亮", value: "light" },
      { label: "卡通", value: "cartoon" }
    ];
    const themeLabels = themeOptions.map((item) => item.label);
    const themeIndex = common_vendor.ref(0);
    const baseQuickLinks = [
      { label: "软件绑定管理", description: "新增、修改与删除绑定软件码", path: "/pages/agent/bind" },
      { label: "结算设置", description: "为当前账号维护各卡密类型的结算价格", path: "/pages/agent/settlement" },
      { label: "卡密管理", description: "搜索、启用禁用与解绑操作", path: "/pages/cards/index" },
      { label: "代理管理", description: "封禁启用、加款与权限配置", path: "/pages/agents/index" },
      { label: "即时沟通", description: "与渠道实时协同巡检", path: "/pages/chat/index" },
      { label: "卡密验证", description: "批量验证与下载链接", path: "/pages/verify/index" },
      { label: "黑名单记录", description: "查询黑名单触发日志", path: "/pages/blacklist/logs/index" },
      { label: "黑名单机器码", description: "管理封禁机器码", path: "/pages/blacklist/machines/index" }
    ];
    const quickNavItems = common_vendor.computed(() => {
      const unread = chatUnreadCount.value;
      const hasBlacklistAlert = hasNewBlacklistLogs.value;
      return baseQuickLinks.map((item) => {
        const next = { ...item };
        if (item.path === "/pages/chat/index" && unread > 0) {
          next.badge = {
            type: "info",
            label: "新消息",
            value: unread > 99 ? "99+" : String(unread)
          };
        } else if (item.path === "/pages/agent/settlement" && settlementHasReminder.value) {
          next.badge = {
            type: "alert",
            label: "待结算",
            subtext: "请尽快处理"
          };
        } else if (item.path === "/pages/blacklist/logs/index" && hasBlacklistAlert) {
          next.badge = {
            type: "alert",
            label: "严重警告",
            subtext: "有人尝试破解"
          };
        }
        return next;
      });
    });
    const systemInfo = common_vendor.computed(() => {
      const info = systemStatus.value;
      if (!info)
        return null;
      return {
        machine: info.machineName || "未识别主机",
        os: info.osDescription || "未知系统",
        cpu: info.cpuLoadPercentage != null ? `${info.cpuLoadPercentage.toFixed(1)}%` : "--",
        memory: info.memoryUsagePercentage != null ? `${info.memoryUsagePercentage.toFixed(1)}%` : "--",
        uptime: formatDuration(info.uptimeSeconds),
        boot: info.bootTime ? utils_time.formatDateTime(info.bootTime) : "--",
        warnings: Array.isArray(info.warnings) ? info.warnings : []
      };
    });
    function formatPickerValue(date) {
      const pad = (value) => value.toString().padStart(2, "0");
      return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
    }
    function normalizePickerValue(value) {
      if (!value)
        return "";
      const trimmed = value.trim();
      if (!trimmed)
        return "";
      if (/^\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}$/.test(trimmed)) {
        return trimmed;
      }
      const parsed = new Date(trimmed.replace("T", " ").replace(/\//g, "-"));
      if (Number.isNaN(parsed.getTime())) {
        return "";
      }
      return formatPickerValue(parsed);
    }
    function formatDateTimeForApi(value) {
      return normalizePickerValue(value);
    }
    function formatCurrency(value) {
      const amount = Number(value ?? 0);
      if (!Number.isFinite(amount)) {
        return "0.00";
      }
      return amount.toFixed(2);
    }
    function applySalesFilters(filters) {
      const now = /* @__PURE__ */ new Date();
      const defaultStart = new Date(now.getTime() - 6 * 24 * 60 * 60 * 1e3);
      salesForm.cardType = (filters == null ? void 0 : filters.cardType) ?? "";
      salesForm.status = (filters == null ? void 0 : filters.status) ?? "";
      salesForm.agent = (filters == null ? void 0 : filters.agent) ?? "";
      salesForm.includeDescendants = (filters == null ? void 0 : filters.includeDescendants) ?? true;
      salesForm.startTime = normalizePickerValue(filters == null ? void 0 : filters.startTime) || formatPickerValue(defaultStart);
      salesForm.endTime = normalizePickerValue(filters == null ? void 0 : filters.endTime) || formatPickerValue(now);
      cardTypeIndex.value = Math.max(0, cardTypeOptions.value.indexOf(salesForm.cardType || "全部"));
      statusIndex.value = Math.max(0, statusOptions.indexOf(salesForm.status || "全部"));
      agentIndex.value = Math.max(0, agentOptions.value.indexOf(salesForm.agent || "全部"));
      salesPage.value = 1;
    }
    function copyValue(value) {
      const text = (value ?? "").toString().trim();
      if (!text) {
        common_vendor.index.showToast({ title: "无可复制内容", icon: "none" });
        return;
      }
      common_vendor.index.setClipboardData({
        data: text,
        success: () => {
          common_vendor.index.showToast({ title: "已复制", icon: "success", duration: 800 });
        },
        fail: () => {
          common_vendor.index.showToast({ title: "复制失败", icon: "none" });
        }
      });
    }
    function changeSalesPage(next) {
      if (next < 1 || next > salesTotalPages.value || next === salesPage.value) {
        return;
      }
      salesPage.value = next;
    }
    function onThemeChange(event) {
      const index = Number(event.detail.value);
      const option = themeOptions[index] ?? themeOptions[0];
      themeIndex.value = index;
      appStore.setTheme(option.value);
    }
    async function handleLogout() {
      if (isLoggingOut.value) {
        return;
      }
      try {
        await appStore.logout();
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/index/index.vue:934", "退出登录失败", error);
        common_vendor.index.showToast({ title: "退出失败，请重试", icon: "none" });
      }
    }
    function resetSalesForm() {
      applySalesFilters({ includeDescendants: true });
      appStore.clearSalesResult({
        includeDescendants: salesForm.includeDescendants,
        cardType: "",
        status: "",
        agent: "",
        startTime: salesForm.startTime,
        endTime: salesForm.endTime
      });
    }
    async function submitSalesQuery() {
      if (!selectedSoftware.value) {
        common_vendor.index.showToast({ title: "请先选择软件位", icon: "none" });
        return;
      }
      try {
        const startTime = formatDateTimeForApi(salesForm.startTime);
        const endTime = formatDateTimeForApi(salesForm.endTime);
        await appStore.runSalesQuery({
          cardType: salesForm.cardType || void 0,
          status: salesForm.status || void 0,
          agent: salesForm.agent || void 0,
          includeDescendants: salesForm.includeDescendants,
          startTime: startTime || void 0,
          endTime: endTime || void 0
        });
        common_vendor.index.showToast({ title: "查询完成", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/index/index.vue:970", "submitSalesQuery error", error);
        common_vendor.index.showToast({ title: "查询失败", icon: "none" });
      }
    }
    function onCardTypeChange(event) {
      const index = Number(event.detail.value);
      cardTypeIndex.value = index;
      salesForm.cardType = index === 0 ? "" : cardTypeOptions.value[index];
    }
    function onStatusChange(event) {
      const index = Number(event.detail.value);
      statusIndex.value = index;
      salesForm.status = index === 0 ? "" : statusOptions[index];
    }
    function onAgentChange(event) {
      const index = Number(event.detail.value);
      agentIndex.value = index;
      salesForm.agent = index === 0 ? "" : agentOptions.value[index];
    }
    async function loginWithBinding(binding, options) {
      if (!binding.authorAccount || !binding.authorPassword) {
        return;
      }
      bindingLoginBusy.value = true;
      try {
        await appStore.login(
          { username: binding.authorAccount, password: binding.authorPassword },
          { skipBootstrap: false }
        );
      } catch (error) {
        if (!(options == null ? void 0 : options.silent)) {
          const message = (error == null ? void 0 : error.message) && typeof error.message === "string" ? error.message : "同步作者登录失败";
          common_vendor.index.showToast({ title: message, icon: "none" });
        }
        throw error;
      } finally {
        bindingLoginBusy.value = false;
      }
    }
    function onBindingChange(event) {
      var _a;
      const index = Number(event.detail.value ?? bindingIndex.value);
      const binding = platformStore.bindings[index];
      if (!binding) {
        return;
      }
      if (((_a = platformStore.selectedBinding) == null ? void 0 : _a.bindingId) === binding.bindingId) {
        return;
      }
      platformStore.selectBinding(binding);
      loginWithBinding(binding, { silent: true }).then(() => {
        common_vendor.index.showToast({ title: `已切换 ${binding.softwareCode}`, icon: "success" });
      }).catch(() => {
      });
    }
    common_vendor.onMounted(async () => {
      if (!platformStore.isAuthenticated) {
        common_vendor.index.reLaunch({ url: "/pages/login/agent" });
        return;
      }
      await ensureWeChatBinding();
      if (isWeChatMiniProgram.value) {
        platformStore.fetchWeChatTemplates().catch(() => void 0);
      }
      if (!platformStore.bindings.length) {
        await platformStore.fetchAgentProfile();
      }
      if (!platformStore.selectedBinding && platformStore.bindings.length) {
        platformStore.selectBinding(platformStore.bindings[0]);
      }
      if (!platformStore.selectedBinding) {
        common_vendor.index.showModal({
          title: "尚未绑定作者",
          content: "请先绑定软件码后再访问控制台。",
          showCancel: false,
          success: () => {
            common_vendor.index.navigateTo({ url: "/pages/agent/bind" });
          }
        });
        return;
      }
      try {
        await loginWithBinding(platformStore.selectedBinding, { silent: true });
      } catch (error) {
        common_vendor.index.__f__("warn", "at pages/index/index.vue:1071", "自动同步作者凭证失败", error);
      }
      await appStore.ensureReady();
      await appStore.loadDashboard();
      await Promise.all([
        appStore.loadCardTypes(),
        appStore.loadAgents({ limit: 200 }),
        appStore.loadChatSessions().catch((error) => {
          common_vendor.index.__f__("warn", "at pages/index/index.vue:1080", "加载即时沟通会话失败", error);
        }),
        appStore.loadBlacklistLogs(100).catch((error) => {
          common_vendor.index.__f__("warn", "at pages/index/index.vue:1083", "加载黑名单日志失败", error);
        })
      ]);
      applySalesFilters(dashboard.value.salesFilters ?? { includeDescendants: true });
    });
    common_vendor.watch(
      () => selectedSoftware.value,
      (next, prev) => {
        if (!next || next === prev)
          return;
        appStore.loadDashboard();
        appStore.loadCardTypes(true);
        appStore.loadAgents({ limit: 200, page: 1, keyword: "" });
        appStore.loadChatSessions().catch((error) => {
          common_vendor.index.__f__("warn", "at pages/index/index.vue:1097", "切换软件时加载会话失败", error);
        });
        appStore.loadBlacklistLogs(100).catch((error) => {
          common_vendor.index.__f__("warn", "at pages/index/index.vue:1100", "切换软件时加载黑名单日志失败", error);
        });
        resetSalesForm();
      }
    );
    common_vendor.watch(
      () => dashboard.value.salesFilters,
      (filters) => {
        if (!filters) {
          resetSalesForm();
          return;
        }
        applySalesFilters(filters);
      },
      { immediate: true }
    );
    common_vendor.watch(
      () => dashboard.value.salesResult,
      () => {
        salesPage.value = 1;
      }
    );
    common_vendor.watch(
      theme,
      (next) => {
        const idx = themeOptions.findIndex((item) => item.value === next);
        themeIndex.value = idx >= 0 ? idx : 0;
      },
      { immediate: true }
    );
    function formatDuration(seconds) {
      if (seconds == null)
        return "—";
      const total = Number(seconds);
      if (!Number.isFinite(total) || total <= 0)
        return "—";
      const days = Math.floor(total / 86400);
      const hours = Math.floor(total % 86400 / 3600);
      const minutes = Math.floor(total % 3600 / 60);
      if (days > 0) {
        return `${days}天${hours}小时`;
      }
      if (hours > 0) {
        return `${hours}小时${minutes}分钟`;
      }
      const secs = Math.floor(total % 60);
      return `${minutes}分钟${secs}秒`;
    }
    function goTo(path) {
      common_vendor.index.navigateTo({ url: path });
    }
    return (_ctx, _cache) => {
      var _a, _b;
      return common_vendor.e({
        a: common_vendor.t(currentBindingLabel.value),
        b: common_vendor.n(bindingDisabled.value ? "picker-disabled" : ""),
        c: bindingLabels.value,
        d: bindingIndex.value,
        e: bindingDisabled.value,
        f: common_vendor.o(onBindingChange),
        g: common_vendor.t(common_vendor.unref(themeLabels)[themeIndex.value]),
        h: common_vendor.unref(themeLabels),
        i: themeIndex.value,
        j: common_vendor.o(onThemeChange),
        k: common_vendor.t(profileInitial.value),
        l: common_vendor.t(((_a = common_vendor.unref(profile)) == null ? void 0 : _a.name) ?? "未登录"),
        m: common_vendor.t(((_b = common_vendor.unref(profile)) == null ? void 0 : _b.role) ?? "访客"),
        n: common_vendor.t(isLoggingOut.value ? "正在退出..." : "退出登录"),
        o: isLoggingOut.value,
        p: common_vendor.o(handleLogout),
        q: isWeChatMiniProgram.value
      }, isWeChatMiniProgram.value ? common_vendor.e({
        r: common_vendor.t(wechatStatusText.value),
        s: wechatDisplayName.value
      }, wechatDisplayName.value ? {
        t: common_vendor.t(wechatDisplayName.value)
      } : {}, {
        v: common_vendor.t(wechatBindLabel.value),
        w: wechatBusy.value,
        x: common_vendor.o(handleWeChatBind),
        y: isWeChatBound.value
      }, isWeChatBound.value ? {
        z: wechatBusy.value,
        A: common_vendor.o(handleWeChatUnbind)
      } : {}, {
        B: showSubscriptionPanel.value
      }, showSubscriptionPanel.value ? {
        C: common_vendor.f(wechatTemplateOptions.value, (option, k0, i0) => {
          return {
            a: common_vendor.t(option.title),
            b: common_vendor.t(option.description),
            c: subscriptionState.value[option.key] !== false,
            d: common_vendor.o(($event) => onSubscriptionToggle(option.key, $event.detail.value), option.key),
            e: option.key
          };
        }),
        D: common_vendor.t(wechatSubscribeBusy.value ? "授权中..." : "保存并请求订阅"),
        E: wechatSubscribeBusy.value,
        F: common_vendor.o(($event) => applySubscriptionRequests())
      } : common_vendor.e({
        G: isWeChatBound.value
      }, isWeChatBound.value ? {} : {})) : {}, {
        H: common_vendor.f(quickNavItems.value, (item, k0, i0) => {
          return common_vendor.e({
            a: item.badge
          }, item.badge ? common_vendor.e({
            b: common_vendor.t(item.badge.label),
            c: item.badge.value
          }, item.badge.value ? {
            d: common_vendor.t(item.badge.value)
          } : {}, {
            e: item.badge.subtext
          }, item.badge.subtext ? {
            f: common_vendor.t(item.badge.subtext)
          } : {}, {
            g: common_vendor.n(`badge-${item.badge.type}`)
          }) : {}, {
            h: common_vendor.t(item.label),
            i: common_vendor.t(item.description),
            j: item.path,
            k: common_vendor.o(($event) => goTo(item.path), item.path)
          });
        }),
        I: systemInfo.value
      }, systemInfo.value ? common_vendor.e({
        J: common_vendor.t(systemInfo.value.machine),
        K: common_vendor.t(systemInfo.value.os),
        L: common_vendor.t(systemInfo.value.cpu),
        M: common_vendor.t(systemInfo.value.memory),
        N: common_vendor.t(systemInfo.value.uptime),
        O: common_vendor.t(systemInfo.value.boot),
        P: systemInfo.value.warnings.length
      }, systemInfo.value.warnings.length ? {
        Q: common_vendor.f(systemInfo.value.warnings, (warning, index, i0) => {
          return {
            a: common_vendor.t(warning),
            b: index
          };
        })
      } : {}) : {}, {
        R: common_vendor.f(displayStats.value, (item, k0, i0) => {
          return {
            a: item.key,
            b: "1cf27b2a-1-" + i0,
            c: common_vendor.p({
              title: item.label,
              value: item.value,
              delta: item.delta,
              trend: item.trend
            })
          };
        }),
        S: common_vendor.p({
          title: "过去 7 日总体态势",
          subtitle: "包含封禁",
          points: trendPoints.value,
          total: trendTotal.value,
          ["total-label"]: "最近7天总计",
          ["max-value"]: trendMax.value
        }),
        T: common_vendor.p({
          title: "最近子代理 7 天激活趋势",
          subtitle: "洞察各下级代理激活情况",
          categories: subTrendData.value.categories,
          series: subTrendData.value.series,
          total: subTrendData.value.total
        }),
        U: common_vendor.t(cardTypeOptions.value[cardTypeIndex.value]),
        V: cardTypeOptions.value,
        W: cardTypeIndex.value,
        X: common_vendor.o(onCardTypeChange),
        Y: common_vendor.t(statusOptions[statusIndex.value]),
        Z: statusOptions,
        aa: statusIndex.value,
        ab: common_vendor.o(onStatusChange),
        ac: common_vendor.t(agentOptions.value[agentIndex.value]),
        ad: agentOptions.value,
        ae: agentIndex.value,
        af: common_vendor.o(onAgentChange),
        ag: salesForm.includeDescendants,
        ah: common_vendor.o((e) => salesForm.includeDescendants = e.detail.value),
        ai: common_vendor.o(($event) => salesForm.startTime = $event),
        aj: common_vendor.p({
          placeholder: "选择开始时间",
          modelValue: salesForm.startTime
        }),
        ak: common_vendor.o(($event) => salesForm.endTime = $event),
        al: common_vendor.p({
          placeholder: "选择结束时间",
          modelValue: salesForm.endTime
        }),
        am: common_vendor.t(salesLoading.value ? "查询中..." : "查询"),
        an: salesLoading.value,
        ao: common_vendor.o(submitSalesQuery),
        ap: salesLoading.value,
        aq: common_vendor.o(resetSalesForm),
        ar: !salesLoading.value
      }, !salesLoading.value ? common_vendor.e({
        as: common_vendor.t(salesResult.value.count),
        at: common_vendor.t(formatCurrency(salesTotalAmount.value)),
        av: common_vendor.t(salesSummary.value),
        aw: salesSettlements.value.length
      }, salesSettlements.value.length ? {
        ax: common_vendor.f(salesSettlements.value, (item, k0, i0) => {
          return {
            a: common_vendor.t(item.cardType),
            b: common_vendor.t(item.count),
            c: common_vendor.t(formatCurrency(item.price)),
            d: common_vendor.t(formatCurrency(item.total)),
            e: item.cardType
          };
        })
      } : {}, {
        ay: !salesResult.value.cards.length
      }, !salesResult.value.cards.length ? {} : common_vendor.e({
        az: common_vendor.f(paginatedSales.value, (item, k0, i0) => {
          return {
            a: common_vendor.t(item.card),
            b: common_vendor.o(($event) => copyValue(item.card), `${item.card}-${item.activateTime}`),
            c: common_vendor.o(($event) => copyValue(item.card), `${item.card}-${item.activateTime}`),
            d: common_vendor.t(item.activateTime || "-"),
            e: common_vendor.o(($event) => copyValue(item.activateTime || "-"), `${item.card}-${item.activateTime}`),
            f: common_vendor.o(($event) => copyValue(item.activateTime || "-"), `${item.card}-${item.activateTime}`),
            g: `${item.card}-${item.activateTime}`
          };
        }),
        aA: salesTotalPages.value > 1
      }, salesTotalPages.value > 1 ? {
        aB: common_vendor.t(salesPage.value),
        aC: common_vendor.t(salesTotalPages.value),
        aD: salesPage.value <= 1 || salesLoading.value,
        aE: common_vendor.o(($event) => changeSalesPage(salesPage.value - 1)),
        aF: salesPage.value >= salesTotalPages.value || salesLoading.value,
        aG: common_vendor.o(($event) => changeSalesPage(salesPage.value + 1))
      } : {})) : {}, {
        aH: loadingDashboard.value
      }, loadingDashboard.value ? {} : !announcements.value.length ? {} : {
        aJ: common_vendor.f(announcements.value, (item, k0, i0) => {
          return {
            a: common_vendor.t(item.title),
            b: common_vendor.t(item.content),
            c: common_vendor.t(item.updatedAt),
            d: item.id
          };
        })
      }, {
        aI: !announcements.value.length,
        aK: loadingDashboard.value
      }, loadingDashboard.value ? {} : !heatmap.value.length ? {} : {
        aM: common_vendor.f(heatmap.value, (city, k0, i0) => {
          return {
            a: common_vendor.t(city.name),
            b: common_vendor.t(city.count),
            c: common_vendor.t(city.percentage),
            d: city.name
          };
        })
      }, {
        aL: !heatmap.value.length
      });
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-1cf27b2a"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/index/index.js.map
