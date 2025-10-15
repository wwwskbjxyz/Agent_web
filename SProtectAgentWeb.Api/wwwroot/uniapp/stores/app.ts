import { computed, reactive, ref, watch } from 'vue';
import { defineStore } from 'pinia';
import {
  apiRequest,
  refreshBaseURL
} from '@/common/api';
import { usePlatformStore } from '@/stores/platform';
import type {
  AgentInfoResponse,
  AgentListItem,
  AgentSearchFilters,
  AgentStatusMutation,
  AgentCreatePayload,
  AgentAdjustBalancePayload,
  AgentUpdateRemarkPayload,
  AgentUpdatePasswordPayload,
  AgentAssignCardTypesPayload,
  AgentStatistics,
  AnnouncementItem,
  AnnouncementResponse,
  CardInfo,
  CardListItem,
  CardListResponse,
  CardSearchFilters,
  CardStatusMutation,
  CardVerificationResult,
  ChatConversationDto,
  ChatContactDto,
  ChatMessageDto,
  ChatMessagesResponse,
  ChatSessionItem,
  ChatContactItem,
  CardTypeInfo,
  CardTypeListResponse,
  GenerateCardsResponse,
  DashboardPayload,
  DashboardStat,
  LanzouLinkListResponse,
  LanzouLinkRecordDto,
  LinkRecordItem,
  LoginPayload,
  LoginSuccess,
  BlacklistLogItem,
  BlacklistMachineItem,
  BlacklistMachineCreatePayload,
  RefreshUserInfoResponse,
  RecentActivationTrendResponse,
  SalesQueryFilters,
  SalesQueryResultPayload,
  SettlementAgentOption,
  SettlementBillItem,
  SettlementCycleInfoDto,
  SettlementRateItem,
  SettlementRateListResponse,
  SoftwareAgentInfo,
  SoftwareListResponse,
  SystemStatusResponse,
  SubAgentTrendPayload,
  TrendPoint,
  UsageDistributionResponse,
  UsageHeatmapItem,
  UserProfile,
  VerificationPayload,
  VerificationStats
} from '@/common/types';
import {
  clearSelectedSoftwareDisplayName,
  clearToken,
  getSelectedSoftwareDisplayName,
  getSelectedSoftwareName,
  getThemePreference,
  getTokenValue,
  setSelectedSoftwareDisplayName,
  setSelectedSoftwareName,
  setThemePreference,
  setTokenValue,
  type ThemePreference
} from '@/utils/storage';
import {
  formatDate,
  formatDateTime,
  formatTime
} from '@/utils/time';

declare const getCurrentPages: (() => Array<{ route?: string }>) | undefined;

const DEFAULT_CARD_PAGE_SIZE = 50;
const DEFAULT_LINK_PAGE_SIZE = 20;

type VerificationStatus = VerificationPayload['status'];

function resolveCardStatus(state?: string | null): { status: CardListItem['status']; text: string } {
  const normalized = (state ?? '').trim();
  if (!normalized) {
    return { status: 'unknown', text: '未知' };
  }
  if (['启用', '可用', 'active', 'enabled', '0'].includes(normalized)) {
    return { status: 'enabled', text: normalized || '启用' };
  }
  if (['禁用', '封禁', 'disabled', '1', '停用'].includes(normalized)) {
    return { status: 'disabled', text: normalized };
  }
  return { status: 'unknown', text: normalized };
}

function ensureArray<T>(value: unknown): T[] {
  if (Array.isArray(value)) {
    return value as T[];
  }
  return [];
}

