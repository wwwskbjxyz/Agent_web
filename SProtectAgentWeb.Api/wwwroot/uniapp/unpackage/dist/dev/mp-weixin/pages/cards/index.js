"use strict";
const common_vendor = require("../../common/vendor.js");
const stores_app = require("../../stores/app.js");
const utils_download = require("../../utils/download.js");
const utils_time = require("../../utils/time.js");
if (!Math) {
  (SoftwarePicker + DateTimePicker + StatusTag + DataTable)();
}
const DataTable = () => "../../components/DataTable.js";
const StatusTag = () => "../../components/StatusTag.js";
const SoftwarePicker = () => "../../components/SoftwarePicker.js";
const DateTimePicker = () => "../../components/DateTimePicker.js";
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "index",
  setup(__props) {
    const appStore = stores_app.useAppStore();
    const { cardKeys, cardTypes, cardTotal, selectedSoftware, agents } = common_vendor.storeToRefs(appStore);
    const cardFilters = appStore.cardFilters;
    const filtersForm = common_vendor.reactive({
      keyword: "",
      cardType: "",
      status: "",
      agent: "",
      includeDescendants: true,
      machineCode: "",
      ip: "",
      startTime: "",
      endTime: ""
    });
    const searchTypeOptions = [
      { label: "智能匹配", value: 0 },
      { label: "备注 / 机器码", value: 1 },
      { label: "IP 地址", value: 2 },
      { label: "按卡种", value: 3 }
    ];
    const statusOptions = [
      { label: "全部", value: "" },
      { label: "启用", value: "enabled" },
      { label: "禁用", value: "disabled" },
      { label: "未知", value: "unknown" }
    ];
    const pageSizeOptions = [20, 50, 100, 200];
    const cardTypeIndex = common_vendor.ref(0);
    const statusIndex = common_vendor.ref(0);
    const agentIndex = common_vendor.ref(0);
    const searchTypeIndex = common_vendor.ref(0);
    const pageSizeIndex = common_vendor.ref(1);
    const showGeneratedModal = common_vendor.ref(false);
    const showCreateCardModal = common_vendor.ref(false);
    const generatedCards = common_vendor.ref([]);
    const generatedCardType = common_vendor.ref("");
    const creatingCards = common_vendor.ref(false);
    const createCardForm = common_vendor.reactive({
      cardTypeIndex: 0,
      quantity: "1",
      remarks: "",
      customPrefix: ""
    });
    const loading = common_vendor.computed(() => appStore.loading.cards);
    const cardTypeOptions = common_vendor.computed(() => ["全部", ...cardTypes.value.map((item) => item.name)]);
    const agentOptions = common_vendor.computed(() => ["全部", ...agents.value.map((item) => item.username)]);
    const statusLabels = common_vendor.computed(() => statusOptions.map((item) => item.label));
    const searchTypeLabels = common_vendor.computed(() => searchTypeOptions.map((item) => item.label));
    const pageSizeLabels = common_vendor.computed(() => pageSizeOptions.map((item) => `${item} 条/页`));
    const cardTypePickerLabels = common_vendor.computed(() => cardTypes.value.map((item) => formatCardTypeLabel(item)));
    const rows = common_vendor.computed(
      () => cardKeys.value.map((item) => ({
        key: item.key,
        fullKey: item.key,
        cardType: item.cardType,
        status: item.status,
        statusText: item.statusText,
        owner: item.owner,
        createdAt: item.createdAt,
        activatedAt: item.activatedAt,
        expireAt: item.expireAt,
        remark: item.remark ?? "",
        ip: item.ip ?? "--",
        machineCodes: item.machineCodes || []
      }))
    );
    const currentPage = common_vendor.computed(() => cardFilters.page ?? 1);
    const pageSize = common_vendor.computed(() => cardFilters.limit ?? pageSizeOptions[pageSizeIndex.value] ?? 50);
    const totalPages = common_vendor.computed(() => {
      const size = pageSize.value || 1;
      return Math.max(1, Math.ceil((cardTotal.value || 0) / size));
    });
    const summaryText = common_vendor.computed(() => `共 ${cardTotal.value} 条记录`);
    const statusText = {
      enabled: "启用",
      disabled: "禁用",
      unknown: "未知"
    };
    const statusMap = {
      enabled: "success",
      disabled: "warning",
      unknown: "info"
    };
    const columns = [
      { key: "key", label: "卡密编号", style: "min-width:260rpx" },
      { key: "status", label: "状态", style: "min-width:160rpx" },
      { key: "cardType", label: "卡密类型", style: "min-width:200rpx" },
      { key: "owner", label: "归属代理", style: "min-width:180rpx" },
      { key: "ip", label: "最近 IP", style: "min-width:180rpx" },
      { key: "machineCodes", label: "绑定机器码", style: "min-width:260rpx" },
      { key: "createdAt", label: "创建时间", style: "min-width:220rpx" },
      { key: "activatedAt", label: "最近激活", style: "min-width:220rpx" },
      { key: "expireAt", label: "到期时间", style: "min-width:220rpx" },
      { key: "remark", label: "备注信息", style: "min-width:220rpx" },
      { key: "fullKey", label: "完整卡密", style: "min-width:280rpx" },
      { key: "operations", label: "操作", style: "min-width:320rpx" }
    ];
    const actionState = common_vendor.reactive({ key: "", action: "" });
    const disableDescendants = common_vendor.computed(() => !!filtersForm.agent);
    function formatCardTypeLabel(type) {
      const durationText = utils_time.formatDurationFromSeconds(type.duration);
      return durationText ? `${type.name}（${durationText}）` : type.name;
    }
    function isActionBusy(key, action) {
      return actionState.key === key && actionState.action === action;
    }
    function setActionBusy(key, action) {
      actionState.key = key;
      actionState.action = action;
    }
    function clearActionBusy() {
      actionState.key = "";
      actionState.action = "";
    }
    function openGeneratedModal(cards, typeName) {
      generatedCards.value = cards;
      generatedCardType.value = typeName;
      showGeneratedModal.value = true;
    }
    function closeGeneratedModal() {
      showGeneratedModal.value = false;
    }
    function closeCreateCardModal() {
      if (creatingCards.value)
        return;
      showCreateCardModal.value = false;
    }
    function onCreateCardTypeChange(event) {
      createCardForm.cardTypeIndex = Number(event.detail.value) || 0;
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
    function copyGeneratedCards() {
      if (!generatedCards.value.length) {
        common_vendor.index.showToast({ title: "无可复制内容", icon: "none" });
        return;
      }
      copyValue(generatedCards.value.join("\n"));
    }
    function exportGeneratedCards() {
      if (!generatedCards.value.length) {
        common_vendor.index.showToast({ title: "暂无卡密", icon: "none" });
        return;
      }
      const filename = `${generatedCardType.value || "cards"}-${Date.now()}.txt`;
      utils_download.downloadTextFile(filename, generatedCards.value.join("\n"));
      common_vendor.index.showToast({ title: "已导出", icon: "success" });
    }
    function formatPickerValue(date) {
      const pad = (input) => input.toString().padStart(2, "0");
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
    function syncFormFromFilters() {
      const keywordSource = cardFilters.keyword || (Array.isArray(cardFilters.keywords) ? cardFilters.keywords.join(" ") : "");
      filtersForm.keyword = keywordSource;
      filtersForm.cardType = cardFilters.cardType || "";
      filtersForm.status = cardFilters.status || "";
      filtersForm.agent = cardFilters.agent || "";
      filtersForm.includeDescendants = cardFilters.agent ? false : cardFilters.includeDescendants ?? true;
      filtersForm.machineCode = cardFilters.machineCode || "";
      filtersForm.ip = cardFilters.ip || "";
      filtersForm.startTime = normalizePickerValue(cardFilters.startTime);
      filtersForm.endTime = normalizePickerValue(cardFilters.endTime);
      const typeName = filtersForm.cardType || "全部";
      const typeIndex = cardTypeOptions.value.indexOf(typeName);
      cardTypeIndex.value = typeIndex >= 0 ? typeIndex : 0;
      const statusIdx = statusOptions.findIndex((item) => item.value === (filtersForm.status || ""));
      statusIndex.value = statusIdx >= 0 ? statusIdx : 0;
      const agentName = filtersForm.agent || "全部";
      const agentIdx = agentOptions.value.indexOf(agentName);
      agentIndex.value = agentIdx >= 0 ? agentIdx : 0;
      const searchIdx = searchTypeOptions.findIndex((item) => item.value === (typeof cardFilters.searchType === "number" ? cardFilters.searchType : 0));
      searchTypeIndex.value = searchIdx >= 0 ? searchIdx : 0;
      const sizeIdx = pageSizeOptions.findIndex((value) => value === (cardFilters.limit ?? 50));
      pageSizeIndex.value = sizeIdx >= 0 ? sizeIdx : 1;
    }
    function onCardTypeChange(event) {
      const index = Number(event.detail.value);
      cardTypeIndex.value = index;
      filtersForm.cardType = index === 0 ? "" : cardTypeOptions.value[index] || "";
    }
    function onStatusChange(event) {
      var _a;
      const index = Number(event.detail.value);
      statusIndex.value = index;
      filtersForm.status = ((_a = statusOptions[index]) == null ? void 0 : _a.value) ?? "";
    }
    function onAgentChange(event) {
      const index = Number(event.detail.value);
      agentIndex.value = index;
      filtersForm.agent = agentOptions.value[index] === "全部" ? "" : agentOptions.value[index] || "";
      if (filtersForm.agent) {
        filtersForm.includeDescendants = false;
      } else {
        filtersForm.includeDescendants = true;
      }
    }
    function onSearchTypeChange(event) {
      const index = Number(event.detail.value);
      searchTypeIndex.value = index;
    }
    function onDescendantChange(event) {
      if (disableDescendants.value) {
        filtersForm.includeDescendants = false;
        return;
      }
      filtersForm.includeDescendants = event.detail.value;
    }
    async function onPageSizeChange(event) {
      const index = Number(event.detail.value);
      pageSizeIndex.value = index;
      const size = pageSizeOptions[index] ?? 50;
      try {
        await appStore.loadCardKeys({ limit: size, page: 1 });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/cards/index.vue:554", "onPageSizeChange error", error);
      }
    }
    async function submitSearch() {
      var _a;
      if (!selectedSoftware.value) {
        common_vendor.index.showToast({ title: "请先选择软件位", icon: "none" });
        return;
      }
      const searchType = ((_a = searchTypeOptions[searchTypeIndex.value]) == null ? void 0 : _a.value) ?? 0;
      const startTime = normalizePickerValue(filtersForm.startTime);
      const endTime = normalizePickerValue(filtersForm.endTime);
      const payload = {
        keyword: filtersForm.keyword.trim(),
        cardType: filtersForm.cardType,
        status: filtersForm.status || "",
        agent: filtersForm.agent,
        includeDescendants: filtersForm.agent ? false : filtersForm.includeDescendants,
        machineCode: filtersForm.machineCode.trim(),
        ip: filtersForm.ip.trim(),
        startTime: startTime || void 0,
        endTime: endTime || void 0,
        searchType,
        page: 1,
        limit: pageSizeOptions[pageSizeIndex.value] ?? cardFilters.limit
      };
      try {
        await appStore.loadCardKeys(payload);
        common_vendor.index.showToast({ title: "查询完成", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/cards/index.vue:587", "submitSearch error", error);
        common_vendor.index.showToast({ title: "查询失败", icon: "none" });
      }
    }
    async function resetFilters() {
      filtersForm.keyword = "";
      filtersForm.cardType = "";
      filtersForm.status = "";
      filtersForm.agent = "";
      filtersForm.includeDescendants = true;
      filtersForm.machineCode = "";
      filtersForm.ip = "";
      filtersForm.startTime = "";
      filtersForm.endTime = "";
      cardTypeIndex.value = 0;
      statusIndex.value = 0;
      agentIndex.value = 0;
      searchTypeIndex.value = 0;
      pageSizeIndex.value = pageSizeOptions.indexOf(50) >= 0 ? pageSizeOptions.indexOf(50) : 0;
      await submitSearch();
    }
    async function refreshList() {
      try {
        await appStore.loadCardKeys({ page: cardFilters.page, limit: cardFilters.limit });
        common_vendor.index.showToast({ title: "已刷新", icon: "none" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/cards/index.vue:615", "refreshList error", error);
      }
    }
    async function changePage(page) {
      if (page < 1 || page > totalPages.value)
        return;
      try {
        await appStore.loadCardKeys({ page });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/cards/index.vue:624", "changePage error", error);
      }
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
    async function handleStatusAction(row, action) {
      if (!(row == null ? void 0 : row.key))
        return;
      const labels = {
        enable: "启用",
        disable: "禁用",
        unban: "解除封禁"
      };
      const confirmed = await confirmAction(`确认${labels[action]}卡密：${row.key}？`);
      if (!confirmed) {
        return;
      }
      setActionBusy(row.key, action);
      try {
        const message = await appStore.updateCardStatus({ cardKey: row.key, action });
        common_vendor.index.showToast({ title: message || `${labels[action]}成功`, icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/cards/index.vue:657", "handleStatusAction error", error);
        common_vendor.index.showToast({ title: `${labels[action]}失败`, icon: "none" });
      } finally {
        clearActionBusy();
      }
    }
    async function handleUnbind(row) {
      if (!(row == null ? void 0 : row.key))
        return;
      const confirmed = await confirmAction(`确认解绑卡密：${row.key}？`);
      if (!confirmed) {
        return;
      }
      setActionBusy(row.key, "unbind");
      try {
        const message = await appStore.unbindCard(row.key);
        common_vendor.index.showToast({ title: message || "解绑成功", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/cards/index.vue:675", "handleUnbind error", error);
        common_vendor.index.showToast({ title: "解绑失败", icon: "none" });
      } finally {
        clearActionBusy();
      }
    }
    async function createCard() {
      await appStore.ensureReady();
      if (!selectedSoftware.value) {
        common_vendor.index.showToast({ title: "请先选择软件位", icon: "none" });
        return;
      }
      const types = cardTypes.value.length ? cardTypes.value : await appStore.loadCardTypes(true);
      if (!types || !types.length) {
        common_vendor.index.showToast({ title: "未获取到卡密类型", icon: "none" });
        return;
      }
      createCardForm.cardTypeIndex = Math.min(createCardForm.cardTypeIndex, Math.max(0, types.length - 1));
      createCardForm.quantity = "1";
      createCardForm.remarks = "";
      createCardForm.customPrefix = "";
      showCreateCardModal.value = true;
    }
    async function submitCreateCard() {
      if (creatingCards.value)
        return;
      const type = cardTypes.value[createCardForm.cardTypeIndex];
      if (!type) {
        common_vendor.index.showToast({ title: "请选择卡密类型", icon: "none" });
        return;
      }
      const quantityNumber = Number(createCardForm.quantity);
      const normalizedQuantity = Math.min(500, Math.max(1, Math.floor(quantityNumber)));
      if (!Number.isFinite(quantityNumber) || quantityNumber <= 0) {
        common_vendor.index.showToast({ title: "请输入 1-500 的数量", icon: "none" });
        return;
      }
      creatingCards.value = true;
      common_vendor.index.showLoading({ title: "生成中...", mask: true });
      try {
        createCardForm.quantity = normalizedQuantity.toString();
        const result = await appStore.generateCards({
          cardType: type.name,
          quantity: normalizedQuantity,
          remarks: createCardForm.remarks.trim(),
          customPrefix: createCardForm.customPrefix.trim() || void 0
        });
        const generated = result.generatedCards && result.generatedCards.length ? result.generatedCards : result.sampleCards || [];
        if (generated.length) {
          openGeneratedModal(generated, type.name || "cards");
        }
        common_vendor.index.showToast({ title: "生成成功", icon: "success" });
        showCreateCardModal.value = false;
        createCardForm.remarks = "";
        createCardForm.customPrefix = "";
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/cards/index.vue:738", "submitCreateCard error", error);
        common_vendor.index.showToast({ title: "生成失败", icon: "none" });
      } finally {
        creatingCards.value = false;
        common_vendor.index.hideLoading();
      }
    }
    function exportCard() {
      if (!rows.value.length) {
        common_vendor.index.showToast({ title: "暂无卡密可导出", icon: "none" });
        return;
      }
      const header = ["卡密编号", "卡密类型", "状态", "归属代理", "最近IP", "绑定机器码", "创建时间", "最近激活", "到期时间", "备注信息"];
      const csvRows = rows.value.map(
        (row) => [
          row.key,
          row.cardType,
          row.statusText || statusText[row.status],
          row.owner,
          row.ip || "",
          row.machineCodes.join("|"),
          row.createdAt,
          row.activatedAt,
          row.expireAt,
          row.remark
        ].map((value) => `"${(value ?? "").toString().replace(/"/g, '""')}"`).join(",")
      );
      const content = ["\uFEFF" + header.join(","), ...csvRows].join("\n");
      const filename = `cards-${Date.now()}.csv`;
      utils_download.downloadTextFile(filename, content, "text/csv;charset=utf-8");
      common_vendor.index.showToast({ title: "导出成功", icon: "success" });
    }
    common_vendor.watch(
      () => [
        cardFilters.keyword,
        cardFilters.cardType,
        cardFilters.status,
        cardFilters.agent,
        cardFilters.includeDescendants,
        cardFilters.machineCode,
        cardFilters.ip,
        cardFilters.startTime,
        cardFilters.endTime,
        cardFilters.searchType,
        cardFilters.limit
      ],
      () => {
        syncFormFromFilters();
      },
      { immediate: true }
    );
    common_vendor.watch(cardTypeOptions, () => syncFormFromFilters());
    common_vendor.watch(agentOptions, () => syncFormFromFilters());
    common_vendor.watch(
      () => cardTypes.value,
      (next) => {
        if (!next || !next.length) {
          showCreateCardModal.value = false;
          createCardForm.cardTypeIndex = 0;
          return;
        }
        if (createCardForm.cardTypeIndex >= next.length) {
          createCardForm.cardTypeIndex = 0;
        }
      },
      { immediate: true, deep: false }
    );
    common_vendor.onMounted(async () => {
      await appStore.ensureReady();
      try {
        await Promise.all([
          appStore.loadCardKeys(),
          appStore.loadCardTypes(),
          appStore.loadAgents({ limit: 200 })
        ]);
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/cards/index.vue:822", "cards/onMounted error", error);
      } finally {
        syncFormFromFilters();
      }
    });
    common_vendor.watch(
      () => selectedSoftware.value,
      async (next, prev) => {
        if (!next || next === prev)
          return;
        filtersForm.keyword = "";
        filtersForm.cardType = "";
        filtersForm.status = "";
        filtersForm.agent = "";
        filtersForm.includeDescendants = true;
        filtersForm.machineCode = "";
        filtersForm.ip = "";
        filtersForm.startTime = "";
        filtersForm.endTime = "";
        cardTypeIndex.value = 0;
        statusIndex.value = 0;
        agentIndex.value = 0;
        searchTypeIndex.value = 0;
        pageSizeIndex.value = pageSizeOptions.indexOf(50) >= 0 ? pageSizeOptions.indexOf(50) : 0;
        try {
          await Promise.all([
            appStore.loadCardKeys({ page: 1 }),
            appStore.loadCardTypes(true),
            appStore.loadAgents({ limit: 200, page: 1, keyword: "" })
          ]);
        } catch (error) {
          common_vendor.index.__f__("error", "at pages/cards/index.vue:853", "selectedSoftware change error", error);
        } finally {
          syncFormFromFilters();
        }
      }
    );
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: common_vendor.o(createCard),
        b: common_vendor.o(exportCard),
        c: filtersForm.keyword,
        d: common_vendor.o(($event) => filtersForm.keyword = $event.detail.value),
        e: common_vendor.t(cardTypeOptions.value[cardTypeIndex.value] || "全部"),
        f: cardTypeOptions.value,
        g: cardTypeIndex.value,
        h: common_vendor.o(onCardTypeChange),
        i: common_vendor.t(statusLabels.value[statusIndex.value] || "全部"),
        j: statusLabels.value,
        k: statusIndex.value,
        l: common_vendor.o(onStatusChange),
        m: common_vendor.t(agentOptions.value[agentIndex.value] || "全部"),
        n: agentOptions.value,
        o: agentIndex.value,
        p: common_vendor.o(onAgentChange),
        q: common_vendor.t(searchTypeLabels.value[searchTypeIndex.value] || "智能匹配"),
        r: searchTypeLabels.value,
        s: searchTypeIndex.value,
        t: common_vendor.o(onSearchTypeChange),
        v: filtersForm.includeDescendants,
        w: disableDescendants.value,
        x: common_vendor.o(onDescendantChange),
        y: disableDescendants.value
      }, disableDescendants.value ? {} : {}, {
        z: filtersForm.machineCode,
        A: common_vendor.o(($event) => filtersForm.machineCode = $event.detail.value),
        B: filtersForm.ip,
        C: common_vendor.o(($event) => filtersForm.ip = $event.detail.value),
        D: common_vendor.o(($event) => filtersForm.startTime = $event),
        E: common_vendor.p({
          placeholder: "选择开始时间",
          modelValue: filtersForm.startTime
        }),
        F: common_vendor.o(($event) => filtersForm.endTime = $event),
        G: common_vendor.p({
          placeholder: "选择结束时间",
          modelValue: filtersForm.endTime
        }),
        H: common_vendor.t(pageSizeLabels.value[pageSizeIndex.value]),
        I: pageSizeLabels.value,
        J: pageSizeIndex.value,
        K: common_vendor.o(onPageSizeChange),
        L: common_vendor.t(loading.value ? "查询中..." : "查询"),
        M: loading.value,
        N: common_vendor.o(submitSearch),
        O: loading.value,
        P: common_vendor.o(resetFilters),
        Q: loading.value,
        R: common_vendor.o(refreshList),
        S: common_vendor.w(({
          row
        }, s0, i0) => {
          return {
            a: common_vendor.t(row.key),
            b: common_vendor.o(($event) => copyValue(row.key)),
            c: "4177db9f-4-" + i0 + ",4177db9f-3",
            d: common_vendor.p({
              status: statusMap[row.status],
              label: row.statusText || statusText[row.status]
            }),
            e: i0,
            f: s0
          };
        }, {
          name: "key",
          path: "S",
          vueId: "4177db9f-3"
        }),
        T: common_vendor.w(({
          row
        }, s0, i0) => {
          return {
            a: "4177db9f-5-" + i0 + ",4177db9f-3",
            b: common_vendor.p({
              status: statusMap[row.status],
              label: row.statusText || statusText[row.status]
            }),
            c: i0,
            d: s0
          };
        }, {
          name: "status",
          path: "T",
          vueId: "4177db9f-3"
        }),
        U: common_vendor.w(({
          row
        }, s0, i0) => {
          return {
            a: common_vendor.t(row.ip || "—"),
            b: common_vendor.o(($event) => copyValue(row.ip || "")),
            c: i0,
            d: s0
          };
        }, {
          name: "ip",
          path: "U",
          vueId: "4177db9f-3"
        }),
        V: common_vendor.w(({
          row
        }, s0, i0) => {
          return common_vendor.e({
            a: row.machineCodes && row.machineCodes.length
          }, row.machineCodes && row.machineCodes.length ? {
            b: common_vendor.f(row.machineCodes, (code, k1, i1) => {
              return {
                a: common_vendor.t(code),
                b: code,
                c: common_vendor.o(($event) => copyValue(code), code)
              };
            })
          } : {}, {
            c: i0,
            d: s0
          });
        }, {
          name: "machineCodes",
          path: "V",
          vueId: "4177db9f-3"
        }),
        W: common_vendor.w(({
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
          path: "W",
          vueId: "4177db9f-3"
        }),
        X: common_vendor.w(({
          row
        }, s0, i0) => {
          return {
            a: common_vendor.t(row.fullKey),
            b: common_vendor.o(($event) => copyValue(row.fullKey)),
            c: i0,
            d: s0
          };
        }, {
          name: "fullKey",
          path: "X",
          vueId: "4177db9f-3"
        }),
        Y: common_vendor.w(({
          row
        }, s0, i0) => {
          return {
            a: common_vendor.t(isActionBusy(row.key, "enable") ? "处理中..." : "启用"),
            b: row.status !== "enabled" ? 1 : "",
            c: loading.value || isActionBusy(row.key, "enable"),
            d: common_vendor.o(($event) => handleStatusAction(row, "enable")),
            e: common_vendor.t(isActionBusy(row.key, "disable") ? "处理中..." : "禁用"),
            f: loading.value || isActionBusy(row.key, "disable"),
            g: common_vendor.o(($event) => handleStatusAction(row, "disable")),
            h: common_vendor.t(isActionBusy(row.key, "unban") ? "处理中..." : "解除封禁"),
            i: loading.value || isActionBusy(row.key, "unban"),
            j: common_vendor.o(($event) => handleStatusAction(row, "unban")),
            k: common_vendor.t(isActionBusy(row.key, "unbind") ? "处理中..." : "解绑"),
            l: loading.value || isActionBusy(row.key, "unbind"),
            m: common_vendor.o(($event) => handleUnbind(row)),
            n: i0,
            o: s0
          };
        }, {
          name: "operations",
          path: "Y",
          vueId: "4177db9f-3"
        }),
        Z: common_vendor.p({
          title: "卡密总览",
          subtitle: summaryText.value,
          columns,
          rows: rows.value,
          loading: loading.value,
          layout: "stack",
          ["primary-column"]: "key",
          ["operations-column"]: "operations",
          ["collapse-details"]: true
        }),
        aa: totalPages.value > 1 || common_vendor.unref(cardTotal)
      }, totalPages.value > 1 || common_vendor.unref(cardTotal) ? {
        ab: common_vendor.t(currentPage.value),
        ac: common_vendor.t(totalPages.value),
        ad: common_vendor.t(common_vendor.unref(cardTotal)),
        ae: currentPage.value <= 1 || loading.value,
        af: common_vendor.o(($event) => changePage(currentPage.value - 1)),
        ag: currentPage.value >= totalPages.value || loading.value,
        ah: common_vendor.o(($event) => changePage(currentPage.value + 1))
      } : {}, {
        ai: showCreateCardModal.value
      }, showCreateCardModal.value ? {
        aj: creatingCards.value,
        ak: common_vendor.o(closeCreateCardModal),
        al: common_vendor.t(cardTypePickerLabels.value[createCardForm.cardTypeIndex] || "请选择卡密类型"),
        am: cardTypePickerLabels.value,
        an: createCardForm.cardTypeIndex,
        ao: common_vendor.o(onCreateCardTypeChange),
        ap: createCardForm.quantity,
        aq: common_vendor.o(($event) => createCardForm.quantity = $event.detail.value),
        ar: createCardForm.remarks,
        as: common_vendor.o(($event) => createCardForm.remarks = $event.detail.value),
        at: createCardForm.customPrefix,
        av: common_vendor.o(($event) => createCardForm.customPrefix = $event.detail.value),
        aw: creatingCards.value,
        ax: common_vendor.o(closeCreateCardModal),
        ay: common_vendor.t(creatingCards.value ? "生成中..." : "立即生成"),
        az: creatingCards.value,
        aA: common_vendor.o(submitCreateCard),
        aB: common_vendor.o(() => {
        }),
        aC: common_vendor.o(closeCreateCardModal)
      } : {}, {
        aD: showGeneratedModal.value
      }, showGeneratedModal.value ? {
        aE: common_vendor.t(generatedCards.value.length),
        aF: common_vendor.o(closeGeneratedModal),
        aG: common_vendor.f(generatedCards.value, (card, k0, i0) => {
          return {
            a: common_vendor.t(card),
            b: card,
            c: common_vendor.o(($event) => copyValue(card), card)
          };
        }),
        aH: common_vendor.o(copyGeneratedCards),
        aI: !generatedCards.value.length,
        aJ: common_vendor.o(exportGeneratedCards),
        aK: !generatedCards.value.length,
        aL: common_vendor.o(() => {
        }),
        aM: common_vendor.o(closeGeneratedModal)
      } : {});
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-4177db9f"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/cards/index.js.map
