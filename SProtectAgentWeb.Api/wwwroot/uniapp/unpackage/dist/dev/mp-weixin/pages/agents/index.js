"use strict";
const common_vendor = require("../../common/vendor.js");
const stores_app = require("../../stores/app.js");
const utils_time = require("../../utils/time.js");
if (!Math) {
  (SoftwarePicker + StatusTag + DataTable)();
}
const DataTable = () => "../../components/DataTable.js";
const StatusTag = () => "../../components/StatusTag.js";
const SoftwarePicker = () => "../../components/SoftwarePicker.js";
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "index",
  setup(__props) {
    const appStore = stores_app.useAppStore();
    const { agents, selectedSoftware, agentTotal, cardTypes } = common_vendor.storeToRefs(appStore);
    const agentFilters = appStore.agentFilters;
    const filtersForm = common_vendor.reactive({ keyword: "" });
    const statusOptions = [
      { label: "全部", value: "all" },
      { label: "启用", value: "enabled" },
      { label: "禁用", value: "disabled" }
    ];
    const statusIndex = common_vendor.ref(0);
    const pageSizeOptions = [20, 50, 100];
    const pageSizeIndex = common_vendor.ref(1);
    const loading = common_vendor.computed(() => appStore.loading.agents);
    const statusLabels = common_vendor.computed(() => statusOptions.map((item) => item.label));
    const pageSizeLabels = common_vendor.computed(() => pageSizeOptions.map((size) => `${size} 条/页`));
    const actionState = common_vendor.reactive({ username: "", type: "" });
    const creating = common_vendor.ref(false);
    const showCreateAgentModal = common_vendor.ref(false);
    const createAgentForm = common_vendor.reactive({
      username: "",
      password: "",
      balance: "",
      timeStock: "",
      parities: "100",
      remark: "",
      cardTypes: []
    });
    const cardTypeChoices = common_vendor.computed(() => cardTypes.value.map((item) => ({
      name: item.name,
      label: formatAgentCardTypeLabel(item)
    })));
    const rows = common_vendor.computed(() => {
      var _a;
      const statusValue = ((_a = statusOptions[statusIndex.value]) == null ? void 0 : _a.value) ?? "all";
      return agents.value.filter((item) => statusValue === "all" ? true : item.status === statusValue).map((item) => ({
        username: item.username,
        status: item.status,
        balance: item.balance.toFixed(2),
        timeStock: item.timeStock,
        parities: item.parities,
        totalParities: item.totalParities,
        remark: item.remark ?? "",
        cardTypes: item.cardTypes ?? [],
        cardTypesDisplay: item.cardTypes && item.cardTypes.length ? item.cardTypes.join("、") : "默认全部"
      }));
    });
    const summaryText = common_vendor.computed(() => {
      const total = agentTotal.value || 0;
      const filtered = rows.value.length;
      return filtered === total ? `共 ${total} 条记录` : `共 ${total} 条记录 · 当前筛选 ${filtered} 条`;
    });
    const columns = [
      { key: "username", label: "代理账号", style: "min-width:200rpx" },
      { key: "status", label: "状态", style: "min-width:160rpx" },
      { key: "balance", label: "余额(元)", style: "min-width:160rpx" },
      { key: "timeStock", label: "时间库存(小时)", style: "min-width:220rpx" },
      { key: "parities", label: "剩余授权", style: "min-width:180rpx" },
      { key: "totalParities", label: "累计授权", style: "min-width:200rpx" },
      { key: "cardTypesDisplay", label: "可售卡种", style: "min-width:240rpx" },
      { key: "remark", label: "备注信息", style: "min-width:240rpx" },
      { key: "operations", label: "操作", style: "min-width:360rpx" }
    ];
    const currentPage = common_vendor.computed(() => agentFilters.page ?? 1);
    const pageSize = common_vendor.computed(() => agentFilters.limit ?? pageSizeOptions[pageSizeIndex.value] ?? 50);
    const totalPages = common_vendor.computed(() => {
      const size = pageSize.value || 1;
      return Math.max(1, Math.ceil((agentTotal.value || 0) / size));
    });
    const statusText = {
      enabled: "启用",
      disabled: "禁用"
    };
    const statusMap = {
      enabled: "success",
      disabled: "warning"
    };
    function formatAgentCardTypeLabel(type) {
      const durationText = utils_time.formatDurationFromSeconds(type.duration);
      return durationText ? `${type.name}（${durationText}）` : type.name;
    }
    function resetCreateAgentForm() {
      createAgentForm.username = "";
      createAgentForm.password = "";
      createAgentForm.balance = "0";
      createAgentForm.timeStock = "0";
      createAgentForm.parities = "100";
      createAgentForm.remark = "";
      createAgentForm.cardTypes = [];
    }
    function closeCreateAgentModal() {
      if (creating.value)
        return;
      showCreateAgentModal.value = false;
      resetCreateAgentForm();
    }
    function onAgentCardTypesChange(event) {
      const values = Array.isArray(event.detail.value) ? event.detail.value : [];
      createAgentForm.cardTypes = Array.from(new Set(values));
    }
    function isActionBusy(username, type) {
      return actionState.username === username && actionState.type === type;
    }
    function setActionBusy(username, type) {
      actionState.username = username;
      actionState.type = type;
    }
    function clearActionBusy() {
      actionState.username = "";
      actionState.type = "";
    }
    async function promptInput(options) {
      const { title, placeholder = "", defaultValue = "", optional = false } = options;
      const result = await new Promise((resolve) => {
        common_vendor.index.showModal({
          title,
          content: defaultValue,
          editable: true,
          placeholderText: placeholder,
          confirmText: "确定",
          cancelText: optional ? "跳过" : "取消",
          success: resolve,
          fail: () => resolve({ confirm: false, cancel: true })
        });
      });
      if (!result.confirm) {
        return optional ? "" : null;
      }
      return (result.content ?? "").trim();
    }
    function syncFiltersFromStore() {
      filtersForm.keyword = agentFilters.keyword ?? "";
      const sizeIdx = pageSizeOptions.findIndex((value) => value === (agentFilters.limit ?? 50));
      pageSizeIndex.value = sizeIdx >= 0 ? sizeIdx : 1;
    }
    async function searchAgents() {
      try {
        await appStore.loadAgents({
          keyword: filtersForm.keyword.trim(),
          page: 1,
          limit: pageSizeOptions[pageSizeIndex.value] ?? agentFilters.limit
        });
        common_vendor.index.showToast({ title: "查询完成", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:397", "searchAgents error", error);
        common_vendor.index.showToast({ title: "查询失败", icon: "none" });
      }
    }
    async function resetFilters() {
      filtersForm.keyword = "";
      statusIndex.value = 0;
      pageSizeIndex.value = pageSizeOptions.indexOf(50) >= 0 ? pageSizeOptions.indexOf(50) : 0;
      await searchAgents();
    }
    async function refreshList() {
      try {
        await appStore.loadAgents({
          page: agentFilters.page,
          limit: agentFilters.limit,
          keyword: agentFilters.keyword
        });
        common_vendor.index.showToast({ title: "已刷新", icon: "none" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:418", "refreshList error", error);
      }
    }
    async function changePage(page) {
      if (page < 1 || page > totalPages.value)
        return;
      try {
        await appStore.loadAgents({ page });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:427", "changePage error", error);
      }
    }
    async function onPageSizeChange(event) {
      const index = Number(event.detail.value);
      pageSizeIndex.value = index;
      const size = pageSizeOptions[index] ?? 50;
      try {
        await appStore.loadAgents({ limit: size, page: 1, keyword: filtersForm.keyword.trim() });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:438", "onPageSizeChange error", error);
      }
    }
    function onStatusChange(event) {
      statusIndex.value = Number(event.detail.value);
    }
    function confirmAction(message) {
      return new Promise((resolve) => {
        common_vendor.index.showModal({
          title: "确认操作",
          content: message,
          confirmText: "确定",
          cancelText: "取消",
          success: (result) => resolve(!!result.confirm),
          fail: () => resolve(false)
        });
      });
    }
    function copyValue(value) {
      const text = (value ?? "").toString().trim();
      if (!text) {
        common_vendor.index.showToast({ title: "无可复制内容", icon: "none" });
        return;
      }
      common_vendor.index.setClipboardData({
        data: text,
        success: () => common_vendor.index.showToast({ title: "已复制", icon: "success", duration: 800 })
      });
    }
    async function toggleStatus(row, enable) {
      const label = enable ? "启用" : "禁用";
      const confirmed = await confirmAction(`确认${label}代理：${row.username}？`);
      if (!confirmed)
        return;
      setActionBusy(row.username, enable ? "enable" : "disable");
      try {
        const message = await appStore.toggleAgentStatus({ usernames: [row.username], enable });
        common_vendor.index.showToast({ title: message || `${label}成功`, icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:480", "toggleStatus error", error);
        common_vendor.index.showToast({ title: `${label}失败`, icon: "none" });
      } finally {
        clearActionBusy();
      }
    }
    async function openAdjust(row) {
      const balanceInput = await promptInput({ title: "加款金额 (元)", placeholder: "可填写正负数", defaultValue: "0" });
      if (balanceInput === null)
        return;
      const timeInput = await promptInput({ title: "加时长 (小时)", placeholder: "可填写正负数", defaultValue: "0" });
      if (timeInput === null)
        return;
      const balance = parseFloat(balanceInput) || 0;
      const timeStock = parseFloat(timeInput) || 0;
      if (!balance && !timeStock) {
        common_vendor.index.showToast({ title: "请至少输入一项调整数值", icon: "none" });
        return;
      }
      setActionBusy(row.username, "adjust");
      try {
        const message = await appStore.adjustAgentBalance({ username: row.username, balance, timeStock });
        common_vendor.index.showToast({ title: message || "调整成功", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:503", "openAdjust error", error);
        common_vendor.index.showToast({ title: "调整失败", icon: "none" });
      } finally {
        clearActionBusy();
      }
    }
    async function openRemark(row) {
      const remarkInput = await promptInput({ title: "修改备注", placeholder: "请输入备注内容", defaultValue: row.remark, optional: true });
      if (remarkInput === null)
        return;
      setActionBusy(row.username, "remark");
      try {
        const message = await appStore.updateAgentRemark({ username: row.username, remark: remarkInput });
        common_vendor.index.showToast({ title: message || "备注已更新", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:518", "openRemark error", error);
        common_vendor.index.showToast({ title: "备注更新失败", icon: "none" });
      } finally {
        clearActionBusy();
      }
    }
    async function openPassword(row) {
      const passwordInput = await promptInput({ title: "重置密码", placeholder: "请输入新的登录密码" });
      if (passwordInput === null)
        return;
      if (!passwordInput) {
        common_vendor.index.showToast({ title: "密码不能为空", icon: "none" });
        return;
      }
      setActionBusy(row.username, "password");
      try {
        const message = await appStore.updateAgentPassword({ username: row.username, password: passwordInput });
        common_vendor.index.showToast({ title: message || "密码已重置", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:537", "openPassword error", error);
        common_vendor.index.showToast({ title: "密码重置失败", icon: "none" });
      } finally {
        clearActionBusy();
      }
    }
    async function openAssignCardTypes(row) {
      try {
        if (!cardTypes.value.length) {
          await appStore.loadCardTypes();
        }
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:550", "loadCardTypes error", error);
      }
      const candidates = cardTypes.value.map((item) => item.name);
      const hint = candidates.length ? `可选：${candidates.slice(0, 6).join("、")}` : "请输入卡种名称，逗号分隔";
      const input = await promptInput({
        title: "配置授权卡种",
        placeholder: hint,
        defaultValue: row.cardTypes.join(","),
        optional: true
      });
      if (input === null)
        return;
      const cardTypeList = input ? input.split(/[，,\s]+/).map((item) => item.trim()).filter((item) => item.length > 0) : [];
      setActionBusy(row.username, "cardTypes");
      try {
        const message = await appStore.assignAgentCardTypes({ username: row.username, cardTypes: cardTypeList });
        common_vendor.index.showToast({ title: message || "卡种配置成功", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:572", "openAssignCardTypes error", error);
        common_vendor.index.showToast({ title: "卡种配置失败", icon: "none" });
      } finally {
        clearActionBusy();
      }
    }
    async function deleteAgent(row) {
      const confirmed = await confirmAction(`确认删除代理：${row.username}？此操作不可恢复。`);
      if (!confirmed)
        return;
      setActionBusy(row.username, "delete");
      try {
        const message = await appStore.deleteAgents([row.username]);
        common_vendor.index.showToast({ title: message || "已删除", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:587", "deleteAgent error", error);
        common_vendor.index.showToast({ title: "删除失败", icon: "none" });
      } finally {
        clearActionBusy();
      }
    }
    async function openCreateAgent() {
      await appStore.ensureReady();
      if (!selectedSoftware.value) {
        common_vendor.index.showToast({ title: "请先选择软件位", icon: "none" });
        return;
      }
      try {
        if (!cardTypes.value.length) {
          await appStore.loadCardTypes();
        }
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:606", "loadCardTypes error", error);
      }
      resetCreateAgentForm();
      showCreateAgentModal.value = true;
    }
    async function submitCreateAgent() {
      if (creating.value)
        return;
      const username = createAgentForm.username.trim();
      if (!username) {
        common_vendor.index.showToast({ title: "账号不能为空", icon: "none" });
        return;
      }
      const password = createAgentForm.password.trim();
      if (!password) {
        common_vendor.index.showToast({ title: "密码不能为空", icon: "none" });
        return;
      }
      const balance = parseFloat(createAgentForm.balance || "0") || 0;
      const timeStock = parseFloat(createAgentForm.timeStock || "0") || 0;
      const parities = Math.max(0, parseFloat(createAgentForm.parities || "100") || 100);
      const remarks = createAgentForm.remark.trim();
      const cardTypeList = createAgentForm.cardTypes.slice();
      creating.value = true;
      try {
        const message = await appStore.createAgent({
          username,
          password,
          balance,
          timeStock,
          parities,
          totalParities: parities,
          remarks,
          cardTypes: cardTypeList
        });
        common_vendor.index.showToast({ title: message || "创建成功", icon: "success" });
        showCreateAgentModal.value = false;
        resetCreateAgentForm();
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:650", "submitCreateAgent error", error);
        common_vendor.index.showToast({ title: "创建失败", icon: "none" });
      } finally {
        creating.value = false;
      }
    }
    common_vendor.watch(
      () => [agentFilters.keyword, agentFilters.limit],
      () => {
        syncFiltersFromStore();
      },
      { immediate: true }
    );
    common_vendor.watch(
      () => cardTypeChoices.value,
      (next) => {
        const available = new Set(next.map((item) => item.name));
        createAgentForm.cardTypes = createAgentForm.cardTypes.filter((name) => available.has(name));
      },
      { immediate: true }
    );
    common_vendor.onMounted(async () => {
      await appStore.ensureReady();
      try {
        await appStore.loadAgents();
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agents/index.vue:679", "agents/onMounted error", error);
      }
      syncFiltersFromStore();
    });
    common_vendor.watch(
      () => selectedSoftware.value,
      async (next, prev) => {
        if (!next || next === prev)
          return;
        filtersForm.keyword = "";
        statusIndex.value = 0;
        pageSizeIndex.value = pageSizeOptions.indexOf(50) >= 0 ? pageSizeOptions.indexOf(50) : 0;
        try {
          await appStore.loadAgents({ page: 1, keyword: "", limit: pageSizeOptions[pageSizeIndex.value] });
        } catch (error) {
          common_vendor.index.__f__("error", "at pages/agents/index.vue:694", "selectedSoftware change error", error);
        }
        syncFiltersFromStore();
      }
    );
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: loading.value,
        b: common_vendor.o(refreshList),
        c: common_vendor.t(creating.value ? "创建中..." : "添加代理"),
        d: creating.value,
        e: common_vendor.o(openCreateAgent),
        f: common_vendor.o(searchAgents),
        g: filtersForm.keyword,
        h: common_vendor.o(($event) => filtersForm.keyword = $event.detail.value),
        i: common_vendor.t(statusLabels.value[statusIndex.value]),
        j: statusLabels.value,
        k: statusIndex.value,
        l: common_vendor.o(onStatusChange),
        m: common_vendor.t(pageSizeLabels.value[pageSizeIndex.value]),
        n: pageSizeLabels.value,
        o: pageSizeIndex.value,
        p: common_vendor.o(onPageSizeChange),
        q: common_vendor.t(loading.value ? "查询中..." : "搜索"),
        r: loading.value,
        s: common_vendor.o(searchAgents),
        t: loading.value,
        v: common_vendor.o(resetFilters),
        w: common_vendor.w(({
          row
        }, s0, i0) => {
          return {
            a: common_vendor.t(row.username),
            b: common_vendor.o(($event) => copyValue(row.username)),
            c: common_vendor.o(($event) => copyValue(row.username)),
            d: "91d40960-2-" + i0 + ",91d40960-1",
            e: common_vendor.p({
              status: statusMap[row.status],
              label: statusText[row.status]
            }),
            f: i0,
            g: s0
          };
        }, {
          name: "username",
          path: "w",
          vueId: "91d40960-1"
        }),
        x: common_vendor.w(({
          row
        }, s0, i0) => {
          return {
            a: "91d40960-3-" + i0 + ",91d40960-1",
            b: common_vendor.p({
              status: statusMap[row.status],
              label: statusText[row.status]
            }),
            c: i0,
            d: s0
          };
        }, {
          name: "status",
          path: "x",
          vueId: "91d40960-1"
        }),
        y: common_vendor.w(({
          row
        }, s0, i0) => {
          return common_vendor.e({
            a: row.cardTypes.length
          }, row.cardTypes.length ? {
            b: common_vendor.f(row.cardTypes, (item, k1, i1) => {
              return {
                a: common_vendor.t(item),
                b: item
              };
            })
          } : {}, {
            c: i0,
            d: s0
          });
        }, {
          name: "cardTypesDisplay",
          path: "y",
          vueId: "91d40960-1"
        }),
        z: common_vendor.w(({
          row
        }, s0, i0) => {
          return common_vendor.e({
            a: row.remark
          }, row.remark ? {
            b: common_vendor.t(row.remark)
          } : {}, {
            c: i0,
            d: s0
          });
        }, {
          name: "remark",
          path: "z",
          vueId: "91d40960-1"
        }),
        A: common_vendor.w(({
          row
        }, s0, i0) => {
          return {
            a: common_vendor.t(isActionBusy(row.username, "enable") ? "处理中..." : "启用"),
            b: loading.value || isActionBusy(row.username, "enable"),
            c: common_vendor.o(($event) => toggleStatus(row, true)),
            d: common_vendor.t(isActionBusy(row.username, "disable") ? "处理中..." : "禁用"),
            e: loading.value || isActionBusy(row.username, "disable"),
            f: common_vendor.o(($event) => toggleStatus(row, false)),
            g: common_vendor.t(isActionBusy(row.username, "adjust") ? "处理中..." : "加款/加时"),
            h: loading.value || isActionBusy(row.username, "adjust"),
            i: common_vendor.o(($event) => openAdjust(row)),
            j: common_vendor.t(isActionBusy(row.username, "remark") ? "处理中..." : "备注"),
            k: loading.value || isActionBusy(row.username, "remark"),
            l: common_vendor.o(($event) => openRemark(row)),
            m: common_vendor.t(isActionBusy(row.username, "password") ? "处理中..." : "重置密码"),
            n: loading.value || isActionBusy(row.username, "password"),
            o: common_vendor.o(($event) => openPassword(row)),
            p: common_vendor.t(isActionBusy(row.username, "cardTypes") ? "处理中..." : "卡种配置"),
            q: loading.value || isActionBusy(row.username, "cardTypes"),
            r: common_vendor.o(($event) => openAssignCardTypes(row)),
            s: common_vendor.t(isActionBusy(row.username, "delete") ? "处理中..." : "删除"),
            t: loading.value || isActionBusy(row.username, "delete"),
            v: common_vendor.o(($event) => deleteAgent(row)),
            w: i0,
            x: s0
          };
        }, {
          name: "operations",
          path: "A",
          vueId: "91d40960-1"
        }),
        B: common_vendor.p({
          title: "子代理列表",
          subtitle: summaryText.value,
          columns,
          rows: rows.value,
          loading: loading.value,
          layout: "stack",
          ["primary-column"]: "username",
          ["operations-column"]: "operations"
        }),
        C: totalPages.value > 1 || common_vendor.unref(agentTotal)
      }, totalPages.value > 1 || common_vendor.unref(agentTotal) ? {
        D: common_vendor.t(currentPage.value),
        E: common_vendor.t(totalPages.value),
        F: common_vendor.t(common_vendor.unref(agentTotal)),
        G: currentPage.value <= 1 || loading.value,
        H: common_vendor.o(($event) => changePage(currentPage.value - 1)),
        I: currentPage.value >= totalPages.value || loading.value,
        J: common_vendor.o(($event) => changePage(currentPage.value + 1))
      } : {}, {
        K: showCreateAgentModal.value
      }, showCreateAgentModal.value ? common_vendor.e({
        L: creating.value,
        M: common_vendor.o(closeCreateAgentModal),
        N: createAgentForm.username,
        O: common_vendor.o(($event) => createAgentForm.username = $event.detail.value),
        P: createAgentForm.password,
        Q: common_vendor.o(($event) => createAgentForm.password = $event.detail.value),
        R: createAgentForm.balance,
        S: common_vendor.o(($event) => createAgentForm.balance = $event.detail.value),
        T: createAgentForm.timeStock,
        U: common_vendor.o(($event) => createAgentForm.timeStock = $event.detail.value),
        V: createAgentForm.parities,
        W: common_vendor.o(($event) => createAgentForm.parities = $event.detail.value),
        X: createAgentForm.remark,
        Y: common_vendor.o(($event) => createAgentForm.remark = $event.detail.value),
        Z: !cardTypeChoices.value.length
      }, !cardTypeChoices.value.length ? {} : {
        aa: common_vendor.f(cardTypeChoices.value, (item, k0, i0) => {
          return {
            a: item.name,
            b: common_vendor.t(item.label),
            c: item.name
          };
        }),
        ab: createAgentForm.cardTypes,
        ac: common_vendor.o(onAgentCardTypesChange)
      }, {
        ad: creating.value,
        ae: common_vendor.o(closeCreateAgentModal),
        af: common_vendor.t(creating.value ? "创建中..." : "确认创建"),
        ag: creating.value,
        ah: common_vendor.o(submitCreateAgent),
        ai: common_vendor.o(() => {
        }),
        aj: common_vendor.o(closeCreateAgentModal)
      }) : {});
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-91d40960"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/agents/index.js.map
