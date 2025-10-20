"use strict";
const common_vendor = require("../../common/vendor.js");
const stores_app = require("../../stores/app.js");
const stores_platform = require("../../stores/platform.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "settlement",
  setup(__props) {
    const appStore = stores_app.useAppStore();
    const platformStore = stores_platform.usePlatformStore();
    const {
      selectedSoftware,
      cardTypes,
      selectedSettlementAgent,
      settlementAgents,
      activeSettlementAgent,
      settlementCycle,
      settlementBills,
      settlementHasReminder
    } = common_vendor.storeToRefs(appStore);
    const editableRates = common_vendor.ref([]);
    const cycleDaysInput = common_vendor.ref("");
    const cycleTimeInput = common_vendor.ref("");
    const billAmountEdits = common_vendor.reactive({});
    const billNoteEdits = common_vendor.reactive({});
    const loadingSettlement = common_vendor.computed(() => appStore.loading.settlementRates);
    const loadingCardTypes = common_vendor.computed(() => appStore.loading.cardTypes);
    const savingRates = common_vendor.computed(() => appStore.loading.saveSettlementRates);
    const agentOptions = common_vendor.computed(() => {
      var _a;
      const rawOptions = Array.isArray(settlementAgents.value) ? settlementAgents.value : [];
      const options = rawOptions.map((item) => ({
        value: ((item == null ? void 0 : item.username) ?? "").toString().trim(),
        label: ((item == null ? void 0 : item.displayName) ?? "").toString().trim() || ((item == null ? void 0 : item.username) ?? "").toString().trim()
      }));
      const filtered = options.filter((item) => item.value.length > 0);
      if (!filtered.length) {
        const fallback = (_a = activeSettlementAgent.value) == null ? void 0 : _a.trim();
        if (fallback) {
          filtered.push({ value: fallback, label: `${fallback} · 当前账号` });
        }
      }
      return filtered;
    });
    const hasAgentOptions = common_vendor.computed(() => agentOptions.value.length > 0);
    const selectedAgentValue = common_vendor.computed(() => {
      var _a, _b;
      return ((_a = selectedSettlementAgent.value) == null ? void 0 : _a.trim()) || ((_b = activeSettlementAgent.value) == null ? void 0 : _b.trim()) || "";
    });
    const agentIndex = common_vendor.computed(() => {
      const options = agentOptions.value;
      const current = selectedAgentValue.value;
      const index = options.findIndex((option) => option.value === current);
      return index >= 0 ? index : 0;
    });
    const selectedAgentLabel = common_vendor.computed(() => {
      var _a;
      const options = agentOptions.value;
      const current = selectedAgentValue.value;
      const match = options.find((option) => option.value === current);
      return (match == null ? void 0 : match.label) || ((_a = options[0]) == null ? void 0 : _a.label) || "当前账号";
    });
    const cycleInfo = common_vendor.computed(() => settlementCycle.value);
    const cycleDue = common_vendor.computed(() => {
      var _a;
      return Boolean((_a = cycleInfo.value) == null ? void 0 : _a.isDue);
    });
    const effectiveCycleDays = common_vendor.computed(() => {
      var _a;
      return ((_a = cycleInfo.value) == null ? void 0 : _a.effectiveDays) ?? 0;
    });
    const cycleInherited = common_vendor.computed(() => {
      var _a;
      return Boolean((_a = cycleInfo.value) == null ? void 0 : _a.isInherited);
    });
    const nextDueText = common_vendor.computed(() => {
      var _a;
      return formatDateTimeDisplay((_a = cycleInfo.value) == null ? void 0 : _a.nextDueTimeUtc);
    });
    const lastSettledText = common_vendor.computed(() => {
      var _a;
      return formatDateTimeDisplay((_a = cycleInfo.value) == null ? void 0 : _a.lastSettledTimeUtc);
    });
    const effectiveCycleTimeText = common_vendor.computed(
      () => {
        var _a;
        return ((_a = cycleInfo.value) == null ? void 0 : _a.effectiveTimeLabel) || formatTimeLabel(0);
      }
    );
    const cycleTimePlaceholder = common_vendor.computed(() => {
      const info = cycleInfo.value;
      if (!info) {
        return "HH:mm";
      }
      const ownDefined = Number.isInteger(info.ownTimeMinutes) && (info.ownDays > 0 || info.ownTimeMinutes !== info.effectiveTimeMinutes);
      if (ownDefined) {
        return info.ownTimeLabel || formatTimeLabel(info.ownTimeMinutes);
      }
      return `默认 ${info.effectiveTimeLabel || formatTimeLabel(info.effectiveTimeMinutes)}`;
    });
    const pendingBills = common_vendor.computed(() => settlementBills.value.filter((bill) => !bill.isSettled));
    const settledBills = common_vendor.computed(() => settlementBills.value.filter((bill) => bill.isSettled));
    function hasBreakdown(bill) {
      return Array.isArray(bill == null ? void 0 : bill.breakdowns) && bill.breakdowns.length > 0;
    }
    common_vendor.watch(
      agentOptions,
      async (options) => {
        if (!options.length) {
          return;
        }
        const current = selectedAgentValue.value;
        const hasMatch = options.some((option) => option.value === current);
        if (!hasMatch) {
          const fallback = options[0];
          if (fallback) {
            appStore.setSelectedSettlementAgent(fallback.value);
            await refresh(true);
          }
        }
      },
      { immediate: false }
    );
    common_vendor.watch(
      settlementCycle,
      (cycle) => {
        if (!cycle) {
          cycleDaysInput.value = "";
          cycleTimeInput.value = "";
          return;
        }
        const source = cycle.ownDays > 0 ? cycle.ownDays : cycle.effectiveDays;
        cycleDaysInput.value = source > 0 ? String(source) : "";
        const timeSource = cycle.ownDays > 0 || cycle.ownTimeMinutes !== cycle.effectiveTimeMinutes ? cycle.ownTimeMinutes : cycle.effectiveTimeMinutes;
        if (Number.isFinite(timeSource)) {
          cycleTimeInput.value = formatTimeLabel(Number(timeSource));
        } else {
          cycleTimeInput.value = "";
        }
      },
      { immediate: true }
    );
    common_vendor.watch(
      pendingBills,
      (bills) => {
        const activeIds = /* @__PURE__ */ new Set();
        bills.forEach((bill) => {
          activeIds.add(bill.id);
          const suggested = formatAmountInput(bill.suggestedAmount ?? bill.amount);
          if (!billAmountEdits[bill.id] || billAmountEdits[bill.id] === "0" || billAmountEdits[bill.id] === "0.00") {
            billAmountEdits[bill.id] = suggested;
          }
          if (bill.note && !billNoteEdits[bill.id]) {
            billNoteEdits[bill.id] = bill.note;
          }
        });
        Object.keys(billAmountEdits).forEach((key) => {
          const id = Number(key);
          if (!Number.isFinite(id) || !activeIds.has(id)) {
            delete billAmountEdits[id];
          }
        });
        Object.keys(billNoteEdits).forEach((key) => {
          const id = Number(key);
          if (!Number.isFinite(id) || !activeIds.has(id)) {
            delete billNoteEdits[id];
          }
        });
      },
      { immediate: true }
    );
    async function bootstrap() {
      if (!platformStore.isAuthenticated) {
        return;
      }
      await refresh(true);
    }
    let refreshGeneration = 0;
    function buildEditableRates(settlementRatesList, cardTypeList) {
      const availableTypes = /* @__PURE__ */ new Set();
      settlementRatesList.forEach((rate) => {
        if (rate == null ? void 0 : rate.cardType) {
          availableTypes.add(rate.cardType);
        }
      });
      if (Array.isArray(cardTypeList)) {
        cardTypeList.forEach((type) => {
          if (type == null ? void 0 : type.name) {
            availableTypes.add(type.name);
          }
        });
      }
      return Array.from(availableTypes).sort((a, b) => a.localeCompare(b)).map((cardType) => {
        const current = settlementRatesList.find((rate) => rate.cardType === cardType);
        return {
          cardType,
          price: current ? formatCurrency(current.price) : "0.00"
        };
      });
    }
    async function refresh(force = false) {
      const software = selectedSoftware.value;
      if (!software) {
        editableRates.value = [];
        return;
      }
      try {
        const generation = ++refreshGeneration;
        const cardTypesPromise = appStore.loadCardTypes(force).catch((error) => {
          common_vendor.index.__f__("warn", "at pages/agent/settlement.vue:429", "loadCardTypes failed", error);
          return null;
        });
        const loadedSettlementRates = await appStore.loadSettlementRates(force);
        const settlementRates = Array.isArray(loadedSettlementRates) ? loadedSettlementRates : [];
        editableRates.value = buildEditableRates(settlementRates, cardTypes.value);
        cardTypesPromise.then((loadedCardTypes) => {
          if (generation !== refreshGeneration) {
            return;
          }
          const resolved = Array.isArray(loadedCardTypes) && loadedCardTypes.length > 0 ? loadedCardTypes : cardTypes.value;
          editableRates.value = buildEditableRates(settlementRates, resolved);
        });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agent/settlement.vue:448", "refresh settlement rates failed", error);
        common_vendor.index.showToast({ title: "加载失败", icon: "none" });
      }
    }
    function formatCurrency(value) {
      const amount = Number(value ?? 0);
      if (!Number.isFinite(amount)) {
        return "0.00";
      }
      return amount.toFixed(2);
    }
    function formatAmountInput(value) {
      if (value == null) {
        return "";
      }
      const numeric = Number(value);
      if (!Number.isFinite(numeric) || numeric <= 0) {
        return "";
      }
      return formatCurrency(numeric);
    }
    function normalizePrice(item) {
      item.price = formatCurrency(item.price);
    }
    function formatDateTimeDisplay(value) {
      if (!value) {
        return "--";
      }
      const date = new Date(value);
      if (Number.isNaN(date.getTime())) {
        return value;
      }
      const yyyy = date.getFullYear();
      const mm = String(date.getMonth() + 1).padStart(2, "0");
      const dd = String(date.getDate()).padStart(2, "0");
      const hh = String(date.getHours()).padStart(2, "0");
      const mi = String(date.getMinutes()).padStart(2, "0");
      return `${yyyy}-${mm}-${dd} ${hh}:${mi}`;
    }
    function formatTimeLabel(value) {
      const minutes = Number(value);
      if (!Number.isFinite(minutes)) {
        return "00:00";
      }
      const normalized = (minutes % 1440 + 1440) % 1440;
      const hours = Math.floor(normalized / 60);
      const mins = Math.floor(normalized % 60);
      return `${hours.toString().padStart(2, "0")}:${mins.toString().padStart(2, "0")}`;
    }
    function parseCycleDays() {
      const raw = cycleDaysInput.value.trim();
      if (!raw) {
        return null;
      }
      const numeric = Number(raw);
      if (!Number.isFinite(numeric) || numeric <= 0) {
        return 0;
      }
      return Math.floor(numeric);
    }
    function handleCycleBlur() {
      const parsed = parseCycleDays();
      if (parsed === null || parsed <= 0) {
        cycleDaysInput.value = "";
      } else {
        cycleDaysInput.value = String(parsed);
      }
    }
    function parseCycleTimeMinutes() {
      const raw = cycleTimeInput.value.trim();
      if (!raw) {
        return null;
      }
      const normalized = raw.replace("：", ":");
      const match = normalized.match(/^\s*(\d{1,2})\s*:\s*(\d{1,2})\s*$/);
      if (!match) {
        return void 0;
      }
      const hours = Number(match[1]);
      const minutes = Number(match[2]);
      if (!Number.isFinite(hours) || !Number.isFinite(minutes) || hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
        return void 0;
      }
      return hours * 60 + minutes;
    }
    function handleCycleTimeBlur() {
      const parsed = parseCycleTimeMinutes();
      if (parsed === void 0) {
        cycleTimeInput.value = "";
        common_vendor.index.showToast({ title: "时间格式应为 HH:mm", icon: "none" });
        return;
      }
      if (parsed === null) {
        cycleTimeInput.value = "";
        return;
      }
      cycleTimeInput.value = formatTimeLabel(parsed);
    }
    async function save() {
      const software = selectedSoftware.value;
      if (!software) {
        common_vendor.index.showToast({ title: "请先选择软件位", icon: "none" });
        return;
      }
      const payload = editableRates.value.map((item) => ({
        cardType: item.cardType,
        price: Number(item.price) || 0
      }));
      try {
        const cycleDays = parseCycleDays();
        const cycleTimeMinutes = parseCycleTimeMinutes();
        if (cycleTimeMinutes === void 0) {
          common_vendor.index.showToast({ title: "请填写正确的结算时间（HH:mm）", icon: "none" });
          return;
        }
        await appStore.saveSettlementRates(
          payload,
          cycleDays ?? void 0,
          cycleTimeMinutes ?? void 0
        );
        common_vendor.index.showToast({ title: "保存成功", icon: "success" });
        await refresh(true);
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agent/settlement.vue:583", "save settlement rates failed", error);
        const message = (error == null ? void 0 : error.message) || "保存失败";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    async function confirmBill(billId) {
      const rawAmount = billAmountEdits[billId] ?? "0";
      let amount = Number(rawAmount);
      const bill = pendingBills.value.find((item) => item.id === billId);
      if ((!Number.isFinite(amount) || amount <= 0) && bill && Number(bill.suggestedAmount ?? 0) > 0) {
        amount = Number(bill.suggestedAmount);
      }
      if (!Number.isFinite(amount) || amount < 0) {
        common_vendor.index.showToast({ title: "请输入有效金额", icon: "none" });
        return;
      }
      try {
        await appStore.completeSettlementBill(billId, amount, billNoteEdits[billId]);
        delete billAmountEdits[billId];
        delete billNoteEdits[billId];
        common_vendor.index.showToast({ title: "账单已结算", icon: "success" });
      } catch (error) {
        common_vendor.index.__f__("error", "at pages/agent/settlement.vue:608", "complete settlement bill failed", error);
        const message = (error == null ? void 0 : error.message) || "更新失败";
        common_vendor.index.showToast({ title: message, icon: "none" });
      }
    }
    async function handleAgentChange(event) {
      var _a;
      const index = Number(((_a = event == null ? void 0 : event.detail) == null ? void 0 : _a.value) ?? 0);
      const options = agentOptions.value;
      const option = options[index];
      if (!option) {
        return;
      }
      appStore.setSelectedSettlementAgent(option.value);
      await refresh(true);
    }
    function goLogin() {
      common_vendor.index.reLaunch({ url: "/pages/login/agent" });
    }
    common_vendor.watch(
      () => selectedSoftware.value,
      () => {
        refresh();
      }
    );
    common_vendor.onMounted(() => {
      bootstrap();
    });
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: !common_vendor.unref(platformStore).isAuthenticated
      }, !common_vendor.unref(platformStore).isAuthenticated ? {
        b: common_vendor.o(goLogin)
      } : common_vendor.e({
        c: common_vendor.t(common_vendor.unref(selectedSoftware) || "未选择"),
        d: !common_vendor.unref(selectedSoftware)
      }, !common_vendor.unref(selectedSoftware) ? {} : common_vendor.e({
        e: loadingSettlement.value
      }, loadingSettlement.value ? {} : !editableRates.value.length ? {} : common_vendor.e({
        g: loadingCardTypes.value
      }, loadingCardTypes.value ? {} : {}, {
        h: hasAgentOptions.value
      }, hasAgentOptions.value ? {
        i: common_vendor.t(selectedAgentLabel.value),
        j: loadingSettlement.value ? 1 : "",
        k: agentOptions.value,
        l: agentIndex.value,
        m: loadingSettlement.value,
        n: common_vendor.o(handleAgentChange)
      } : {}, {
        o: cycleInherited.value
      }, cycleInherited.value ? {} : {}, {
        p: common_vendor.t(nextDueText.value),
        q: cycleDue.value ? 1 : "",
        r: effectiveCycleDays.value > 0 ? `默认 ${effectiveCycleDays.value} 天` : "输入天数",
        s: savingRates.value,
        t: common_vendor.o(handleCycleBlur),
        v: cycleDaysInput.value,
        w: common_vendor.o(($event) => cycleDaysInput.value = $event.detail.value),
        x: cycleTimePlaceholder.value,
        y: savingRates.value,
        z: common_vendor.o(handleCycleTimeBlur),
        A: cycleTimeInput.value,
        B: common_vendor.o(($event) => cycleTimeInput.value = $event.detail.value),
        C: common_vendor.t(lastSettledText.value),
        D: common_vendor.t(effectiveCycleTimeText.value),
        E: cycleDue.value
      }, cycleDue.value ? {} : {}, {
        F: common_vendor.f(editableRates.value, (item, k0, i0) => {
          return {
            a: common_vendor.t(item.cardType),
            b: common_vendor.o(($event) => normalizePrice(item), item.cardType),
            c: item.price,
            d: common_vendor.o(($event) => item.price = $event.detail.value, item.cardType),
            e: item.cardType
          };
        })
      }), {
        f: !editableRates.value.length,
        G: common_vendor.t(savingRates.value ? "保存中..." : "保存设置"),
        H: savingRates.value || loadingSettlement.value || loadingCardTypes.value,
        I: common_vendor.o(save),
        J: savingRates.value || loadingSettlement.value,
        K: common_vendor.o(refresh)
      }), {
        L: !pendingBills.value.length && !settledBills.value.length
      }, !pendingBills.value.length && !settledBills.value.length ? {} : {
        M: common_vendor.f(pendingBills.value, (bill, k0, i0) => {
          return common_vendor.e({
            a: common_vendor.t(formatDateTimeDisplay(bill.cycleStartUtc)),
            b: common_vendor.t(formatDateTimeDisplay(bill.cycleEndUtc)),
            c: hasBreakdown(bill)
          }, hasBreakdown(bill) ? {
            d: common_vendor.t(formatCurrency(bill.suggestedAmount ?? bill.amount)),
            e: common_vendor.f(bill.breakdowns, (item, k1, i1) => {
              return {
                a: common_vendor.t(item.displayName || item.agent),
                b: common_vendor.t(item.count),
                c: common_vendor.t(formatCurrency(item.amount)),
                d: `${bill.id}-${item.agent}`
              };
            })
          } : {}, {
            f: bill.suggestedAmount ? formatCurrency(bill.suggestedAmount) : "0.00",
            g: !hasBreakdown(bill),
            h: billAmountEdits[bill.id],
            i: common_vendor.o(($event) => billAmountEdits[bill.id] = $event.detail.value, `pending-${bill.id}`),
            j: billNoteEdits[bill.id],
            k: common_vendor.o(($event) => billNoteEdits[bill.id] = $event.detail.value, `pending-${bill.id}`),
            l: savingRates.value || !hasBreakdown(bill),
            m: common_vendor.o(($event) => confirmBill(bill.id), `pending-${bill.id}`),
            n: `pending-${bill.id}`
          });
        }),
        N: common_vendor.f(settledBills.value, (bill, k0, i0) => {
          var _a, _b;
          return common_vendor.e({
            a: common_vendor.t(formatDateTimeDisplay(bill.cycleStartUtc)),
            b: common_vendor.t(formatDateTimeDisplay(bill.cycleEndUtc)),
            c: common_vendor.t(formatCurrency(bill.amount)),
            d: (_a = bill.breakdowns) == null ? void 0 : _a.length
          }, ((_b = bill.breakdowns) == null ? void 0 : _b.length) ? {
            e: common_vendor.f(bill.breakdowns, (item, k1, i1) => {
              return {
                a: common_vendor.t(item.displayName || item.agent),
                b: common_vendor.t(item.count),
                c: common_vendor.t(formatCurrency(item.amount)),
                d: `${bill.id}-${item.agent}`
              };
            })
          } : {}, {
            f: common_vendor.t(formatDateTimeDisplay(bill.settledAtUtc)),
            g: bill.note
          }, bill.note ? {
            h: common_vendor.t(bill.note)
          } : {}, {
            i: `settled-${bill.id}`
          });
        })
      }));
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-8f29828e"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/agent/settlement.js.map
