"use strict";
const common_vendor = require("../common/vendor.js");
const common_api = require("../common/api.js");
const stores_platform = require("./platform.js");
const utils_storage = require("../utils/storage.js");
const utils_time = require("../utils/time.js");
const DEFAULT_CARD_PAGE_SIZE = 50;
const DEFAULT_LINK_PAGE_SIZE = 20;
function resolveCardStatus(state) {
  const normalized = (state ?? "").trim();
  if (!normalized) {
    return { status: "unknown", text: "未知" };
  }
  if (["启用", "可用", "active", "enabled", "0"].includes(normalized)) {
    return { status: "enabled", text: normalized || "启用" };
  }
  if (["禁用", "封禁", "disabled", "1", "停用"].includes(normalized)) {
    return { status: "disabled", text: normalized };
  }
  return { status: "unknown", text: normalized };
}
function ensureArray(value) {
  if (Array.isArray(value)) {
    return value;
  }
  return [];
}
function splitKeywordInput(raw) {
  return raw.split(/[\s,，;；]+/).map((item) => item.trim()).filter((item) => item.length > 0);
}
function computeTrendMetrics(points) {
  if (!points.length) {
    return { total: 0, max: 0 };
  }
  return points.reduce(
    (acc, point) => {
      const value = Number(point.value ?? 0);
      if (Number.isFinite(value)) {
        acc.total += value;
        if (value > acc.max) {
          acc.max = value;
        }
      }
      return acc;
    },
    { total: 0, max: 0 }
  );
}
function buildSubAgentTrendPayload(trend) {
  if (!trend) {
    return { categories: [], series: [], total: 0 };
  }
  const categoriesRaw = ensureArray(trend.categories);
  const points = ensureArray(trend.points);
  const categories = categoriesRaw.length ? categoriesRaw.map((item) => item.trim()).filter((item) => item.length > 0) : points.map((item) => ((item == null ? void 0 : item.date) || "").toString().trim()).filter((item) => item.length > 0);
  const normalizedCategories = categories.length ? categories : [];
  const series = ensureArray(trend.series).map((item) => {
    const name = ((item == null ? void 0 : item.displayName) || (item == null ? void 0 : item.agent) || "").toString().trim() || "未命名代理";
    const map = /* @__PURE__ */ new Map();
    ensureArray(item == null ? void 0 : item.points).forEach((point) => {
      const key = ((point == null ? void 0 : point.date) || "").toString().trim();
      if (!key)
        return;
      const value = Number((point == null ? void 0 : point.count) ?? 0);
      map.set(key, Number.isFinite(value) ? value : 0);
    });
    const values = normalizedCategories.map((category) => map.get(category) ?? 0);
    const total2 = values.reduce((sum, value) => sum + value, 0);
    return { name, values, total: total2 };
  });
  const total = series.reduce((sum, item) => sum + item.total, 0);
  return { categories: normalizedCategories, series, total };
}
function normalizeSoftwareName(value) {
  return (value ?? "").toString().trim().toLowerCase();
}
function matchesSoftware(value, software) {
  if (!software)
    return false;
  return normalizeSoftwareName(value).toLowerCase() === normalizeSoftwareName(software).toLowerCase();
}
const BLACKLIST_STORAGE_PREFIX = "sprotect:blacklist:lastSeen:";
function buildBlacklistStorageKey(software) {
  return `${BLACKLIST_STORAGE_PREFIX}${software}`;
}
function normalizeStorageValue(value) {
  if (typeof value === "string") {
    return value;
  }
  if (typeof value === "number" && Number.isFinite(value)) {
    return value.toString();
  }
  if (value != null) {
    try {
      return String(value);
    } catch (error) {
      common_vendor.index.__f__("warn", "at stores/app.ts:202", "Failed to normalize storage value", error);
    }
  }
  return "";
}
function transformSalesResult(data) {
  const cards = ensureArray(data == null ? void 0 : data.cards).map((item) => ({
    card: ((item == null ? void 0 : item.card) || (item == null ? void 0 : item.prefix_Name) || (item == null ? void 0 : item.key) || "").toString(),
    activateTime: utils_time.formatDateTime((item == null ? void 0 : item.activateTime) ?? (item == null ? void 0 : item.activateTime_) ?? (item == null ? void 0 : item.ActivateTime_))
  }));
  const count = Number((data == null ? void 0 : data.count) ?? cards.length ?? 0);
  const settlements = ensureArray(data == null ? void 0 : data.settlements).map((item) => ({
    cardType: ((item == null ? void 0 : item.cardType) ?? "").toString(),
    count: Number((item == null ? void 0 : item.count) ?? 0) || 0,
    price: Number((item == null ? void 0 : item.price) ?? 0) || 0,
    total: Number((item == null ? void 0 : item.total) ?? 0) || 0
  })).filter((item) => item.cardType);
  const totalAmountRaw = Number((data == null ? void 0 : data.totalAmount) ?? 0);
  const totalAmount = Number.isFinite(totalAmountRaw) ? totalAmountRaw : settlements.reduce((sum, item) => sum + item.total, 0);
  return {
    count: Number.isFinite(count) ? count : cards.length,
    cards,
    settlements,
    totalAmount
  };
}
function normalizeSettlementBill(raw) {
  const breakdowns = ensureArray(raw == null ? void 0 : raw.breakdowns).map((item) => ({
    agent: ((item == null ? void 0 : item.agent) ?? "").toString().trim(),
    displayName: ((item == null ? void 0 : item.displayName) ?? "").toString().trim(),
    count: Number((item == null ? void 0 : item.count) ?? 0) || 0,
    amount: Number((item == null ? void 0 : item.amount) ?? 0) || 0
  })).filter((item) => item.agent.length > 0);
  return {
    id: Number((raw == null ? void 0 : raw.id) ?? 0) || 0,
    cycleStartUtc: ((raw == null ? void 0 : raw.cycleStartUtc) ?? "").toString(),
    cycleEndUtc: ((raw == null ? void 0 : raw.cycleEndUtc) ?? "").toString(),
    amount: Number((raw == null ? void 0 : raw.amount) ?? 0) || 0,
    suggestedAmount: (raw == null ? void 0 : raw.suggestedAmount) == null ? void 0 : (() => {
      const value = Number(raw.suggestedAmount);
      return Number.isFinite(value) && value > 0 ? value : void 0;
    })(),
    isSettled: Boolean(raw == null ? void 0 : raw.isSettled),
    settledAtUtc: (raw == null ? void 0 : raw.settledAtUtc) ?? (raw == null ? void 0 : raw.SettledAtUtc) ?? null,
    note: ((raw == null ? void 0 : raw.note) ?? "").toString() || null,
    breakdowns
  };
}
function transformBlacklistLog(raw) {
  return {
    timestamp: utils_time.formatDateTime((raw == null ? void 0 : raw.timestamp) ?? (raw == null ? void 0 : raw.Timestamp)),
    software: normalizeSoftwareName((raw == null ? void 0 : raw.software) ?? (raw == null ? void 0 : raw.Software)),
    ip: ((raw == null ? void 0 : raw.ip) ?? (raw == null ? void 0 : raw.IP) ?? "").toString(),
    card: ((raw == null ? void 0 : raw.card) ?? (raw == null ? void 0 : raw.Card) ?? "").toString(),
    machineCode: ((raw == null ? void 0 : raw.pcsign) ?? (raw == null ? void 0 : raw.PCSign) ?? "").toString(),
    event: ((raw == null ? void 0 : raw.errEvents) ?? (raw == null ? void 0 : raw.ErrEvents) ?? "").toString()
  };
}
function transformBlacklistMachine(raw) {
  const value = ((raw == null ? void 0 : raw.value) ?? (raw == null ? void 0 : raw.Value) ?? "").toString();
  return {
    value,
    software: normalizeSoftwareName((raw == null ? void 0 : raw.software) ?? (raw == null ? void 0 : raw.Software)),
    type: Number((raw == null ? void 0 : raw.type) ?? (raw == null ? void 0 : raw.Type) ?? 2) || 0,
    remarks: ((raw == null ? void 0 : raw.remarks) ?? (raw == null ? void 0 : raw.Remarks) ?? "").toString() || void 0
  };
}
function buildCardListPayload(software, filters, page, limit) {
  const payload = { software, page, limit };
  const agent = (filters.agent ?? "").trim();
  if (agent) {
    payload.agent = agent;
    payload.includeDescendants = false;
  } else {
    payload.includeDescendants = filters.includeDescendants ?? true;
  }
  if (filters.status && filters.status !== "") {
    payload.status = filters.status;
  }
  const rawKeyword = (filters.keyword ?? "").trim();
  const baseKeywords = filters.keywords && filters.keywords.length ? filters.keywords : splitKeywordInput(rawKeyword);
  const ipRegex = /^(\d{1,3}\.){3}\d{1,3}$/;
  let searchType = typeof filters.searchType === "number" ? filters.searchType : 0;
  if (filters.cardType && (searchType === 0 || searchType === 3)) {
    searchType = 3;
  }
  if (!filters.cardType && baseKeywords.length === 1 && ipRegex.test(baseKeywords[0]) && (searchType === 0 || searchType === 2)) {
    searchType = 2;
  }
  payload.searchType = searchType;
  let keywords = [];
  if (searchType === 3 && filters.cardType) {
    keywords = [filters.cardType];
  } else if (searchType === 2) {
    keywords = baseKeywords.filter((item) => ipRegex.test(item));
  } else if (searchType === 1) {
    keywords = baseKeywords.slice(0, 10);
  } else {
    keywords = baseKeywords;
  }
  if (filters.machineCode) {
    keywords.push(filters.machineCode.trim());
  }
  if (filters.ip) {
    keywords.push(filters.ip.trim());
  }
  keywords = Array.from(new Set(keywords.filter((item) => item.length > 0)));
  if (keywords.length) {
    payload.keywords = keywords;
  }
  if (filters.cardType && searchType !== 3) {
    payload.cardType = filters.cardType;
  }
  if (filters.startTime) {
    payload.startTime = filters.startTime;
  }
  if (filters.endTime) {
    payload.endTime = filters.endTime;
  }
  return payload;
}
function transformCard(item) {
  const key = item.prefix_Name || "";
  const owner = (item.whom || item.owner || "").trim();
  const cardType = (item.cardType || "").trim();
  const { status, text } = resolveCardStatus(item.state);
  const createdAt = utils_time.formatDateTime(item.createData_);
  const activatedAt = utils_time.formatDateTime(item.activateTime_ ?? item.lastLoginTime_);
  const expireAt = utils_time.formatDateTime(item.expiredTime__);
  const machineCodes = ensureArray(item.machineCodes).map((code) => code.trim()).filter((code) => code.length > 0);
  if (item.machineCode) {
    const candidate = item.machineCode.trim();
    if (candidate && !machineCodes.includes(candidate)) {
      machineCodes.unshift(candidate);
    }
  }
  return {
    key,
    owner: owner || "—",
    cardType: cardType || "未分类",
    status,
    statusText: text || (status === "enabled" ? "启用" : status === "disabled" ? "禁用" : "未知"),
    createdAt,
    activatedAt,
    expireAt,
    ip: item.ip || void 0,
    machineCodes,
    remark: item.remarks || ""
  };
}
function determineAgentStatus(status) {
  if (typeof status === "boolean") {
    return status ? "enabled" : "disabled";
  }
  if (typeof status === "number") {
    return status === 0 ? "enabled" : "disabled";
  }
  if (typeof status === "string") {
    const normalized = status.trim().toLowerCase();
    if (["启用", "enabled", "true", "0"].includes(normalized)) {
      return "enabled";
    }
    if (["禁用", "disabled", "false", "1", "停用"].includes(normalized)) {
      return "disabled";
    }
  }
  return "enabled";
}
function toNumber(value, fallback = 0) {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : fallback;
}
function transformAgent(raw) {
  const username = ((raw == null ? void 0 : raw.username) || (raw == null ? void 0 : raw.user) || (raw == null ? void 0 : raw.account) || "-").toString();
  const balance = toNumber((raw == null ? void 0 : raw.balance) ?? (raw == null ? void 0 : raw.money));
  const timeStock = toNumber((raw == null ? void 0 : raw.timeStock) ?? (raw == null ? void 0 : raw.stockHours));
  const parities = toNumber(raw == null ? void 0 : raw.parities);
  const totalParities = toNumber((raw == null ? void 0 : raw.totalParities) ?? (raw == null ? void 0 : raw.total_parities), parities);
  const remark = ((raw == null ? void 0 : raw.remark) ?? (raw == null ? void 0 : raw.remarks) ?? "").toString();
  const password = ((raw == null ? void 0 : raw.password) ?? (raw == null ? void 0 : raw.Password) ?? "").toString();
  const cardTypes = ensureArray((raw == null ? void 0 : raw.card_types) ?? (raw == null ? void 0 : raw.cardTypes)).map((item) => item.toString());
  const expiration = (raw == null ? void 0 : raw.expiration) ? utils_time.formatDateTime(raw.expiration) : void 0;
  const status = determineAgentStatus((raw == null ? void 0 : raw.status) ?? (raw == null ? void 0 : raw.stat) ?? (raw == null ? void 0 : raw.cardsEnable) ?? (raw == null ? void 0 : raw.enabled) ?? (raw == null ? void 0 : raw.state));
  const depth = toNumber(raw == null ? void 0 : raw.depth, 0);
  return {
    username,
    balance,
    timeStock,
    parities,
    totalParities,
    status,
    depth,
    remark,
    password,
    cardTypes,
    expiration
  };
}
function transformContact(dto) {
  var _a;
  const username = ((dto == null ? void 0 : dto.username) || "").trim();
  const displayName = ((dto == null ? void 0 : dto.displayName) || username || "-").toString().trim();
  const remark = (_a = dto == null ? void 0 : dto.remark) == null ? void 0 : _a.toString().trim();
  return {
    username,
    displayName: displayName || username || "未命名联系人",
    remark: remark || void 0
  };
}
function buildDashboardStats(stats) {
  if (!stats) {
    return [];
  }
  return [
    { key: "total", label: "卡密总数", value: stats.totalCards.toString() },
    { key: "active", label: "启用中", value: stats.activeCards.toString() },
    { key: "used", label: "已使用", value: stats.usedCards.toString() },
    { key: "expired", label: "已过期", value: stats.expiredCards.toString() },
    { key: "subAgents", label: "子代理", value: stats.subAgents.toString() }
  ];
}
function buildTrendData(trend) {
  if (!trend) {
    return [];
  }
  if (Array.isArray(trend.categories) && trend.categories.length > 0) {
    const map = /* @__PURE__ */ new Map();
    (trend.points || []).forEach((point) => {
      if (!(point == null ? void 0 : point.date))
        return;
      map.set(point.date, toNumber(point.count));
    });
    const categories = trend.categories.slice(-7);
    return categories.map((category) => ({
      date: category,
      value: map.get(category) ?? 0
    }));
  }
  const points = (trend.points || []).map((point) => ({
    date: point.date,
    value: toNumber(point.count)
  }));
  return points.slice(-7);
}
function buildAnnouncementsPayload(response) {
  if (!response || !response.content) {
    return [];
  }
  return [
    {
      id: "announcement",
      title: "运维公告",
      content: response.content.trim(),
      updatedAt: utils_time.formatDateTime(response.updatedAt)
    }
  ];
}
function buildHeatmapPayload(response) {
  if (!response) {
    return [];
  }
  const provinces = ensureArray(response.provinces).sort((a, b) => ((b == null ? void 0 : b.count) ?? 0) - ((a == null ? void 0 : a.count) ?? 0));
  const total = response.resolvedTotal || provinces.reduce((sum, item) => sum + ((item == null ? void 0 : item.count) ?? 0), 0);
  return provinces.slice(0, 6).map((item, index) => {
    const name = (item == null ? void 0 : item.province) || (item == null ? void 0 : item.city) || (item == null ? void 0 : item.district) || `地区${index + 1}`;
    const count = (item == null ? void 0 : item.count) ?? 0;
    const percentage = total > 0 ? Math.round(count / total * 1e3) / 10 : 0;
    return {
      name,
      count,
      percentage
    };
  });
}
function transformLink(record) {
  return {
    id: record.id,
    url: record.url,
    extractionCode: record.extractionCode,
    createdAt: utils_time.formatDateTime(record.createdAt),
    content: record.rawContent
  };
}
function transformConversation(dto, currentUser) {
  const participants = ensureArray(dto.participants);
  const displayName = dto.isGroup ? dto.groupName || `群聊（${participants.length}）` : participants.find((name) => name && name !== currentUser) || dto.groupName || dto.conversationId;
  return {
    id: dto.conversationId,
    title: displayName,
    unread: dto.unreadCount ?? 0,
    updatedAt: utils_time.formatDateTime(dto.updatedAt),
    preview: dto.lastMessagePreview || "",
    isGroup: dto.isGroup,
    participants,
    messages: []
  };
}
function transformMessages(list, currentUser) {
  return ensureArray(list).map((message) => ({
    id: message.id,
    sender: message.sender === currentUser ? "user" : "system",
    content: message.content,
    time: utils_time.formatTime(message.timestamp),
    type: message.type,
    caption: message.caption
  }));
}
function resolveVerificationStatus(result) {
  if (result.verificationPassed) {
    return "success";
  }
  if (result.hasReachedLinkLimit) {
    return "warning";
  }
  return "error";
}
function buildVerificationPayload(result) {
  var _a, _b;
  const stats = {
    attemptNumber: result.attemptNumber,
    remainingDownloads: result.remainingLinkQuota,
    expiresAt: result.expiresAt ? utils_time.formatDate(result.expiresAt) : void 0
  };
  const history = ensureArray(result.downloadHistory).map((item) => ({
    id: item.linkId,
    url: item.url,
    extractionCode: item.extractionCode,
    assignedAt: utils_time.formatDateTime(item.assignedAt),
    isNew: !!item.isNew
  }));
  return {
    status: resolveVerificationStatus(result),
    message: result.message,
    downloadUrl: (_a = result.download) == null ? void 0 : _a.url,
    extractionCode: (_b = result.download) == null ? void 0 : _b.extractionCode,
    stats,
    history
  };
}
let bootstrapPromise = null;
let redirectingToLogin = false;
const useAppStore = common_vendor.defineStore("app", () => {
  const loading = common_vendor.reactive({
    bootstrap: false,
    login: false,
    logout: false,
    user: false,
    software: false,
    dashboard: false,
    cards: false,
    cardTypes: false,
    agents: false,
    links: false,
    chat: false,
    chatMessages: false,
    chatContacts: false,
    verification: false,
    system: false,
    sales: false,
    settlementRates: false,
    saveSettlementRates: false,
    blacklistLogs: false,
    blacklistMachines: false
  });
  const token = common_vendor.ref(utils_storage.getTokenValue());
  const session = common_vendor.ref(null);
  const softwareList = common_vendor.ref([]);
  const selectedSoftware = common_vendor.ref(utils_storage.getSelectedSoftwareDisplayName() || "");
  const settlementAgents = common_vendor.ref([]);
  const selectedSettlementAgent = common_vendor.ref("");
  const settlementCycle = common_vendor.ref(null);
  const settlementBills = common_vendor.ref([]);
  const settlementHasReminder = common_vendor.ref(false);
  const theme = common_vendor.ref(utils_storage.getThemePreference());
  const dashboard = common_vendor.reactive({
    stats: [],
    trend: [],
    trendTotal: 0,
    trendMax: 0,
    subAgentTrend: { categories: [], series: [], total: 0 },
    announcements: [],
    usageHeatmap: [],
    salesResult: { count: 0, cards: [], settlements: [], totalAmount: 0 },
    salesFilters: { includeDescendants: true }
  });
  const recentCards = common_vendor.ref([]);
  const cardKeys = common_vendor.ref([]);
  const cardTotal = common_vendor.ref(0);
  const cardTypes = common_vendor.ref([]);
  const cardFilters = common_vendor.reactive({
    includeDescendants: true,
    searchType: 0,
    status: "",
    page: 1,
    limit: DEFAULT_CARD_PAGE_SIZE
  });
  const agents = common_vendor.ref([]);
  const agentTotal = common_vendor.ref(0);
  const agentFilters = common_vendor.reactive({ keyword: "", page: 1, limit: 50 });
  const linkRecords = common_vendor.ref([]);
  const linkTotal = common_vendor.ref(0);
  const chatSessions = common_vendor.ref([]);
  const chatContacts = common_vendor.ref([]);
  const verification = common_vendor.ref(null);
  const systemStatus = common_vendor.ref(null);
  const blacklistLogs = common_vendor.ref([]);
  const blacklistMachines = common_vendor.ref([]);
  const blacklistLatestBySoftware = common_vendor.reactive({});
  const blacklistLastSeenCache = common_vendor.reactive({});
  const settlementRateCache = common_vendor.reactive({});
  const cardTypeRequestCache = /* @__PURE__ */ new Map();
  const activeSettlementAgent = common_vendor.computed(() => {
    var _a, _b, _c, _d, _e, _f;
    const explicit = (_a = selectedSettlementAgent.value) == null ? void 0 : _a.trim();
    if (explicit) {
      return explicit;
    }
    const software = selectedSoftware.value;
    if (software) {
      const profile2 = (_c = (_b = session.value) == null ? void 0 : _b.softwareAgentInfo) == null ? void 0 : _c[software];
      const username = (_d = profile2 == null ? void 0 : profile2.user) == null ? void 0 : _d.trim();
      if (username) {
        return username;
      }
    }
    return ((_f = (_e = session.value) == null ? void 0 : _e.username) == null ? void 0 : _f.trim()) || "";
  });
  function buildSettlementCacheKey(software, agent) {
    const trimmedSoftware = (software ?? "").trim();
    const normalizedAgent = (agent ?? "").trim() || activeSettlementAgent.value || "anonymous";
    return `${trimmedSoftware}::${normalizedAgent}`;
  }
  const settlementRates = common_vendor.computed(() => {
    const software = selectedSoftware.value;
    if (!software) {
      return [];
    }
    const cacheKey = buildSettlementCacheKey(software, activeSettlementAgent.value);
    return settlementRateCache[cacheKey] ?? [];
  });
  const chatUnreadCount = common_vendor.computed(
    () => chatSessions.value.reduce((sum, item) => sum + (Number(item.unread) > 0 ? Number(item.unread) : 0), 0)
  );
  const hasNewBlacklistLogs = common_vendor.computed(() => {
    const software = selectedSoftware.value;
    if (!software) {
      return false;
    }
    const latest = blacklistLatestBySoftware[software];
    if (!latest) {
      return false;
    }
    const lastSeen = getStoredBlacklistTimestamp(software);
    if (!lastSeen) {
      return true;
    }
    return latest > lastSeen;
  });
  function getStoredBlacklistTimestamp(software) {
    if (!software) {
      return "";
    }
    if (Object.prototype.hasOwnProperty.call(blacklistLastSeenCache, software)) {
      return blacklistLastSeenCache[software] ?? "";
    }
    try {
      const stored = common_vendor.index.getStorageSync(buildBlacklistStorageKey(software));
      const normalized = normalizeStorageValue(stored);
      blacklistLastSeenCache[software] = normalized;
      return normalized;
    } catch (error) {
      common_vendor.index.__f__("warn", "at stores/app.ts:736", "Failed to read blacklist last seen timestamp", error);
      blacklistLastSeenCache[software] = "";
      return "";
    }
  }
  function setStoredBlacklistTimestamp(software, timestamp) {
    if (!software) {
      return;
    }
    try {
      if (!timestamp) {
        if (typeof common_vendor.index.removeStorageSync === "function") {
          common_vendor.index.removeStorageSync(buildBlacklistStorageKey(software));
        } else {
          common_vendor.index.setStorageSync(buildBlacklistStorageKey(software), "");
        }
      } else {
        common_vendor.index.setStorageSync(buildBlacklistStorageKey(software), timestamp);
      }
    } catch (error) {
      common_vendor.index.__f__("warn", "at stores/app.ts:757", "Failed to persist blacklist last seen timestamp", error);
    }
    blacklistLastSeenCache[software] = timestamp;
  }
  const lastLoadedCardTypeSoftware = common_vendor.ref("");
  const lastLoadedContactSoftware = common_vendor.ref("");
  common_vendor.watch(
    () => selectedSoftware.value,
    (software) => {
      const normalized = software == null ? void 0 : software.trim();
      if (!normalized) {
        cardTypes.value = [];
        lastLoadedCardTypeSoftware.value = "";
        return;
      }
      if (!token.value) {
        return;
      }
      void loadCardTypes().catch((error) => {
        common_vendor.index.__f__("warn", "at stores/app.ts:780", "Prefetch card types failed", error);
      });
    },
    { flush: "post" }
  );
  const profile = common_vendor.computed(() => {
    if (!session.value) {
      return null;
    }
    return {
      id: session.value.username,
      name: session.value.username,
      role: session.value.isSuper ? "超级管理员" : "代理用户",
      avatar: "",
      permissions: session.value.softwareList ?? []
    };
  });
  const isSuper = common_vendor.computed(() => {
    var _a;
    return ((_a = session.value) == null ? void 0 : _a.isSuper) ?? false;
  });
  function logoutLocal() {
    utils_storage.clearToken();
    token.value = null;
    session.value = null;
    softwareList.value = [];
    selectedSoftware.value = "";
    selectedSettlementAgent.value = "";
    settlementAgents.value = [];
    cardTypes.value = [];
    lastLoadedCardTypeSoftware.value = "";
    cardTypeRequestCache.clear();
    utils_storage.setSelectedSoftwareName();
    bootstrapPromise = null;
    Object.keys(settlementRateCache).forEach((key) => {
      delete settlementRateCache[key];
    });
  }
  function setTheme(next) {
    if (theme.value === next) {
      return;
    }
    theme.value = next;
    utils_storage.setThemePreference(next);
  }
  function redirectToLogin(options) {
    var _a;
    const message = options == null ? void 0 : options.message;
    const wasRedirecting = redirectingToLogin;
    logoutLocal();
    if (message && !wasRedirecting) {
      common_vendor.index.showToast({ title: message, icon: "none" });
    }
    const pagesGetter = typeof getCurrentPages === "function" ? getCurrentPages : void 0;
    const pages = pagesGetter ? pagesGetter() : [];
    const hasPages = Array.isArray(pages) && pages.length > 0;
    const currentRoute = hasPages ? ((_a = pages[pages.length - 1]) == null ? void 0 : _a.route) ?? "" : "";
    if (!hasPages || currentRoute === "pages/login/index") {
      redirectingToLogin = false;
      return;
    }
    if (redirectingToLogin) {
      return;
    }
    redirectingToLogin = true;
    const delay = message && !wasRedirecting ? 420 : 80;
    setTimeout(() => {
      common_vendor.index.reLaunch({ url: "/pages/login/index" });
      setTimeout(() => {
        redirectingToLogin = false;
      }, 240);
    }, delay);
  }
  function handleUnauthorized(message) {
    redirectToLogin({ message: message || "登录已过期，请重新登录" });
  }
  async function bootstrap(force = false) {
    if (!token.value) {
      redirectToLogin();
      return;
    }
    if (!force && session.value && softwareList.value.length) {
      return;
    }
    if (loading.bootstrap && bootstrapPromise) {
      return bootstrapPromise;
    }
    loading.bootstrap = true;
    common_api.refreshBaseURL();
    bootstrapPromise = (async () => {
      try {
        await loadCurrentUser();
        await loadSoftwareList();
      } catch (error) {
        common_vendor.index.__f__("error", "at stores/app.ts:886", "Bootstrap failed", error);
        redirectToLogin();
        throw error;
      } finally {
        loading.bootstrap = false;
        bootstrapPromise = null;
      }
    })();
    return bootstrapPromise;
  }
  async function ensureReady() {
    if (!token.value) {
      try {
        const platformStore = stores_platform.usePlatformStore();
        if (platformStore.isAuthenticated) {
          const binding = platformStore.selectedBinding || platformStore.bindings[0];
          if ((binding == null ? void 0 : binding.authorAccount) && (binding == null ? void 0 : binding.authorPassword) && !loading.login) {
            await login(
              { username: binding.authorAccount, password: binding.authorPassword },
              { skipBootstrap: true }
            );
          }
        }
      } catch (error) {
        common_vendor.index.__f__("warn", "at stores/app.ts:912", "Auto login via platform binding failed", error);
      }
    }
    if (!token.value) {
      redirectToLogin();
      return false;
    }
    if (!session.value || !softwareList.value.length) {
      await bootstrap();
    }
    return !!session.value;
  }
  async function loadCurrentUser() {
    if (!token.value) {
      session.value = null;
      return null;
    }
    loading.user = true;
    try {
      const data = await common_api.apiRequest({
        url: "/api/Auth/getUserInfo",
        method: "POST",
        auth: true,
        disableMock: true
      });
      session.value = data;
      return data;
    } catch (error) {
      session.value = null;
      throw error;
    } finally {
      loading.user = false;
    }
  }
  async function loadSoftwareList() {
    if (!token.value) {
      softwareList.value = [];
      selectedSoftware.value = "";
      return [];
    }
    loading.software = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/Software/GetSoftwareList",
        method: "POST",
        auth: true,
        disableMock: true
      });
      const items = ensureArray(response == null ? void 0 : response.softwares);
      softwareList.value = items;
      const stored = utils_storage.getSelectedSoftwareDisplayName() || utils_storage.getSelectedSoftwareName();
      const matched = items.find((item) => normalizeSoftwareName(item.softwareName) === normalizeSoftwareName(stored));
      if (matched) {
        selectedSoftware.value = matched.softwareName;
        utils_storage.setSelectedSoftwareDisplayName(matched.softwareName);
        selectedSettlementAgent.value = "";
        settlementAgents.value = [];
      } else if (items.length) {
        selectedSoftware.value = items[0].softwareName;
        utils_storage.setSelectedSoftwareDisplayName(items[0].softwareName);
        selectedSettlementAgent.value = "";
        settlementAgents.value = [];
      } else {
        selectedSoftware.value = "";
        utils_storage.clearSelectedSoftwareDisplayName();
        selectedSettlementAgent.value = "";
        settlementAgents.value = [];
      }
      return items;
    } finally {
      loading.software = false;
    }
  }
  function setSelectedSoftware(name) {
    const normalized = normalizeSoftwareName(name);
    const match = softwareList.value.find((item) => normalizeSoftwareName(item.softwareName) === normalized);
    if (!match) {
      return;
    }
    selectedSoftware.value = match.softwareName;
    utils_storage.setSelectedSoftwareDisplayName(match.softwareName);
    selectedSettlementAgent.value = "";
    settlementAgents.value = [];
  }
  function setSelectedSettlementAgent(username) {
    selectedSettlementAgent.value = (username ?? "").trim();
  }
  async function login(payload, options) {
    loading.login = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/Auth/login",
        method: "POST",
        data: payload,
        disableMock: true
      });
      utils_storage.setTokenValue(response.token);
      token.value = response.token;
      if (!(options == null ? void 0 : options.skipBootstrap)) {
        await bootstrap(true);
      }
      return response;
    } finally {
      loading.login = false;
    }
  }
  async function logout() {
    if (loading.logout) {
      return;
    }
    loading.logout = true;
    try {
      if (token.value) {
        await common_api.apiRequest({
          url: "/api/Auth/logout",
          method: "POST",
          auth: true,
          disableMock: true
        });
      }
    } catch (error) {
      common_vendor.index.__f__("warn", "at stores/app.ts:1041", "Logout request failed", error);
    } finally {
      loading.logout = false;
      redirectToLogin({ message: "已退出登录" });
    }
  }
  async function loadDashboard() {
    var _a;
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      dashboard.stats = [];
      dashboard.trend = [];
      dashboard.announcements = [];
      dashboard.usageHeatmap = [];
      recentCards.value = [];
      return;
    }
    loading.dashboard = true;
    try {
      const [agentInfo, trend, subTrend, usage, cardsResponse, announcement, system] = await Promise.all([
        common_api.apiRequest({
          url: "/api/Agent/getUserInfo",
          method: "POST",
          data: { software },
          auth: true,
          disableMock: true
        }),
        common_api.apiRequest({
          url: "/api/Card/getRecentActivationTrend",
          method: "POST",
          data: { software, OnlyDescendants: false },
          auth: true,
          disableMock: true
        }),
        common_api.apiRequest({
          url: "/api/Card/getRecentActivationTrend",
          method: "POST",
          data: { software, OnlyDescendants: true },
          auth: true,
          disableMock: true
        }),
        common_api.apiRequest({
          url: "/api/Card/getUsageDistribution",
          method: "POST",
          data: { software, includeDescendants: true },
          auth: true,
          disableMock: true
        }),
        common_api.apiRequest({
          url: "/api/Card/getCardList",
          method: "POST",
          data: { software, page: 1, limit: 8, includeDescendants: true },
          auth: true,
          disableMock: true
        }),
        common_api.apiRequest({
          url: "/api/System/announcement",
          method: "GET",
          auth: true,
          disableMock: true
        }).catch(() => null),
        ((_a = session.value) == null ? void 0 : _a.isSuper) ? common_api.apiRequest({
          url: "/api/System/status",
          method: "GET",
          auth: true,
          disableMock: true
        }).catch(() => null) : Promise.resolve(null)
      ]);
      dashboard.stats = buildDashboardStats(agentInfo == null ? void 0 : agentInfo.statistics);
      const totalTrend = buildTrendData(trend);
      dashboard.trend = totalTrend;
      const trendMetrics = computeTrendMetrics(totalTrend);
      dashboard.trendTotal = trendMetrics.total;
      dashboard.trendMax = trendMetrics.max;
      const descendantTrend = buildSubAgentTrendPayload(subTrend);
      dashboard.subAgentTrend = descendantTrend;
      dashboard.announcements = buildAnnouncementsPayload(announcement);
      dashboard.usageHeatmap = buildHeatmapPayload(usage);
      const cards = ensureArray(cardsResponse == null ? void 0 : cardsResponse.data);
      recentCards.value = cards.map(transformCard);
      if (system) {
        systemStatus.value = system;
      }
    } finally {
      loading.dashboard = false;
    }
  }
  async function loadCardKeys(filters = {}) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      cardKeys.value = [];
      cardTotal.value = 0;
      return;
    }
    const nextFilters = {
      ...cardFilters,
      ...filters
    };
    nextFilters.page = nextFilters.page && nextFilters.page > 0 ? nextFilters.page : 1;
    nextFilters.limit = nextFilters.limit && nextFilters.limit > 0 ? nextFilters.limit : DEFAULT_CARD_PAGE_SIZE;
    Object.assign(cardFilters, nextFilters);
    const page = cardFilters.page ?? 1;
    const limit = cardFilters.limit ?? DEFAULT_CARD_PAGE_SIZE;
    loading.cards = true;
    try {
      const payload = buildCardListPayload(software, cardFilters, page, limit);
      const response = await common_api.apiRequest({
        url: "/api/Card/getCardList",
        method: "POST",
        data: payload,
        auth: true,
        disableMock: true
      });
      const items = ensureArray(response == null ? void 0 : response.data);
      cardKeys.value = items.map(transformCard);
      cardTotal.value = Number((response == null ? void 0 : response.total) ?? items.length ?? 0);
    } finally {
      loading.cards = false;
    }
  }
  async function loadCardTypes(force = false) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      cardTypes.value = [];
      lastLoadedCardTypeSoftware.value = "";
      return [];
    }
    if (!force && cardTypes.value.length > 0 && lastLoadedCardTypeSoftware.value === software) {
      return cardTypes.value;
    }
    if (!force && cardTypeRequestCache.has(software)) {
      return cardTypeRequestCache.get(software);
    }
    if (force) {
      cardTypeRequestCache.delete(software);
    }
    const request = (async () => {
      loading.cardTypes = true;
      try {
        const response = await common_api.apiRequest({
          url: "/api/CardType/getCardTypeList",
          method: "POST",
          data: { software },
          auth: true,
          disableMock: true
        });
        const items = ensureArray(response == null ? void 0 : response.items);
        cardTypes.value = items.map((item) => ({
          name: (item == null ? void 0 : item.name) ?? "",
          prefix: (item == null ? void 0 : item.prefix) ?? void 0,
          duration: Number((item == null ? void 0 : item.duration) ?? 0),
          price: Number((item == null ? void 0 : item.price) ?? 0),
          remarks: (item == null ? void 0 : item.remarks) ?? void 0
        }));
        lastLoadedCardTypeSoftware.value = software;
        return cardTypes.value;
      } finally {
        loading.cardTypes = false;
        cardTypeRequestCache.delete(software);
      }
    })();
    cardTypeRequestCache.set(software, request);
    return request;
  }
  async function loadSettlementRates(force = false) {
    var _a, _b, _c;
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      return [];
    }
    const targetAgent = ((_a = selectedSettlementAgent.value) == null ? void 0 : _a.trim()) || "";
    const cacheKey = buildSettlementCacheKey(software, targetAgent || activeSettlementAgent.value);
    if (!force && settlementRateCache[cacheKey]) {
      return settlementRateCache[cacheKey];
    }
    loading.settlementRates = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/Settlement/list",
        method: "POST",
        data: { software, targetAgent: targetAgent || void 0 },
        auth: true,
        disableMock: true
      });
      const responseTarget = ((response == null ? void 0 : response.targetAgent) ?? targetAgent ?? "").toString().trim();
      const options = ensureArray(response == null ? void 0 : response.agents).map((item) => ({
        username: ((item == null ? void 0 : item.username) ?? "").toString().trim(),
        displayName: ((item == null ? void 0 : item.displayName) ?? "").toString().trim(),
        hasPendingReminder: Boolean(item == null ? void 0 : item.hasPendingReminder)
      })).filter((item) => item.username.length > 0);
      if (!options.length) {
        const fallback = activeSettlementAgent.value || ((_c = (_b = session.value) == null ? void 0 : _b.username) == null ? void 0 : _c.trim()) || "";
        if (fallback) {
          options.push({ username: fallback, displayName: `${fallback} · 当前账号` });
        }
      }
      settlementAgents.value = options;
      if (responseTarget !== selectedSettlementAgent.value) {
        selectedSettlementAgent.value = responseTarget;
      }
      settlementCycle.value = (response == null ? void 0 : response.cycle) ?? null;
      settlementBills.value = ensureArray(response == null ? void 0 : response.bills).map(normalizeSettlementBill);
      settlementHasReminder.value = Boolean(response == null ? void 0 : response.hasPendingReminder);
      const items = ensureArray(response == null ? void 0 : response.rates).map((item) => ({
        cardType: ((item == null ? void 0 : item.cardType) ?? "").toString().trim(),
        price: Number((item == null ? void 0 : item.price) ?? 0) || 0
      })).filter((item) => item.cardType);
      settlementRateCache[cacheKey] = items;
      return settlementRateCache[cacheKey];
    } finally {
      loading.settlementRates = false;
    }
  }
  async function saveSettlementRates(rates, cycleDays, cycleTimeMinutes) {
    var _a;
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const normalized = ensureArray(rates).map((item) => ({
      cardType: ((item == null ? void 0 : item.cardType) ?? "").toString().trim(),
      price: Number((item == null ? void 0 : item.price) ?? 0) || 0
    })).filter((item) => item.cardType);
    const targetAgent = ((_a = selectedSettlementAgent.value) == null ? void 0 : _a.trim()) || "";
    loading.saveSettlementRates = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/Settlement/upsert",
        method: "POST",
        data: {
          software,
          rates: normalized,
          targetAgent: targetAgent || void 0,
          cycleDays: typeof cycleDays === "number" ? cycleDays : void 0,
          cycleTimeMinutes: typeof cycleTimeMinutes === "number" && !Number.isNaN(cycleTimeMinutes) ? cycleTimeMinutes : void 0
        },
        auth: true,
        disableMock: true
      });
      const responseTarget = ((response == null ? void 0 : response.targetAgent) ?? targetAgent ?? "").toString().trim();
      if (responseTarget !== selectedSettlementAgent.value) {
        selectedSettlementAgent.value = responseTarget;
      }
      const options = ensureArray(response == null ? void 0 : response.agents).map((item) => ({
        username: ((item == null ? void 0 : item.username) ?? "").toString().trim(),
        displayName: ((item == null ? void 0 : item.displayName) ?? "").toString().trim(),
        hasPendingReminder: Boolean(item == null ? void 0 : item.hasPendingReminder)
      })).filter((item) => item.username.length > 0);
      if (options.length) {
        settlementAgents.value = options;
      }
      settlementCycle.value = (response == null ? void 0 : response.cycle) ?? settlementCycle.value;
      settlementBills.value = ensureArray(response == null ? void 0 : response.bills).map(normalizeSettlementBill);
      settlementHasReminder.value = Boolean(response == null ? void 0 : response.hasPendingReminder);
      const saved = ensureArray(response == null ? void 0 : response.rates).map((item) => ({
        cardType: ((item == null ? void 0 : item.cardType) ?? "").toString().trim(),
        price: Number((item == null ? void 0 : item.price) ?? 0) || 0
      })).filter((item) => item.cardType);
      const cacheKey = buildSettlementCacheKey(software, responseTarget || activeSettlementAgent.value);
      settlementRateCache[cacheKey] = saved;
      return settlementRateCache[cacheKey];
    } finally {
      loading.saveSettlementRates = false;
    }
  }
  async function completeSettlementBill(billId, amount, note) {
    var _a;
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const targetAgent = ((_a = selectedSettlementAgent.value) == null ? void 0 : _a.trim()) || "";
    const response = await common_api.apiRequest({
      url: "/api/Settlement/bill/complete",
      method: "POST",
      data: {
        software,
        billId,
        amount,
        note,
        targetAgent: targetAgent || void 0
      },
      auth: true,
      disableMock: true
    });
    settlementCycle.value = (response == null ? void 0 : response.cycle) ?? settlementCycle.value;
    settlementBills.value = ensureArray(response == null ? void 0 : response.bills).map(normalizeSettlementBill);
    settlementHasReminder.value = Boolean(response == null ? void 0 : response.hasPendingReminder);
    return settlementBills.value;
  }
  async function generateCards(payload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const data = {
      software,
      cardType: payload.cardType,
      quantity: payload.quantity,
      remarks: payload.remarks ?? "",
      ...payload.customPrefix ? { customPrefix: payload.customPrefix } : {}
    };
    const response = await common_api.apiRequest({
      url: "/api/Card/generateCards",
      method: "POST",
      data,
      auth: true,
      disableMock: true
    });
    await loadCardKeys({ page: cardFilters.page, limit: cardFilters.limit });
    return response;
  }
  async function updateCardStatus(mutation) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const actionMap = {
      enable: "enableCard",
      disable: "disableCard",
      unban: "enableCardWithBanTimeReturn"
    };
    const action = actionMap[mutation.action];
    if (!action) {
      throw new Error("不支持的操作");
    }
    const result = await common_api.apiRequest({
      url: `/api/Card/${action}`,
      method: "POST",
      data: { software, cardKey: mutation.cardKey },
      auth: true,
      disableMock: true
    });
    await loadCardKeys({ page: cardFilters.page, limit: cardFilters.limit });
    return (result == null ? void 0 : result.message) ?? "";
  }
  async function unbindCard(cardKey) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const result = await common_api.apiRequest({
      url: "/api/Card/unbindCard",
      method: "POST",
      data: { software, cardKey },
      auth: true,
      disableMock: true
    });
    await loadCardKeys({ page: cardFilters.page, limit: cardFilters.limit });
    return (result == null ? void 0 : result.message) ?? "";
  }
  async function loadAgents(options = {}) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      agents.value = [];
      agentTotal.value = 0;
      return;
    }
    const nextFilters = {
      ...agentFilters,
      ...options
    };
    nextFilters.page = nextFilters.page && nextFilters.page > 0 ? nextFilters.page : 1;
    nextFilters.limit = nextFilters.limit && nextFilters.limit > 0 ? nextFilters.limit : 50;
    nextFilters.keyword = (nextFilters.keyword ?? "").trim();
    Object.assign(agentFilters, nextFilters);
    const page = agentFilters.page ?? 1;
    const limit = agentFilters.limit ?? 50;
    const keyword = (agentFilters.keyword ?? "").trim();
    loading.agents = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/Agent/getSubAgentList",
        method: "POST",
        data: { software, page, limit, keyword, searchType: 1 },
        auth: true,
        disableMock: true
      });
      const items = ensureArray(response == null ? void 0 : response.data);
      agents.value = items.map(transformAgent);
      agentTotal.value = Number((response == null ? void 0 : response.total) ?? items.length ?? 0);
    } finally {
      loading.agents = false;
    }
  }
  async function toggleAgentStatus(payload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const usernames = ensureArray(payload.usernames).map((item) => item.trim()).filter((item) => item.length > 0);
    if (!usernames.length) {
      throw new Error("请选择代理");
    }
    const path = payload.enable ? "enableAgent" : "disableAgent";
    const result = await common_api.apiRequest({
      url: `/api/Agent/${path}`,
      method: "POST",
      data: { software, username: usernames },
      auth: true,
      disableMock: true
    });
    await loadAgents({ page: agentFilters.page, limit: agentFilters.limit, keyword: agentFilters.keyword });
    return (result == null ? void 0 : result.message) ?? "";
  }
  async function deleteAgents(usernames) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const targets = ensureArray(usernames).map((item) => item.trim()).filter((item) => item.length > 0);
    if (!targets.length) {
      throw new Error("请选择代理");
    }
    const result = await common_api.apiRequest({
      url: "/api/Agent/deleteSubAgent",
      method: "POST",
      data: { software, username: targets },
      auth: true,
      disableMock: true
    });
    await loadAgents({ page: agentFilters.page, limit: agentFilters.limit, keyword: agentFilters.keyword });
    return (result == null ? void 0 : result.message) ?? "";
  }
  async function createAgent(payload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const data = {
      software,
      username: payload.username,
      password: payload.password,
      initialBalance: Number(payload.balance ?? 0),
      initialTimeStock: Number(payload.timeStock ?? 0),
      parities: Number(payload.parities ?? 100),
      totalParities: Number(payload.totalParities ?? payload.parities ?? 100),
      remark: payload.remarks ?? "",
      cardTypes: ensureArray(payload.cardTypes)
    };
    const result = await common_api.apiRequest({
      url: "/api/Agent/createSubAgent",
      method: "POST",
      data,
      auth: true,
      disableMock: true
    });
    await loadAgents({ page: 1, limit: agentFilters.limit, keyword: agentFilters.keyword });
    return (result == null ? void 0 : result.message) ?? "";
  }
  async function adjustAgentBalance(payload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const data = {
      software,
      username: payload.username,
      balance: Number(payload.balance ?? 0),
      timeStock: Number(payload.timeStock ?? 0)
    };
    const result = await common_api.apiRequest({
      url: "/api/Agent/addMoney",
      method: "POST",
      data,
      auth: true,
      disableMock: true
    });
    await loadAgents({ page: agentFilters.page, limit: agentFilters.limit, keyword: agentFilters.keyword });
    return (result == null ? void 0 : result.message) ?? "";
  }
  async function updateAgentRemark(payload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const result = await common_api.apiRequest({
      url: "/api/Agent/updateAgentRemark",
      method: "POST",
      data: { software, username: payload.username, remark: payload.remark ?? "" },
      auth: true,
      disableMock: true
    });
    await loadAgents({ page: agentFilters.page, limit: agentFilters.limit, keyword: agentFilters.keyword });
    return (result == null ? void 0 : result.message) ?? "";
  }
  async function updateAgentPassword(payload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const result = await common_api.apiRequest({
      url: "/api/Agent/updateAgentPassword",
      method: "POST",
      data: { software, username: payload.username, newPassword: payload.password },
      auth: true,
      disableMock: true
    });
    return (result == null ? void 0 : result.message) ?? "";
  }
  async function assignAgentCardTypes(payload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const result = await common_api.apiRequest({
      url: "/api/Agent/setAgentCardType",
      method: "POST",
      data: { software, username: payload.username, cardTypes: ensureArray(payload.cardTypes) },
      auth: true,
      disableMock: true
    });
    return (result == null ? void 0 : result.message) ?? "";
  }
  async function loadAgentCardTypes(username) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const response = await common_api.apiRequest({
      url: "/api/Agent/getAgentCardType",
      method: "POST",
      data: { software, username },
      auth: true,
      disableMock: true
    });
    return ensureArray(response == null ? void 0 : response.cardTypes);
  }
  async function runSalesQuery(filters = {}) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    loading.sales = true;
    try {
      const payload = {
        software,
        includeDescendants: filters.includeDescendants ?? true
      };
      if (filters.cardType) {
        payload.cardTypes = [filters.cardType];
      }
      if (filters.status) {
        payload.status = filters.status;
      }
      if (filters.agent) {
        payload.whomList = [filters.agent];
      }
      if (filters.startTime) {
        payload.startTime = filters.startTime;
      }
      if (filters.endTime) {
        payload.endTime = filters.endTime;
      }
      const response = await common_api.apiRequest({
        url: "/api/Card/countActivatedCards",
        method: "POST",
        data: payload,
        auth: true,
        disableMock: true
      });
      const normalizedFilters = {
        includeDescendants: filters.includeDescendants ?? true,
        cardType: filters.cardType,
        status: filters.status,
        agent: filters.agent,
        startTime: filters.startTime,
        endTime: filters.endTime
      };
      dashboard.salesFilters = normalizedFilters;
      dashboard.salesResult = transformSalesResult(response);
      return dashboard.salesResult;
    } finally {
      loading.sales = false;
    }
  }
  function clearSalesResult(nextFilters) {
    dashboard.salesResult = { count: 0, cards: [], settlements: [], totalAmount: 0 };
    dashboard.salesFilters = {
      includeDescendants: (nextFilters == null ? void 0 : nextFilters.includeDescendants) ?? true,
      cardType: nextFilters == null ? void 0 : nextFilters.cardType,
      status: nextFilters == null ? void 0 : nextFilters.status,
      agent: nextFilters == null ? void 0 : nextFilters.agent,
      startTime: nextFilters == null ? void 0 : nextFilters.startTime,
      endTime: nextFilters == null ? void 0 : nextFilters.endTime
    };
  }
  async function loadBlacklistLogs(limit = 200) {
    var _a;
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      blacklistLogs.value = [];
      return [];
    }
    loading.blacklistLogs = true;
    try {
      const response = await common_api.apiRequest({
        url: `/api/Blacklist/logs?limit=${encodeURIComponent(limit)}`,
        method: "GET",
        auth: true,
        disableMock: true
      });
      const items = ensureArray(response == null ? void 0 : response.items);
      const filtered = items.filter((item) => matchesSoftware((item == null ? void 0 : item.software) ?? (item == null ? void 0 : item.Software), software)).map(transformBlacklistLog);
      blacklistLogs.value = filtered;
      const latestRaw = filtered.length > 0 ? ((_a = filtered[0]) == null ? void 0 : _a.timestamp) ?? "" : "";
      const normalizedLatest = normalizeStorageValue(latestRaw);
      blacklistLatestBySoftware[software] = normalizedLatest && normalizedLatest !== "-" ? normalizedLatest : "";
      return filtered;
    } finally {
      loading.blacklistLogs = false;
    }
  }
  function markBlacklistLogsSeen(software) {
    const target = software ?? selectedSoftware.value;
    if (!target) {
      return;
    }
    const latest = blacklistLatestBySoftware[target] ?? "";
    if (!latest) {
      setStoredBlacklistTimestamp(target, "");
      return;
    }
    setStoredBlacklistTimestamp(target, latest);
  }
  async function loadBlacklistMachines() {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      blacklistMachines.value = [];
      return [];
    }
    loading.blacklistMachines = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/Blacklist/machines",
        method: "GET",
        auth: true,
        disableMock: true
      });
      const items = ensureArray(response == null ? void 0 : response.items);
      const filtered = items.filter((item) => matchesSoftware((item == null ? void 0 : item.software) ?? (item == null ? void 0 : item.Software), software)).map(transformBlacklistMachine);
      blacklistMachines.value = filtered;
      return filtered;
    } finally {
      loading.blacklistMachines = false;
    }
  }
  async function createBlacklistMachine(payload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    await common_api.apiRequest({
      url: "/api/Blacklist/machines",
      method: "POST",
      data: { software, value: payload.value, type: payload.type, remarks: payload.remarks ?? "" },
      auth: true,
      disableMock: true
    });
    return loadBlacklistMachines();
  }
  async function deleteBlacklistMachines(values) {
    await ensureReady();
    const targets = ensureArray(values).map((item) => item.trim()).filter((item) => item.length > 0);
    if (!targets.length) {
      throw new Error("请选择要删除的记录");
    }
    await common_api.apiRequest({
      url: "/api/Blacklist/machines/delete",
      method: "POST",
      data: { values: targets },
      auth: true,
      disableMock: true
    });
    return loadBlacklistMachines();
  }
  async function loadLinkRecords(options = {}) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const page = options.page ?? 1;
    const limit = options.limit ?? DEFAULT_LINK_PAGE_SIZE;
    loading.links = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/LinkAudit/listLanzouLinks",
        method: "POST",
        data: { page, limit, software },
        auth: true,
        disableMock: true
      });
      const items = ensureArray(response == null ? void 0 : response.items);
      linkRecords.value = items.map(transformLink);
      linkTotal.value = Number((response == null ? void 0 : response.total) ?? items.length ?? 0);
    } finally {
      loading.links = false;
    }
  }
  async function loadChatContacts(force = false) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      chatContacts.value = [];
      lastLoadedContactSoftware.value = "";
      return [];
    }
    if (!force && chatContacts.value.length > 0 && lastLoadedContactSoftware.value === software) {
      return chatContacts.value;
    }
    loading.chatContacts = true;
    try {
      const response = await common_api.apiRequest({
        url: `/api/Chat/contacts?software=${encodeURIComponent(software)}`,
        method: "GET",
        auth: true,
        disableMock: true
      });
      chatContacts.value = ensureArray(response).map(transformContact);
      lastLoadedContactSoftware.value = software;
      return chatContacts.value;
    } finally {
      loading.chatContacts = false;
    }
  }
  async function loadChatSessions() {
    var _a;
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      chatSessions.value = [];
      return;
    }
    loading.chat = true;
    try {
      const response = await common_api.apiRequest({
        url: `/api/Chat/conversations?software=${encodeURIComponent(software)}`,
        method: "GET",
        auth: true,
        disableMock: true
      });
      const currentUser = ((_a = session.value) == null ? void 0 : _a.username) ?? "";
      chatSessions.value = ensureArray(response).map((conversation) => transformConversation(conversation, currentUser));
    } finally {
      loading.chat = false;
    }
  }
  async function loadChatMessages(conversationId) {
    var _a, _b, _c, _d;
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software || !conversationId) {
      return [];
    }
    loading.chatMessages = true;
    try {
      const response = await common_api.apiRequest({
        url: `/api/Chat/messages?software=${encodeURIComponent(software)}&conversationId=${encodeURIComponent(conversationId)}`,
        method: "GET",
        auth: true,
        disableMock: true
      });
      const currentUser = ((_a = session.value) == null ? void 0 : _a.username) ?? "";
      const messages = transformMessages((response == null ? void 0 : response.messages) ?? [], currentUser);
      const target = chatSessions.value.find((sessionItem) => sessionItem.id === conversationId);
      if (target) {
        target.messages = messages;
        target.preview = ((_b = response == null ? void 0 : response.conversation) == null ? void 0 : _b.lastMessagePreview) || target.preview;
        target.updatedAt = utils_time.formatDateTime(((_c = response == null ? void 0 : response.conversation) == null ? void 0 : _c.updatedAt) || target.updatedAt);
        target.unread = ((_d = response == null ? void 0 : response.conversation) == null ? void 0 : _d.unreadCount) ?? target.unread;
      }
      return messages;
    } finally {
      loading.chatMessages = false;
    }
  }
  async function sendChatMessage(conversationId, message, options = {}) {
    var _a, _b, _c, _d;
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software || !conversationId) {
      return null;
    }
    const type = options.type ?? "text";
    const trimmedMessage = (message ?? "").toString().trim();
    const caption = (options.caption ?? "").toString().trim();
    if (type === "text" && !trimmedMessage) {
      return null;
    }
    if (type === "image" && !options.mediaBase64) {
      throw new Error("缺少图片数据");
    }
    const payload = {
      software,
      conversationId,
      message: type === "image" ? caption : trimmedMessage
    };
    if (type && type !== "text") {
      payload.messageType = type;
    }
    if (options.mediaBase64) {
      payload.mediaBase64 = options.mediaBase64;
    }
    if (options.mediaName) {
      payload.mediaName = options.mediaName;
    }
    const response = await common_api.apiRequest({
      url: "/api/Chat/send",
      method: "POST",
      data: payload,
      auth: true,
      disableMock: true
    });
    const currentUser = ((_a = session.value) == null ? void 0 : _a.username) ?? "";
    const target = chatSessions.value.find((sessionItem) => sessionItem.id === conversationId);
    if (target) {
      target.messages = transformMessages((response == null ? void 0 : response.messages) ?? [], currentUser);
      target.preview = ((_b = response == null ? void 0 : response.conversation) == null ? void 0 : _b.lastMessagePreview) || target.preview;
      target.updatedAt = utils_time.formatDateTime(((_c = response == null ? void 0 : response.conversation) == null ? void 0 : _c.updatedAt) || target.updatedAt);
      target.unread = ((_d = response == null ? void 0 : response.conversation) == null ? void 0 : _d.unreadCount) ?? target.unread;
    }
    return response;
  }
  async function createDirectConversation(targetUser, message) {
    var _a;
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    if (!targetUser) {
      throw new Error("请选择聊天对象");
    }
    if (!message.trim()) {
      throw new Error("请输入消息内容");
    }
    const payload = {
      software,
      targetUser,
      message: message.trim()
    };
    const response = await common_api.apiRequest({
      url: "/api/Chat/send",
      method: "POST",
      data: payload,
      auth: true,
      disableMock: true
    });
    const currentUser = ((_a = session.value) == null ? void 0 : _a.username) ?? "";
    const conversation = transformConversation(response == null ? void 0 : response.conversation, currentUser);
    conversation.messages = transformMessages((response == null ? void 0 : response.messages) ?? [], currentUser);
    const existingIndex = chatSessions.value.findIndex((item) => item.id === conversation.id);
    if (existingIndex >= 0) {
      chatSessions.value.splice(existingIndex, 1, conversation);
    } else {
      chatSessions.value.unshift(conversation);
    }
    return conversation;
  }
  async function createGroupConversation(groupName, members, initialMessage) {
    var _a, _b;
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    const name = groupName.trim();
    if (!name) {
      throw new Error("请输入群聊名称");
    }
    const participantList = ensureArray(members).map((item) => item.trim()).filter((item) => item.length > 0);
    if (!participantList.length) {
      throw new Error("请至少选择一位成员");
    }
    const response = await common_api.apiRequest({
      url: "/api/Chat/groups",
      method: "POST",
      data: { software, name, participants: participantList },
      auth: true,
      disableMock: true
    });
    const currentUser = ((_a = session.value) == null ? void 0 : _a.username) ?? "";
    const conversation = transformConversation(response, currentUser);
    const existingIndex = chatSessions.value.findIndex((item) => item.id === conversation.id);
    if (existingIndex >= 0) {
      chatSessions.value.splice(existingIndex, 1, conversation);
    } else {
      chatSessions.value.unshift(conversation);
    }
    if ((initialMessage == null ? void 0 : initialMessage.content) || ((_b = initialMessage == null ? void 0 : initialMessage.options) == null ? void 0 : _b.mediaBase64)) {
      try {
        await sendChatMessage(conversation.id, initialMessage.content ?? "", initialMessage.options ?? {});
      } catch (error) {
        common_vendor.index.__f__("warn", "at stores/app.ts:2119", "createGroupConversation initial message failed", error);
      }
    }
    return conversation;
  }
  async function exportChatHistory(conversationId) {
    var _a, _b, _c, _d;
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error("请选择软件位");
    }
    if (!conversationId) {
      throw new Error("请选择会话");
    }
    const response = await common_api.apiRequest({
      url: `/api/Chat/messages?software=${encodeURIComponent(software)}&conversationId=${encodeURIComponent(conversationId)}&limit=500`,
      method: "GET",
      auth: true,
      disableMock: true
    });
    const currentUser = ((_a = session.value) == null ? void 0 : _a.username) ?? "";
    const target = chatSessions.value.find((sessionItem) => sessionItem.id === conversationId);
    if (target) {
      target.messages = transformMessages((response == null ? void 0 : response.messages) ?? [], currentUser);
      target.preview = ((_b = response == null ? void 0 : response.conversation) == null ? void 0 : _b.lastMessagePreview) || target.preview;
      target.updatedAt = utils_time.formatDateTime(((_c = response == null ? void 0 : response.conversation) == null ? void 0 : _c.updatedAt) || target.updatedAt);
      target.unread = ((_d = response == null ? void 0 : response.conversation) == null ? void 0 : _d.unreadCount) ?? target.unread;
    }
    const conversation = response == null ? void 0 : response.conversation;
    const participants = ensureArray(conversation == null ? void 0 : conversation.participants);
    const title = (conversation == null ? void 0 : conversation.isGroup) ? (conversation == null ? void 0 : conversation.groupName) || "群聊" : participants.find((name) => name && name !== currentUser) || participants[0] || conversationId;
    const lines = [];
    lines.push(`会话：${title}`);
    lines.push(`成员：${participants.join("、")}`);
    lines.push(`导出时间：${utils_time.formatDateTime((/* @__PURE__ */ new Date()).toISOString())}`);
    lines.push("");
    ensureArray(response == null ? void 0 : response.messages).forEach((message) => {
      const timestamp = utils_time.formatDateTime(message.timestamp);
      const sender = message.sender === currentUser ? `${message.sender}(我)` : message.sender;
      const content = message.type === "image" ? `[图片] ${message.content}` : message.content;
      lines.push(`[${timestamp}] ${sender}: ${content}`.trim());
      if (message.caption) {
        lines.push(`  说明：${message.caption}`);
      }
    });
    const filename = `${title || "chat"}-${Date.now()}.txt`;
    return {
      filename,
      content: lines.join("\n")
    };
  }
  async function loadVerification(cardKey, context) {
    loading.verification = true;
    try {
      if (!cardKey) {
        verification.value = null;
        return null;
      }
      if (!context || !context.software) {
        throw new Error("缺少软件位信息，无法验证卡密");
      }
      if (!context.softwareCode) {
        throw new Error("缺少软件码信息，无法验证卡密");
      }
      const response = await common_api.apiRequest({
        url: "/api/card-verification/verify",
        method: "POST",
        data: {
          cardKey,
          software: context.software,
          softwareCode: context.softwareCode,
          agentAccount: context.agentAccount
        },
        disableMock: true,
        skipProxy: true,
        auth: false
      });
      verification.value = buildVerificationPayload(response);
      return verification.value;
    } finally {
      loading.verification = false;
    }
  }
  return {
    loading,
    token,
    session,
    profile,
    isSuper,
    softwareList,
    selectedSoftware,
    theme,
    dashboard,
    recentCards,
    cardKeys,
    cardTotal,
    cardTypes,
    cardFilters,
    agents,
    agentTotal,
    agentFilters,
    linkRecords,
    linkTotal,
    chatSessions,
    chatContacts,
    chatUnreadCount,
    settlementRates,
    settlementAgents,
    selectedSettlementAgent,
    activeSettlementAgent,
    settlementCycle,
    settlementBills,
    settlementHasReminder,
    verification,
    systemStatus,
    blacklistLogs,
    blacklistMachines,
    hasNewBlacklistLogs,
    markBlacklistLogsSeen,
    ensureReady,
    bootstrap,
    login,
    logout,
    handleUnauthorized,
    setTheme,
    setSelectedSoftware,
    setSelectedSettlementAgent,
    loadDashboard,
    loadCardKeys,
    loadCardTypes,
    loadAgents,
    loadSettlementRates,
    saveSettlementRates,
    completeSettlementBill,
    updateCardStatus,
    unbindCard,
    toggleAgentStatus,
    deleteAgents,
    createAgent,
    adjustAgentBalance,
    updateAgentRemark,
    updateAgentPassword,
    assignAgentCardTypes,
    loadAgentCardTypes,
    runSalesQuery,
    clearSalesResult,
    loadBlacklistLogs,
    loadBlacklistMachines,
    createBlacklistMachine,
    deleteBlacklistMachines,
    loadLinkRecords,
    loadChatContacts,
    loadChatSessions,
    loadChatMessages,
    sendChatMessage,
    createDirectConversation,
    createGroupConversation,
    exportChatHistory,
    generateCards,
    loadVerification
  };
});
exports.useAppStore = useAppStore;
//# sourceMappingURL=../../.sourcemap/mp-weixin/stores/app.js.map