function splitKeywordInput(raw: string): string[] {
  return raw
    .split(/[\s,，;；]+/)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

function computeTrendMetrics(points: TrendPoint[]): { total: number; max: number } {
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

function buildSubAgentTrendPayload(trend?: RecentActivationTrendResponse): SubAgentTrendPayload {
  if (!trend) {
    return { categories: [], series: [], total: 0 };
  }

  const categoriesRaw = ensureArray<string>(trend.categories);
  const points = ensureArray(trend.points);
  const categories = categoriesRaw.length
    ? categoriesRaw.map((item) => item.trim()).filter((item) => item.length > 0)
    : points.map((item) => (item?.date || '').toString().trim()).filter((item) => item.length > 0);

  const normalizedCategories = categories.length ? categories : [];
  const series = ensureArray(trend.series).map((item) => {
    const name = (item?.displayName || item?.agent || '').toString().trim() || '未命名代理';
    const map = new Map<string, number>();
    ensureArray(item?.points).forEach((point) => {
      const key = (point?.date || '').toString().trim();
      if (!key) return;
      const value = Number(point?.count ?? 0);
      map.set(key, Number.isFinite(value) ? value : 0);
    });
    const values = normalizedCategories.map((category) => map.get(category) ?? 0);
    const total = values.reduce((sum, value) => sum + value, 0);
    return { name, values, total };
  });

  const total = series.reduce((sum, item) => sum + item.total, 0);
  return { categories: normalizedCategories, series, total };
}

function normalizeSoftwareName(value?: string | null): string {
  return (value ?? '').toString().trim().toLowerCase();
}

interface ChatMessageSendOptions {
  type?: 'text' | 'image';
  mediaBase64?: string;
  mediaName?: string;
  caption?: string;
}

function matchesSoftware(value: unknown, software: string): boolean {
  if (!software) return false;
  return normalizeSoftwareName(value).toLowerCase() === normalizeSoftwareName(software).toLowerCase();
}

const BLACKLIST_STORAGE_PREFIX = 'sprotect:blacklist:lastSeen:';

function buildBlacklistStorageKey(software: string): string {
  return `${BLACKLIST_STORAGE_PREFIX}${software}`;
}

function normalizeStorageValue(value: unknown): string {
  if (typeof value === 'string') {
    return value;
  }
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value.toString();
  }
  if (value != null) {
    try {
      return String(value);
    } catch (error) {
      console.warn('Failed to normalize storage value', error);
    }
  }
  return '';
}

function transformSalesResult(data?: any): SalesQueryResultPayload {
  const cards = ensureArray<any>(data?.cards).map((item) => ({
    card: (item?.card || item?.prefix_Name || item?.key || '').toString(),
    activateTime: formatDateTime(item?.activateTime ?? item?.activateTime_ ?? item?.ActivateTime_)
  }));
  const count = Number(data?.count ?? cards.length ?? 0);
  const settlements = ensureArray<any>(data?.settlements)
    .map((item) => ({
      cardType: (item?.cardType ?? '').toString(),
      count: Number(item?.count ?? 0) || 0,
      price: Number(item?.price ?? 0) || 0,
      total: Number(item?.total ?? 0) || 0
    }))
    .filter((item) => item.cardType);
  const totalAmountRaw = Number(data?.totalAmount ?? 0);
  const totalAmount = Number.isFinite(totalAmountRaw)
    ? totalAmountRaw
    : settlements.reduce((sum, item) => sum + item.total, 0);
  return {
    count: Number.isFinite(count) ? count : cards.length,
    cards,
    settlements,
    totalAmount
  };
}

function transformBlacklistLog(raw: any): BlacklistLogItem {
  return {
    timestamp: formatDateTime(raw?.timestamp ?? raw?.Timestamp),
    software: normalizeSoftwareName(raw?.software ?? raw?.Software),
    ip: (raw?.ip ?? raw?.IP ?? '').toString(),
    card: (raw?.card ?? raw?.Card ?? '').toString(),
    machineCode: (raw?.pcsign ?? raw?.PCSign ?? '').toString(),
    event: (raw?.errEvents ?? raw?.ErrEvents ?? '').toString()
  };
}

function transformBlacklistMachine(raw: any): BlacklistMachineItem {
  const value = (raw?.value ?? raw?.Value ?? '').toString();
  return {
    value,
    software: normalizeSoftwareName(raw?.software ?? raw?.Software),
    type: Number(raw?.type ?? raw?.Type ?? 2) || 0,
    remarks: (raw?.remarks ?? raw?.Remarks ?? '').toString() || undefined
  };
}

function buildCardListPayload(
  software: string,
  filters: CardSearchFilters,
  page: number,
  limit: number
): Record<string, unknown> {
  const payload: Record<string, unknown> = { software, page, limit };
  const agent = (filters.agent ?? '').trim();
  if (agent) {
    payload.agent = agent;
    payload.includeDescendants = false;
  } else {
    payload.includeDescendants = filters.includeDescendants ?? true;
  }

  if (filters.status && filters.status !== '') {
    payload.status = filters.status;
  }

  const rawKeyword = (filters.keyword ?? '').trim();
  const baseKeywords = filters.keywords && filters.keywords.length ? filters.keywords : splitKeywordInput(rawKeyword);
  const ipRegex = /^(\d{1,3}\.){3}\d{1,3}$/;

  let searchType = typeof filters.searchType === 'number' ? filters.searchType : 0;
  if (filters.cardType && (searchType === 0 || searchType === 3)) {
    searchType = 3;
  }
  if (!filters.cardType && baseKeywords.length === 1 && ipRegex.test(baseKeywords[0]) && (searchType === 0 || searchType === 2)) {
    searchType = 2;
  }
  payload.searchType = searchType;

  let keywords: string[] = [];
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

function transformCard(item: CardInfo): CardListItem {
  const key = item.prefix_Name || '';
  const owner = (item.whom || item.owner || '').trim();
  const cardType = (item.cardType || '').trim();
  const { status, text } = resolveCardStatus(item.state);
  const createdAt = formatDateTime(item.createData_);
  const activatedAt = formatDateTime(item.activateTime_ ?? item.lastLoginTime_);
  const expireAt = formatDateTime(item.expiredTime__);
  const machineCodes = ensureArray<string>(item.machineCodes)
    .map((code) => code.trim())
    .filter((code) => code.length > 0);

  if (item.machineCode) {
    const candidate = item.machineCode.trim();
    if (candidate && !machineCodes.includes(candidate)) {
      machineCodes.unshift(candidate);
    }
  }

  return {
    key,
    owner: owner || '—',
    cardType: cardType || '未分类',
    status,
    statusText: text || (status === 'enabled' ? '启用' : status === 'disabled' ? '禁用' : '未知'),
    createdAt,
    activatedAt,
    expireAt,
    ip: item.ip || undefined,
    machineCodes,
    remark: item.remarks || ''
  };
}

function determineAgentStatus(status: unknown): AgentListItem['status'] {
  if (typeof status === 'boolean') {
    return status ? 'enabled' : 'disabled';
  }
  if (typeof status === 'number') {
    return status === 0 ? 'enabled' : 'disabled';
  }
  if (typeof status === 'string') {
    const normalized = status.trim().toLowerCase();
    if (['启用', 'enabled', 'true', '0'].includes(normalized)) {
      return 'enabled';
    }
    if (['禁用', 'disabled', 'false', '1', '停用'].includes(normalized)) {
      return 'disabled';
    }
  }
  return 'enabled';
}

function toNumber(value: unknown, fallback = 0) {
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : fallback;
}

function transformAgent(raw: any): AgentListItem {
  const username = (raw?.username || raw?.user || raw?.account || '-').toString();
  const balance = toNumber(raw?.balance ?? raw?.money);
  const timeStock = toNumber(raw?.timeStock ?? raw?.stockHours);
  const parities = toNumber(raw?.parities);
  const totalParities = toNumber(raw?.totalParities ?? raw?.total_parities, parities);
  const remark = (raw?.remark ?? raw?.remarks ?? '').toString();
  const password = (raw?.password ?? raw?.Password ?? '').toString();
  const cardTypes = ensureArray<string>(raw?.card_types ?? raw?.cardTypes).map((item) => item.toString());
  const expiration = raw?.expiration ? formatDateTime(raw.expiration) : undefined;
  const status = determineAgentStatus(raw?.status ?? raw?.stat ?? raw?.cardsEnable ?? raw?.enabled ?? raw?.state);
  const depth = toNumber(raw?.depth, 0);

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

function transformContact(dto: ChatContactDto): ChatContactItem {
  const username = (dto?.username || '').trim();
  const displayName = (dto?.displayName || username || '-').toString().trim();
  const remark = dto?.remark?.toString().trim();
  return {
    username,
    displayName: displayName || username || '未命名联系人',
    remark: remark || undefined
  };
}

function buildDashboardStats(stats?: AgentStatistics): DashboardStat[] {
  if (!stats) {
    return [];
  }
  return [
    { key: 'total', label: '卡密总数', value: stats.totalCards.toString() },
    { key: 'active', label: '启用中', value: stats.activeCards.toString() },
    { key: 'used', label: '已使用', value: stats.usedCards.toString() },
    { key: 'expired', label: '已过期', value: stats.expiredCards.toString() },
    { key: 'subAgents', label: '子代理', value: stats.subAgents.toString() }
  ];
}

function buildTrendData(trend?: RecentActivationTrendResponse): TrendPoint[] {
  if (!trend) {
    return [];
  }

  if (Array.isArray(trend.categories) && trend.categories.length > 0) {
    const map = new Map<string, number>();
    (trend.points || []).forEach((point) => {
      if (!point?.date) return;
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

function buildAnnouncementsPayload(response?: AnnouncementResponse | null): AnnouncementItem[] {
  if (!response || !response.content) {
    return [];
  }
  return [
    {
      id: 'announcement',
      title: '运维公告',
      content: response.content.trim(),
      updatedAt: formatDateTime(response.updatedAt)
    }
  ];
}

function buildHeatmapPayload(response?: UsageDistributionResponse | null): UsageHeatmapItem[] {
  if (!response) {
    return [];
  }

  const provinces = ensureArray(response.provinces).sort((a, b) => (b?.count ?? 0) - (a?.count ?? 0));
  const total = response.resolvedTotal || provinces.reduce((sum, item) => sum + (item?.count ?? 0), 0);

  return provinces.slice(0, 6).map((item, index) => {
    const name = item?.province || item?.city || item?.district || `地区${index + 1}`;
    const count = item?.count ?? 0;
    const percentage = total > 0 ? Math.round((count / total) * 1000) / 10 : 0;
    return {
      name,
      count,
      percentage
    };
  });
}

function transformLink(record: LanzouLinkRecordDto): LinkRecordItem {
  return {
    id: record.id,
    url: record.url,
    extractionCode: record.extractionCode,
    createdAt: formatDateTime(record.createdAt),
    content: record.rawContent
  };
}

function transformConversation(dto: ChatConversationDto, currentUser: string): ChatSessionItem {
  const participants = ensureArray<string>(dto.participants);
  const displayName = dto.isGroup
    ? dto.groupName || `群聊（${participants.length}）`
    : participants.find((name) => name && name !== currentUser) || dto.groupName || dto.conversationId;
  return {
    id: dto.conversationId,
    title: displayName,
    unread: dto.unreadCount ?? 0,
    updatedAt: formatDateTime(dto.updatedAt),
    preview: dto.lastMessagePreview || '',
    isGroup: dto.isGroup,
    participants,
    messages: []
  };
}

function transformMessages(list: ChatMessageDto[], currentUser: string): ChatSessionItem['messages'] {
  return ensureArray<ChatMessageDto>(list).map((message) => ({
    id: message.id,
    sender: message.sender === currentUser ? 'user' : 'system',
    content: message.content,
    time: formatTime(message.timestamp),
    type: message.type,
    caption: message.caption
  }));
}

function resolveVerificationStatus(result: CardVerificationResult): VerificationStatus {
  if (result.verificationPassed) {
    return 'success';
  }
  if (result.hasReachedLinkLimit) {
    return 'warning';
  }
  return 'error';
}

function buildVerificationPayload(result: CardVerificationResult): VerificationPayload {
  const stats: VerificationStats = {
    attemptNumber: result.attemptNumber,
    remainingDownloads: result.remainingLinkQuota,
    expiresAt: result.expiresAt ? formatDate(result.expiresAt) : undefined
  };

  const history = ensureArray(result.downloadHistory).map((item) => ({
    id: item.linkId,
    url: item.url,
    extractionCode: item.extractionCode,
    assignedAt: formatDateTime(item.assignedAt),
    isNew: !!item.isNew
  }));

  return {
    status: resolveVerificationStatus(result),
    message: result.message,
    downloadUrl: result.download?.url,
    extractionCode: result.download?.extractionCode,
    stats,
    history
  };
}

let bootstrapPromise: Promise<void> | null = null;
let redirectingToLogin = false;

export const useAppStore = defineStore('app', () => {
  const loading = reactive({
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

  const token = ref<string | null>(getTokenValue());
  const session = ref<RefreshUserInfoResponse | null>(null);
  const softwareList = ref<SoftwareAgentInfo[]>([]);
  const selectedSoftware = ref<string>(getSelectedSoftwareDisplayName() || '');
  const settlementAgents = ref<SettlementAgentOption[]>([]);
  const selectedSettlementAgent = ref<string>('');
  const settlementCycle = ref<SettlementCycleInfoDto | null>(null);
  const settlementBills = ref<SettlementBillItem[]>([]);
  const settlementHasReminder = ref(false);
  const theme = ref<ThemePreference>(getThemePreference());

  const dashboard = reactive<DashboardPayload>({
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

  const recentCards = ref<CardListItem[]>([]);
  const cardKeys = ref<CardListItem[]>([]);
  const cardTotal = ref(0);
  const cardTypes = ref<CardTypeInfo[]>([]);
  const cardFilters = reactive<CardSearchFilters>({
    includeDescendants: true,
    searchType: 0,
    status: '',
    page: 1,
    limit: DEFAULT_CARD_PAGE_SIZE
  });
  const agents = ref<AgentListItem[]>([]);
  const agentTotal = ref(0);
  const agentFilters = reactive<AgentSearchFilters>({ keyword: '', page: 1, limit: 50 });
  const linkRecords = ref<LinkRecordItem[]>([]);
  const linkTotal = ref(0);
  const chatSessions = ref<ChatSessionItem[]>([]);
  const chatContacts = ref<ChatContactItem[]>([]);
  const verification = ref<VerificationPayload | null>(null);
  const systemStatus = ref<SystemStatusResponse | null>(null);
  const blacklistLogs = ref<BlacklistLogItem[]>([]);
  const blacklistMachines = ref<BlacklistMachineItem[]>([]);
  const blacklistLatestBySoftware = reactive<Record<string, string>>({});
  const blacklistLastSeenCache = reactive<Record<string, string>>({});
  const settlementRateCache = reactive<Record<string, SettlementRateItem[]>>({});
  const cardTypeRequestCache = new Map<string, Promise<CardTypeInfo[]>>();

  const activeSettlementAgent = computed(() => {
    const explicit = selectedSettlementAgent.value?.trim();
    if (explicit) {
      return explicit;
    }
    const software = selectedSoftware.value;
    if (software) {
      const profile = session.value?.softwareAgentInfo?.[software];
      const username = profile?.user?.trim();
      if (username) {
        return username;
      }
    }
    return session.value?.username?.trim() || '';
  });

  function buildSettlementCacheKey(software: string, agent?: string): string {
    const trimmedSoftware = (software ?? '').trim();
    const normalizedAgent = (agent ?? '').trim() || activeSettlementAgent.value || 'anonymous';
    return `${trimmedSoftware}::${normalizedAgent}`;
  }
  const settlementRates = computed<SettlementRateItem[]>(() => {
    const software = selectedSoftware.value;
    if (!software) {
      return [];
    }
    const cacheKey = buildSettlementCacheKey(software, activeSettlementAgent.value);
    return settlementRateCache[cacheKey] ?? [];
  });

  const chatUnreadCount = computed(() =>
    chatSessions.value.reduce((sum, item) => sum + (Number(item.unread) > 0 ? Number(item.unread) : 0), 0)
  );
  const hasNewBlacklistLogs = computed(() => {
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

  function getStoredBlacklistTimestamp(software: string): string {
    if (!software) {
      return '';
    }
    if (Object.prototype.hasOwnProperty.call(blacklistLastSeenCache, software)) {
      return blacklistLastSeenCache[software] ?? '';
    }
    try {
      const stored = uni.getStorageSync(buildBlacklistStorageKey(software));
      const normalized = normalizeStorageValue(stored);
      blacklistLastSeenCache[software] = normalized;
      return normalized;
    } catch (error) {
      console.warn('Failed to read blacklist last seen timestamp', error);
      blacklistLastSeenCache[software] = '';
      return '';
    }
  }

  function setStoredBlacklistTimestamp(software: string, timestamp: string) {
    if (!software) {
      return;
    }
    try {
      if (!timestamp) {
        if (typeof uni.removeStorageSync === 'function') {
          uni.removeStorageSync(buildBlacklistStorageKey(software));
        } else {
          uni.setStorageSync(buildBlacklistStorageKey(software), '');
        }
      } else {
        uni.setStorageSync(buildBlacklistStorageKey(software), timestamp);
      }
    } catch (error) {
      console.warn('Failed to persist blacklist last seen timestamp', error);
    }
    blacklistLastSeenCache[software] = timestamp;
  }

  const lastLoadedCardTypeSoftware = ref<string>('');
  const lastLoadedContactSoftware = ref<string>('');

  watch(
    () => selectedSoftware.value,
    (software) => {
      const normalized = software?.trim();
      if (!normalized) {
        cardTypes.value = [];
        lastLoadedCardTypeSoftware.value = '';
        return;
      }

      if (!token.value) {
        return;
      }

      void loadCardTypes().catch((error) => {
        console.warn('Prefetch card types failed', error);
      });
    },
    { flush: 'post' }
  );

  const profile = computed<UserProfile | null>(() => {
    if (!session.value) {
      return null;
    }
    return {
      id: session.value.username,
      name: session.value.username,
      role: session.value.isSuper ? '超级管理员' : '代理用户',
      avatar: '',
      permissions: session.value.softwareList ?? []
    };
  });

  const isSuper = computed(() => session.value?.isSuper ?? false);

  function logoutLocal() {
    clearToken();
    token.value = null;
    session.value = null;
    softwareList.value = [];
    selectedSoftware.value = '';
    selectedSettlementAgent.value = '';
    settlementAgents.value = [];
    cardTypes.value = [];
    lastLoadedCardTypeSoftware.value = '';
    cardTypeRequestCache.clear();
    setSelectedSoftwareName();
    bootstrapPromise = null;
    Object.keys(settlementRateCache).forEach((key) => {
      delete settlementRateCache[key];
    });
  }

  function setTheme(next: ThemePreference) {
    if (theme.value === next) {
      return;
    }
    theme.value = next;
    setThemePreference(next);
  }

  function redirectToLogin(options?: { message?: string }) {
    const message = options?.message;
    const wasRedirecting = redirectingToLogin;
    logoutLocal();

    if (message && !wasRedirecting) {
      uni.showToast({ title: message, icon: 'none' });
    }

    const pagesGetter = typeof getCurrentPages === 'function' ? getCurrentPages : undefined;
    const pages = pagesGetter ? pagesGetter() : [];
    const hasPages = Array.isArray(pages) && pages.length > 0;
    const currentRoute = hasPages ? pages[pages.length - 1]?.route ?? '' : '';

    if (!hasPages || currentRoute === 'pages/login/index') {
      redirectingToLogin = false;
      return;
    }

    if (redirectingToLogin) {
      return;
    }

    redirectingToLogin = true;
    const delay = message && !wasRedirecting ? 420 : 80;
    setTimeout(() => {
      uni.reLaunch({ url: '/pages/login/index' });
      setTimeout(() => {
        redirectingToLogin = false;
      }, 240);
    }, delay);
  }

  function handleUnauthorized(message?: string) {
    redirectToLogin({ message: message || '登录已过期，请重新登录' });
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
    refreshBaseURL();

    bootstrapPromise = (async () => {
      try {
        await loadCurrentUser();
        await loadSoftwareList();
      } catch (error) {
        console.error('Bootstrap failed', error);
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
        const platformStore = usePlatformStore();
        if (platformStore.isAuthenticated) {
          const binding = platformStore.selectedBinding || platformStore.bindings[0];
          if (binding?.authorAccount && binding?.authorPassword && !loading.login) {
            await login(
              { username: binding.authorAccount, password: binding.authorPassword },
              { skipBootstrap: true }
            );
          }
        }
      } catch (error) {
        console.warn('Auto login via platform binding failed', error);
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
      const data = await apiRequest<RefreshUserInfoResponse>({
        url: '/api/Auth/getUserInfo',
        method: 'POST',
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
      selectedSoftware.value = '';
      return [];
    }
    loading.software = true;
    try {
      const response = await apiRequest<SoftwareListResponse>({
        url: '/api/Software/GetSoftwareList',
        method: 'POST',
        auth: true,
        disableMock: true
      });
      const items = ensureArray<SoftwareAgentInfo>(response?.softwares);
      softwareList.value = items;

      const stored = getSelectedSoftwareDisplayName() || getSelectedSoftwareName();
      const matched = items.find((item) => normalizeSoftwareName(item.softwareName) === normalizeSoftwareName(stored));
      if (matched) {
        selectedSoftware.value = matched.softwareName;
        setSelectedSoftwareDisplayName(matched.softwareName);
        selectedSettlementAgent.value = '';
        settlementAgents.value = [];
      } else if (items.length) {
        selectedSoftware.value = items[0].softwareName;
        setSelectedSoftwareDisplayName(items[0].softwareName);
        selectedSettlementAgent.value = '';
        settlementAgents.value = [];
      } else {
        selectedSoftware.value = '';
        clearSelectedSoftwareDisplayName();
        selectedSettlementAgent.value = '';
        settlementAgents.value = [];
      }
      return items;
    } finally {
      loading.software = false;
    }
  }

  function setSelectedSoftware(name: string) {
    const normalized = normalizeSoftwareName(name);
    const match = softwareList.value.find((item) => normalizeSoftwareName(item.softwareName) === normalized);
    if (!match) {
      return;
    }
    selectedSoftware.value = match.softwareName;
    setSelectedSoftwareDisplayName(match.softwareName);
    selectedSettlementAgent.value = '';
    settlementAgents.value = [];
  }

  function setSelectedSettlementAgent(username: string) {
    selectedSettlementAgent.value = (username ?? '').trim();
  }

  async function login(payload: LoginPayload, options?: { skipBootstrap?: boolean }) {
    loading.login = true;
    try {
      const response = await apiRequest<LoginSuccess>({
        url: '/api/Auth/login',
        method: 'POST',
        data: payload,
        disableMock: true
      });
      setTokenValue(response.token);
      token.value = response.token;
      if (!options?.skipBootstrap) {
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
        await apiRequest({
          url: '/api/Auth/logout',
          method: 'POST',
          auth: true,
          disableMock: true
        });
      }
    } catch (error) {
      console.warn('Logout request failed', error);
    } finally {
      loading.logout = false;
      redirectToLogin({ message: '已退出登录' });
    }
  }

  async function loadDashboard() {
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
        apiRequest<AgentInfoResponse>({
          url: '/api/Agent/getUserInfo',
          method: 'POST',
          data: { software },
          auth: true,
          disableMock: true
        }),
        apiRequest<RecentActivationTrendResponse>({
          url: '/api/Card/getRecentActivationTrend',
          method: 'POST',
          data: { software, OnlyDescendants: false },
          auth: true,
          disableMock: true
        }),
        apiRequest<RecentActivationTrendResponse>({
          url: '/api/Card/getRecentActivationTrend',
          method: 'POST',
          data: { software, OnlyDescendants: true },
          auth: true,
          disableMock: true
        }),
        apiRequest<UsageDistributionResponse>({
          url: '/api/Card/getUsageDistribution',
          method: 'POST',
          data: { software, includeDescendants: true },
          auth: true,
          disableMock: true
        }),
        apiRequest<CardListResponse>({
          url: '/api/Card/getCardList',
          method: 'POST',
          data: { software, page: 1, limit: 8, includeDescendants: true },
          auth: true,
          disableMock: true
        }),
        apiRequest<AnnouncementResponse>({
          url: '/api/System/announcement',
          method: 'GET',
          auth: true,
          disableMock: true
        }).catch(() => null),
        session.value?.isSuper
          ? apiRequest<SystemStatusResponse>({
              url: '/api/System/status',
              method: 'GET',
              auth: true,
              disableMock: true
            }).catch(() => null)
          : Promise.resolve(null)
      ]);

      dashboard.stats = buildDashboardStats(agentInfo?.statistics);
      const totalTrend = buildTrendData(trend);
      dashboard.trend = totalTrend;
      const trendMetrics = computeTrendMetrics(totalTrend);
      dashboard.trendTotal = trendMetrics.total;
      dashboard.trendMax = trendMetrics.max;
      const descendantTrend = buildSubAgentTrendPayload(subTrend);
      dashboard.subAgentTrend = descendantTrend;
      dashboard.announcements = buildAnnouncementsPayload(announcement);
      dashboard.usageHeatmap = buildHeatmapPayload(usage);

      const cards = ensureArray<CardInfo>(cardsResponse?.data);
      recentCards.value = cards.map(transformCard);

      if (system) {
        systemStatus.value = system;
      }
    } finally {
      loading.dashboard = false;
    }
  }

  async function loadCardKeys(filters: Partial<CardSearchFilters> = {}) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      cardKeys.value = [];
      cardTotal.value = 0;
      return;
    }

    const nextFilters: CardSearchFilters = {
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
      const response = await apiRequest<CardListResponse>({
        url: '/api/Card/getCardList',
        method: 'POST',
        data: payload,
        auth: true,
        disableMock: true
      });
      const items = ensureArray<CardInfo>(response?.data);
      cardKeys.value = items.map(transformCard);
      cardTotal.value = Number(response?.total ?? items.length ?? 0);
    } finally {
      loading.cards = false;
    }
  }

  async function loadCardTypes(force = false) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      cardTypes.value = [];
      lastLoadedCardTypeSoftware.value = '';
      return [];
    }

    if (!force && cardTypes.value.length > 0 && lastLoadedCardTypeSoftware.value === software) {
      return cardTypes.value;
    }

    if (!force && cardTypeRequestCache.has(software)) {
      return cardTypeRequestCache.get(software)!;
    }

    if (force) {
      cardTypeRequestCache.delete(software);
    }

    const request = (async () => {
      loading.cardTypes = true;
      try {
        const response = await apiRequest<CardTypeListResponse>({
          url: '/api/CardType/getCardTypeList',
          method: 'POST',
          data: { software },
          auth: true,
          disableMock: true
        });
        const items = ensureArray(response?.items);
        cardTypes.value = items.map((item) => ({
          name: item?.name ?? '',
          prefix: item?.prefix ?? undefined,
          duration: Number(item?.duration ?? 0),
          price: Number(item?.price ?? 0),
          remarks: item?.remarks ?? undefined
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
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      return [] as SettlementRateItem[];
    }

    const targetAgent = selectedSettlementAgent.value?.trim() || '';
    const cacheKey = buildSettlementCacheKey(software, targetAgent || activeSettlementAgent.value);

    if (!force && settlementRateCache[cacheKey]) {
      return settlementRateCache[cacheKey];
    }

    loading.settlementRates = true;
    try {
      const response = await apiRequest<SettlementRateListResponse>({
        url: '/api/Settlement/list',
        method: 'POST',
        data: { software, targetAgent: targetAgent || undefined },
        auth: true,
        disableMock: true
      });

      const responseTarget = (response?.targetAgent ?? targetAgent ?? '').toString().trim();
      const options = ensureArray<any>(response?.agents)
        .map((item) => ({
          username: (item?.username ?? '').toString().trim(),
          displayName: (item?.displayName ?? '').toString().trim(),
          hasPendingReminder: Boolean(item?.hasPendingReminder)
        }))
        .filter((item) => item.username.length > 0);

      if (!options.length) {
        const fallback = activeSettlementAgent.value || session.value?.username?.trim() || '';
        if (fallback) {
          options.push({ username: fallback, displayName: `${fallback} · 当前账号` });
        }
      }

      settlementAgents.value = options;
      if (responseTarget !== selectedSettlementAgent.value) {
        selectedSettlementAgent.value = responseTarget;
      }

      settlementCycle.value = response?.cycle ?? null;
      settlementBills.value = ensureArray<SettlementBillItem>(response?.bills);
      settlementHasReminder.value = Boolean(response?.hasPendingReminder);

      const items = ensureArray<any>(response?.rates)
        .map((item) => ({
          cardType: (item?.cardType ?? '').toString().trim(),
          price: Number(item?.price ?? 0) || 0
        }))
        .filter((item) => item.cardType);

      settlementRateCache[cacheKey] = items;
      return settlementRateCache[cacheKey];
    } finally {
      loading.settlementRates = false;
    }
  }

  async function saveSettlementRates(
    rates: SettlementRateItem[],
    cycleDays?: number | null,
    cycleTimeMinutes?: number | null
  ) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const normalized = ensureArray<SettlementRateItem>(rates)
      .map((item) => ({
        cardType: (item?.cardType ?? '').toString().trim(),
        price: Number(item?.price ?? 0) || 0
      }))
      .filter((item) => item.cardType);

    const targetAgent = selectedSettlementAgent.value?.trim() || '';

    loading.saveSettlementRates = true;
    try {
      const response = await apiRequest<SettlementRateListResponse>({
        url: '/api/Settlement/upsert',
        method: 'POST',
        data: {
          software,
          rates: normalized,
          targetAgent: targetAgent || undefined,
          cycleDays: typeof cycleDays === 'number' ? cycleDays : undefined,
          cycleTimeMinutes:
            typeof cycleTimeMinutes === 'number' && !Number.isNaN(cycleTimeMinutes)
              ? cycleTimeMinutes
              : undefined
        },
        auth: true,
        disableMock: true
      });

      const responseTarget = (response?.targetAgent ?? targetAgent ?? '').toString().trim();
      if (responseTarget !== selectedSettlementAgent.value) {
        selectedSettlementAgent.value = responseTarget;
      }

      const options = ensureArray<any>(response?.agents)
        .map((item) => ({
          username: (item?.username ?? '').toString().trim(),
          displayName: (item?.displayName ?? '').toString().trim(),
          hasPendingReminder: Boolean(item?.hasPendingReminder)
        }))
        .filter((item) => item.username.length > 0);
      if (options.length) {
        settlementAgents.value = options;
      }

      settlementCycle.value = response?.cycle ?? settlementCycle.value;
      settlementBills.value = ensureArray<SettlementBillItem>(response?.bills);
      settlementHasReminder.value = Boolean(response?.hasPendingReminder);

      const saved = ensureArray<any>(response?.rates)
        .map((item) => ({
          cardType: (item?.cardType ?? '').toString().trim(),
          price: Number(item?.price ?? 0) || 0
        }))
        .filter((item) => item.cardType);

      const cacheKey = buildSettlementCacheKey(software, responseTarget || activeSettlementAgent.value);
      settlementRateCache[cacheKey] = saved;
      return settlementRateCache[cacheKey];
    } finally {
      loading.saveSettlementRates = false;
    }
  }

  async function completeSettlementBill(billId: number, amount: number, note?: string) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const targetAgent = selectedSettlementAgent.value?.trim() || '';

    const response = await apiRequest<SettlementRateListResponse>({
      url: '/api/Settlement/bill/complete',
      method: 'POST',
      data: {
        software,
        billId,
        amount,
        note,
        targetAgent: targetAgent || undefined
      },
      auth: true,
      disableMock: true
    });

    settlementCycle.value = response?.cycle ?? settlementCycle.value;
    settlementBills.value = ensureArray<SettlementBillItem>(response?.bills);
    settlementHasReminder.value = Boolean(response?.hasPendingReminder);

    return settlementBills.value;
  }

  async function generateCards(payload: { cardType: string; quantity: number; remarks?: string; customPrefix?: string }): Promise<GenerateCardsResponse> {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const data = {
      software,
      cardType: payload.cardType,
      quantity: payload.quantity,
      remarks: payload.remarks ?? '',
      ...(payload.customPrefix ? { customPrefix: payload.customPrefix } : {})
    };

    const response = await apiRequest<GenerateCardsResponse>({
      url: '/api/Card/generateCards',
      method: 'POST',
      data,
      auth: true,
      disableMock: true
    });

    await loadCardKeys({ page: cardFilters.page, limit: cardFilters.limit });
    return response;
  }

  async function updateCardStatus(mutation: CardStatusMutation) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const actionMap: Record<CardStatusMutation['action'], string> = {
      enable: 'enableCard',
      disable: 'disableCard',
      unban: 'enableCardWithBanTimeReturn'
    };

    const action = actionMap[mutation.action];
    if (!action) {
      throw new Error('不支持的操作');
    }

    const result = await apiRequest<{ message?: string }>({
      url: `/api/Card/${action}`,
      method: 'POST',
      data: { software, cardKey: mutation.cardKey },
      auth: true,
      disableMock: true
    });

    await loadCardKeys({ page: cardFilters.page, limit: cardFilters.limit });
    return result?.message ?? '';
  }

  async function unbindCard(cardKey: string) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const result = await apiRequest<{ message?: string }>({
      url: '/api/Card/unbindCard',
      method: 'POST',
      data: { software, cardKey },
      auth: true,
      disableMock: true
    });

    await loadCardKeys({ page: cardFilters.page, limit: cardFilters.limit });
    return result?.message ?? '';
  }

  async function loadAgents(options: AgentSearchFilters = {}) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      agents.value = [];
      agentTotal.value = 0;
      return;
    }

    const nextFilters: AgentSearchFilters = {
      ...agentFilters,
      ...options
    };

    nextFilters.page = nextFilters.page && nextFilters.page > 0 ? nextFilters.page : 1;
    nextFilters.limit = nextFilters.limit && nextFilters.limit > 0 ? nextFilters.limit : 50;
    nextFilters.keyword = (nextFilters.keyword ?? '').trim();

    Object.assign(agentFilters, nextFilters);

    const page = agentFilters.page ?? 1;
    const limit = agentFilters.limit ?? 50;
    const keyword = (agentFilters.keyword ?? '').trim();

    loading.agents = true;
    try {
      const response = await apiRequest<{ data: any[]; total: number }>({
        url: '/api/Agent/getSubAgentList',
        method: 'POST',
        data: { software, page, limit, keyword, searchType: 1 },
        auth: true,
        disableMock: true
      });
      const items = ensureArray<any>(response?.data);
      agents.value = items.map(transformAgent);
      agentTotal.value = Number(response?.total ?? items.length ?? 0);
    } finally {
      loading.agents = false;
    }
  }

  async function toggleAgentStatus(payload: AgentStatusMutation) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const usernames = ensureArray<string>(payload.usernames)
      .map((item) => item.trim())
      .filter((item) => item.length > 0);

    if (!usernames.length) {
      throw new Error('请选择代理');
    }

    const path = payload.enable ? 'enableAgent' : 'disableAgent';
    const result = await apiRequest<{ message?: string }>({
      url: `/api/Agent/${path}`,
      method: 'POST',
      data: { software, username: usernames },
      auth: true,
      disableMock: true
    });

    await loadAgents({ page: agentFilters.page, limit: agentFilters.limit, keyword: agentFilters.keyword });
    return result?.message ?? '';
  }

  async function deleteAgents(usernames: string[]) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const targets = ensureArray<string>(usernames)
      .map((item) => item.trim())
      .filter((item) => item.length > 0);

    if (!targets.length) {
      throw new Error('请选择代理');
    }

    const result = await apiRequest<{ message?: string }>({
      url: '/api/Agent/deleteSubAgent',
      method: 'POST',
      data: { software, username: targets },
      auth: true,
      disableMock: true
    });

    await loadAgents({ page: agentFilters.page, limit: agentFilters.limit, keyword: agentFilters.keyword });
    return result?.message ?? '';
  }

  async function createAgent(payload: AgentCreatePayload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const data = {
      software,
      username: payload.username,
      password: payload.password,
      initialBalance: Number(payload.balance ?? 0),
      initialTimeStock: Number(payload.timeStock ?? 0),
      parities: Number(payload.parities ?? 100),
      totalParities: Number(payload.totalParities ?? payload.parities ?? 100),
      remark: payload.remarks ?? '',
      cardTypes: ensureArray<string>(payload.cardTypes)
    };

    const result = await apiRequest<{ message?: string }>({
      url: '/api/Agent/createSubAgent',
      method: 'POST',
      data,
      auth: true,
      disableMock: true
    });

    await loadAgents({ page: 1, limit: agentFilters.limit, keyword: agentFilters.keyword });
    return result?.message ?? '';
  }

  async function adjustAgentBalance(payload: AgentAdjustBalancePayload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const data = {
      software,
      username: payload.username,
      balance: Number(payload.balance ?? 0),
      timeStock: Number(payload.timeStock ?? 0)
    };

    const result = await apiRequest<{ message?: string }>({
      url: '/api/Agent/addMoney',
      method: 'POST',
      data,
      auth: true,
      disableMock: true
    });

    await loadAgents({ page: agentFilters.page, limit: agentFilters.limit, keyword: agentFilters.keyword });
    return result?.message ?? '';
  }

  async function updateAgentRemark(payload: AgentUpdateRemarkPayload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const result = await apiRequest<{ message?: string }>({
      url: '/api/Agent/updateAgentRemark',
      method: 'POST',
      data: { software, username: payload.username, remark: payload.remark ?? '' },
      auth: true,
      disableMock: true
    });

    await loadAgents({ page: agentFilters.page, limit: agentFilters.limit, keyword: agentFilters.keyword });
    return result?.message ?? '';
  }

  async function updateAgentPassword(payload: AgentUpdatePasswordPayload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const result = await apiRequest<{ message?: string }>({
      url: '/api/Agent/updateAgentPassword',
      method: 'POST',
      data: { software, username: payload.username, newPassword: payload.password },
      auth: true,
      disableMock: true
    });

    return result?.message ?? '';
  }

  async function assignAgentCardTypes(payload: AgentAssignCardTypesPayload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const result = await apiRequest<{ message?: string }>({
      url: '/api/Agent/setAgentCardType',
      method: 'POST',
      data: { software, username: payload.username, cardTypes: ensureArray<string>(payload.cardTypes) },
      auth: true,
      disableMock: true
    });

    return result?.message ?? '';
  }

  async function loadAgentCardTypes(username: string) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const response = await apiRequest<{ cardTypes?: string[] }>({
      url: '/api/Agent/getAgentCardType',
      method: 'POST',
      data: { software, username },
      auth: true,
      disableMock: true
    });

    return ensureArray<string>(response?.cardTypes);
  }

  async function runSalesQuery(filters: SalesQueryFilters = {}) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    loading.sales = true;
    try {
      const payload: Record<string, unknown> = {
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

      const response = await apiRequest<SalesQueryResultPayload>({
        url: '/api/Card/countActivatedCards',
        method: 'POST',
        data: payload,
        auth: true,
        disableMock: true
      });

      const normalizedFilters: SalesQueryFilters = {
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

  function clearSalesResult(nextFilters?: Partial<SalesQueryFilters>) {
    dashboard.salesResult = { count: 0, cards: [], settlements: [], totalAmount: 0 };
    dashboard.salesFilters = {
      includeDescendants: nextFilters?.includeDescendants ?? true,
      cardType: nextFilters?.cardType,
      status: nextFilters?.status,
      agent: nextFilters?.agent,
      startTime: nextFilters?.startTime,
      endTime: nextFilters?.endTime
    };
  }

  async function loadBlacklistLogs(limit = 200) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      blacklistLogs.value = [];
      return [];
    }

    loading.blacklistLogs = true;
    try {
      const response = await apiRequest<{ items?: any[] }>({
        url: `/api/Blacklist/logs?limit=${encodeURIComponent(limit)}`,
        method: 'GET',
        auth: true,
        disableMock: true
      });
      const items = ensureArray<any>(response?.items);
      const filtered = items
        .filter((item) => matchesSoftware(item?.software ?? item?.Software, software))
        .map(transformBlacklistLog);
      blacklistLogs.value = filtered;
      const latestRaw = filtered.length > 0 ? filtered[0]?.timestamp ?? '' : '';
      const normalizedLatest = normalizeStorageValue(latestRaw);
      blacklistLatestBySoftware[software] = normalizedLatest && normalizedLatest !== '-' ? normalizedLatest : '';
      return filtered;
    } finally {
      loading.blacklistLogs = false;
    }
  }

  function markBlacklistLogsSeen(software?: string) {
    const target = software ?? selectedSoftware.value;
    if (!target) {
      return;
    }
    const latest = blacklistLatestBySoftware[target] ?? '';
    if (!latest) {
      setStoredBlacklistTimestamp(target, '');
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
      const response = await apiRequest<{ items?: any[] }>({
        url: '/api/Blacklist/machines',
        method: 'GET',
        auth: true,
        disableMock: true
      });
      const items = ensureArray<any>(response?.items);
      const filtered = items
        .filter((item) => matchesSoftware(item?.software ?? item?.Software, software))
        .map(transformBlacklistMachine);
      blacklistMachines.value = filtered;
      return filtered;
    } finally {
      loading.blacklistMachines = false;
    }
  }

  async function createBlacklistMachine(payload: BlacklistMachineCreatePayload) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    await apiRequest<{ message?: string }>({
      url: '/api/Blacklist/machines',
      method: 'POST',
      data: { software, value: payload.value, type: payload.type, remarks: payload.remarks ?? '' },
      auth: true,
      disableMock: true
    });

    return loadBlacklistMachines();
  }

  async function deleteBlacklistMachines(values: string[]) {
    await ensureReady();
    const targets = ensureArray<string>(values)
      .map((item) => item.trim())
      .filter((item) => item.length > 0);

    if (!targets.length) {
      throw new Error('请选择要删除的记录');
    }

    await apiRequest<{ message?: string }>({
      url: '/api/Blacklist/machines/delete',
      method: 'POST',
      data: { values: targets },
      auth: true,
      disableMock: true
    });

    return loadBlacklistMachines();
  }

  async function loadLinkRecords(options: { page?: number; limit?: number } = {}) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }
    const page = options.page ?? 1;
    const limit = options.limit ?? DEFAULT_LINK_PAGE_SIZE;

    loading.links = true;
    try {
      const response = await apiRequest<LanzouLinkListResponse>({
        url: '/api/LinkAudit/listLanzouLinks',
        method: 'POST',
        data: { page, limit, software },
        auth: true,
        disableMock: true
      });
      const items = ensureArray<LanzouLinkRecordDto>(response?.items);
      linkRecords.value = items.map(transformLink);
      linkTotal.value = Number(response?.total ?? items.length ?? 0);
    } finally {
      loading.links = false;
    }
  }

  async function loadChatContacts(force = false) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      chatContacts.value = [];
      lastLoadedContactSoftware.value = '';
      return [];
    }

    if (!force && chatContacts.value.length > 0 && lastLoadedContactSoftware.value === software) {
      return chatContacts.value;
    }

    loading.chatContacts = true;
    try {
      const response = await apiRequest<ChatContactDto[]>({
        url: `/api/Chat/contacts?software=${encodeURIComponent(software)}`,
        method: 'GET',
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
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      chatSessions.value = [];
      return;
    }

    loading.chat = true;
    try {
      const response = await apiRequest<ChatConversationDto[]>({
        url: `/api/Chat/conversations?software=${encodeURIComponent(software)}`,
        method: 'GET',
        auth: true,
        disableMock: true
      });
      const currentUser = session.value?.username ?? '';
      chatSessions.value = ensureArray(response).map((conversation) => transformConversation(conversation, currentUser));
    } finally {
      loading.chat = false;
    }
  }

  async function loadChatMessages(conversationId: string) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software || !conversationId) {
      return [];
    }

    loading.chatMessages = true;
    try {
      const response = await apiRequest<ChatMessagesResponse>({
        url: `/api/Chat/messages?software=${encodeURIComponent(software)}&conversationId=${encodeURIComponent(conversationId)}`,
        method: 'GET',
        auth: true,
        disableMock: true
      });
      const currentUser = session.value?.username ?? '';
      const messages = transformMessages(response?.messages ?? [], currentUser);
      const target = chatSessions.value.find((sessionItem) => sessionItem.id === conversationId);
      if (target) {
        target.messages = messages;
        target.preview = response?.conversation?.lastMessagePreview || target.preview;
        target.updatedAt = formatDateTime(response?.conversation?.updatedAt || target.updatedAt);
        target.unread = response?.conversation?.unreadCount ?? target.unread;
      }
      return messages;
    } finally {
      loading.chatMessages = false;
    }
  }

  async function sendChatMessage(conversationId: string, message: string, options: ChatMessageSendOptions = {}) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software || !conversationId) {
      return null;
    }

    const type = options.type ?? 'text';
    const trimmedMessage = (message ?? '').toString().trim();
    const caption = (options.caption ?? '').toString().trim();

    if (type === 'text' && !trimmedMessage) {
      return null;
    }
    if (type === 'image' && !options.mediaBase64) {
      throw new Error('缺少图片数据');
    }

    const payload: Record<string, unknown> = {
      software,
      conversationId,
      message: type === 'image' ? caption : trimmedMessage
    };

    if (type && type !== 'text') {
      payload.messageType = type;
    }
    if (options.mediaBase64) {
      payload.mediaBase64 = options.mediaBase64;
    }
    if (options.mediaName) {
      payload.mediaName = options.mediaName;
    }

    const response = await apiRequest<ChatMessagesResponse>({
      url: '/api/Chat/send',
      method: 'POST',
      data: payload,
      auth: true,
      disableMock: true
    });

    const currentUser = session.value?.username ?? '';
    const target = chatSessions.value.find((sessionItem) => sessionItem.id === conversationId);
    if (target) {
      target.messages = transformMessages(response?.messages ?? [], currentUser);
      target.preview = response?.conversation?.lastMessagePreview || target.preview;
      target.updatedAt = formatDateTime(response?.conversation?.updatedAt || target.updatedAt);
      target.unread = response?.conversation?.unreadCount ?? target.unread;
    }

    return response;
  }

  async function createDirectConversation(targetUser: string, message: string) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }
    if (!targetUser) {
      throw new Error('请选择聊天对象');
    }
    if (!message.trim()) {
      throw new Error('请输入消息内容');
    }

    const payload = {
      software,
      targetUser,
      message: message.trim()
    };

    const response = await apiRequest<ChatMessagesResponse>({
      url: '/api/Chat/send',
      method: 'POST',
      data: payload,
      auth: true,
      disableMock: true
    });

    const currentUser = session.value?.username ?? '';
    const conversation = transformConversation(response?.conversation, currentUser);
    conversation.messages = transformMessages(response?.messages ?? [], currentUser);

    const existingIndex = chatSessions.value.findIndex((item) => item.id === conversation.id);
    if (existingIndex >= 0) {
      chatSessions.value.splice(existingIndex, 1, conversation);
    } else {
      chatSessions.value.unshift(conversation);
    }

    return conversation;
  }

  async function createGroupConversation(
    groupName: string,
    members: string[],
    initialMessage?: { content?: string; options?: ChatMessageSendOptions }
  ) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }

    const name = groupName.trim();
    if (!name) {
      throw new Error('请输入群聊名称');
    }

    const participantList = ensureArray<string>(members)
      .map((item) => item.trim())
      .filter((item) => item.length > 0);

    if (!participantList.length) {
      throw new Error('请至少选择一位成员');
    }

    const response = await apiRequest<ChatConversationDto>({
      url: '/api/Chat/groups',
      method: 'POST',
      data: { software, name, participants: participantList },
      auth: true,
      disableMock: true
    });

    const currentUser = session.value?.username ?? '';
    const conversation = transformConversation(response, currentUser);
    const existingIndex = chatSessions.value.findIndex((item) => item.id === conversation.id);
    if (existingIndex >= 0) {
      chatSessions.value.splice(existingIndex, 1, conversation);
    } else {
      chatSessions.value.unshift(conversation);
    }

    if (initialMessage?.content || initialMessage?.options?.mediaBase64) {
      try {
        await sendChatMessage(conversation.id, initialMessage.content ?? '', initialMessage.options ?? {});
      } catch (error) {
        console.warn('createGroupConversation initial message failed', error);
      }
    }

    return conversation;
  }

  async function exportChatHistory(conversationId: string) {
    await ensureReady();
    const software = selectedSoftware.value;
    if (!software) {
      throw new Error('请选择软件位');
    }
    if (!conversationId) {
      throw new Error('请选择会话');
    }

    const response = await apiRequest<ChatMessagesResponse>({
      url: `/api/Chat/messages?software=${encodeURIComponent(software)}&conversationId=${encodeURIComponent(conversationId)}&limit=500`,
      method: 'GET',
      auth: true,
      disableMock: true
    });

    const currentUser = session.value?.username ?? '';
    const target = chatSessions.value.find((sessionItem) => sessionItem.id === conversationId);
    if (target) {
      target.messages = transformMessages(response?.messages ?? [], currentUser);
      target.preview = response?.conversation?.lastMessagePreview || target.preview;
      target.updatedAt = formatDateTime(response?.conversation?.updatedAt || target.updatedAt);
      target.unread = response?.conversation?.unreadCount ?? target.unread;
    }

    const conversation = response?.conversation;
    const participants = ensureArray<string>(conversation?.participants);
    const title = conversation?.isGroup
      ? conversation?.groupName || '群聊'
      : participants.find((name) => name && name !== currentUser) || participants[0] || conversationId;

    const lines: string[] = [];
    lines.push(`会话：${title}`);
    lines.push(`成员：${participants.join('、')}`);
    lines.push(`导出时间：${formatDateTime(new Date().toISOString())}`);
    lines.push('');

    ensureArray<ChatMessageDto>(response?.messages).forEach((message) => {
      const timestamp = formatDateTime(message.timestamp);
      const sender = message.sender === currentUser ? `${message.sender}(我)` : message.sender;
      const content = message.type === 'image' ? `[图片] ${message.content}` : message.content;
      lines.push(`[${timestamp}] ${sender}: ${content}`.trim());
      if (message.caption) {
        lines.push(`  说明：${message.caption}`);
      }
    });

    const filename = `${title || 'chat'}-${Date.now()}.txt`;
    return {
      filename,
      content: lines.join('\n')
    };
  }

  async function loadVerification(
    cardKey?: string,
    context?: { software: string; softwareCode?: string; agentAccount?: string; gateway?: string }
  ) {
    loading.verification = true;
    try {
      if (!cardKey) {
        verification.value = null;
        return null;
      }
      if (!context || !context.software) {
        throw new Error('缺少软件位信息，无法验证卡密');
      }
      if (!context.softwareCode) {
        throw new Error('缺少软件码信息，无法验证卡密');
      }
      const response = await apiRequest<CardVerificationResult>({
        url: '/api/card-verification/verify',
        method: 'POST',
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
