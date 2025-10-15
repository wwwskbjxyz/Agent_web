export interface ApiResponse<T> {
  code: number;
  message: string;
  data: T;
}

export interface WeChatBindingInfo {
  openId: string;
  unionId?: string | null;
  userType: string;
  userId: number;
  nickname?: string | null;
}

export interface WeChatBindResult {
  openId: string;
  unionId?: string | null;
  userType: string;
  userId: number;
  nickname?: string | null;
}

export interface WeChatTemplateConfig {
  instantCommunication?: string | null;
  blacklistAlert?: string | null;
  settlementNotice?: string | null;
  previewData?: Record<string, Record<string, string>>;
}

export interface WeChatNotificationResult {
  success: boolean;
  errorCode: number;
  errorMessage?: string | null;
}

export interface PlatformAuthorSoftware {
  softwareId: number;
  displayName: string;
  apiAddress: string;
  apiPort: number;
  softwareType: string;
  softwareCode: string;
  createdAt: string;
}

export interface PlatformAuthorProfile {
  username: string;
  email: string;
  displayName: string;
  apiAddress: string;
  apiPort: number;
  softwareType: string;
  softwareCode: string;
  softwares: PlatformAuthorSoftware[];
  primarySoftwareId?: number | null;
}

export interface PlatformAuthorLoginResponse {
  profile: PlatformAuthorProfile;
  token: string;
  expiresAt: string;
}

export interface PlatformAuthorUpdatePayload {
  softwareId: number;
  displayName: string;
  apiAddress: string;
  apiPort: number;
  softwareType: string;
}

export interface PlatformAuthorSoftwareCodeResponse {
  softwareCode: string;
}

export interface PlatformAgentProfile {
  username: string;
  email: string;
  displayName: string;
}

export interface PlatformBinding {
  bindingId: number;
  authorSoftwareId: number;
  softwareCode: string;
  softwareType: string;
  authorDisplayName: string;
  authorEmail: string;
  apiAddress: string;
  apiPort: number;
  authorAccount: string;
  authorPassword: string;
}

export interface PlatformAgentLoginResponse {
  agent: PlatformAgentProfile;
  token: string;
  expiresAt: string;
  bindings: PlatformBinding[];
}

export interface LoginPayload {
  username: string;
  password: string;
}

export interface LoginSuccess {
  username: string;
  softwareList: string[];
  token: string;
  expiresAt: string;
  isSuper: boolean;
}

export interface AgentProfile {
  user: string;
  password?: string;
  accountBalance?: number;
  accountTime?: number;
  duration?: string;
  authority?: string;
  cardTypeAuthName?: string;
  cardTypeAuthNameArray?: string[];
  cardsEnable?: boolean;
  remarks?: string;
  fNode?: string;
  stat?: number;
  deltm?: number;
  duration_?: number;
  duration__?: number;
  duration__text?: string;
  tatalParities?: number;
  parities?: number;
}

export interface RefreshUserInfoResponse {
  username: string;
  softwareList: string[];
  softwareAgentInfo: Record<string, AgentProfile>;
  token?: string;
  tokenExpiresAt?: string;
  isSuper: boolean;
}

export interface SoftwareAgent {
  username: string;
  balance: number;
  timeStock: number;
  cardTypes: string[];
  status: string;
  expiration?: string;
  permissions: Record<string, boolean>;
}

export interface SoftwareAgentInfo {
  softwareName: string;
  idc?: string;
  state: number;
  agentInfo?: SoftwareAgent;
  permissions: Record<string, boolean>;
}

export interface SoftwareListResponse {
  softwares: SoftwareAgentInfo[];
}

export interface AgentStatistics {
  totalCards: number;
  activeCards: number;
  usedCards: number;
  expiredCards: number;
  subAgents: number;
}

export interface AgentInfoResponse {
  agent?: AgentProfile;
  permissions: string[];
  statistics: AgentStatistics;
}

export interface DashboardStat {
  key: string;
  label: string;
  value: string;
  hint?: string;
}

export interface TrendPoint {
  date: string;
  value: number;
}

export interface TrendSeriesPoint {
  date: string;
  count: number;
}

export interface TrendSeries {
  agent: string;
  displayName: string;
  points: TrendSeriesPoint[];
  total: number;
}

export interface RecentActivationTrendResponse {
  points: TrendSeriesPoint[];
  categories: string[];
  series: TrendSeries[];
}

export interface SubAgentTrendSeriesPayload {
  name: string;
  values: number[];
  total: number;
}

export interface SubAgentTrendPayload {
  categories: string[];
  series: SubAgentTrendSeriesPayload[];
  total: number;
}

export interface SalesQueryFilters {
  cardType?: string;
  status?: string;
  agent?: string;
  includeDescendants?: boolean;
  startTime?: string;
  endTime?: string;
}

export interface SalesQueryResultCard {
  card: string;
  activateTime?: string;
}

export interface SalesQueryResultPayload {
  count: number;
  cards: SalesQueryResultCard[];
  settlements?: SettlementSummaryItem[];
  totalAmount?: number;
}

export interface UsageLocationStat {
  province: string;
  city: string;
  district: string;
  count: number;
}

export interface UsageDistributionResponse {
  provinces: UsageLocationStat[];
  cities: UsageLocationStat[];
  districts: UsageLocationStat[];
  resolvedTotal: number;
}

export interface AnnouncementResponse {
  content: string;
  updatedAt: number;
}

export interface AnnouncementItem {
  id: string;
  title: string;
  content: string;
  updatedAt?: string;
}

export interface UsageHeatmapItem {
  name: string;
  count: number;
  percentage: number;
}

export interface DashboardPayload {
  stats: DashboardStat[];
  trend: TrendPoint[];
  trendTotal?: number;
  trendMax?: number;
  subAgentTrend?: SubAgentTrendPayload;
  announcements: AnnouncementItem[];
  usageHeatmap: UsageHeatmapItem[];
  salesResult?: SalesQueryResultPayload;
  salesFilters?: SalesQueryFilters;
}

export interface CardInfo {
  prefix_Name: string;
  whom?: string;
  cardType?: string;
  fyI?: number;
  state?: string;
  bind?: number;
  openNum?: number;
  loginCount?: number;
  ip?: string;
  remarks?: string;
  createData_?: number;
  activateTime_?: number;
  expiredTime__?: number;
  lastLoginTime_?: number;
  owner?: string;
  machineCodes?: string[];
  machineCode?: string;
}

export interface CardListResponse {
  data: CardInfo[];
  total: number;
}

export interface CardListItem {
  key: string;
  owner: string;
  cardType: string;
  status: 'enabled' | 'disabled' | 'unknown';
  statusText: string;
  createdAt: string;
  activatedAt: string;
  expireAt: string;
  ip?: string;
  machineCodes: string[];
  remark?: string;
}

export interface CardSearchFilters {
  keyword?: string;
  cardType?: string;
  status?: 'enabled' | 'disabled' | 'unknown' | '';
  agent?: string;
  includeDescendants?: boolean;
  searchType?: number;
  keywords?: string[];
  machineCode?: string;
  ip?: string;
  startTime?: string;
  endTime?: string;
  page?: number;
  limit?: number;
}

export interface CardStatusMutation {
  cardKey: string;
  action: 'enable' | 'disable' | 'unban';
}

export interface AgentListItem {
  username: string;
  balance: number;
  timeStock: number;
  parities: number;
  totalParities: number;
  status: 'enabled' | 'disabled';
  depth: number;
  remark?: string;
  password?: string;
  cardTypes: string[];
  expiration?: string;
}

export interface AgentSearchFilters {
  keyword?: string;
  page?: number;
  limit?: number;
}

export interface AgentStatusMutation {
  usernames: string[];
  enable: boolean;
}

export interface AgentCreatePayload {
  username: string;
  password: string;
  remarks?: string;
  parities?: number;
  totalParities?: number;
  balance?: number;
  timeStock?: number;
  cardTypes?: string[];
}

export interface AgentAdjustBalancePayload {
  username: string;
  balance?: number;
  timeStock?: number;
  remark?: string;
}

export interface AgentUpdateRemarkPayload {
  username: string;
  remark: string;
}

export interface AgentUpdatePasswordPayload {
  username: string;
  password: string;
}

export interface AgentAssignCardTypesPayload {
  username: string;
  cardTypes: string[];
}

export interface LanzouLinkRecordDto {
  id: number;
  url: string;
  extractionCode: string;
  rawContent: string;
  createdAt: string;
}

export interface LanzouLinkListResponse {
  items: LanzouLinkRecordDto[];
  total: number;
}

export interface LinkRecordItem {
  id: number;
  url: string;
  extractionCode: string;
  createdAt: string;
  content: string;
}

export interface ChatConversationDto {
  conversationId: string;
  isGroup: boolean;
  groupName?: string;
  owner?: string;
  participants: string[];
  updatedAt: string;
  unreadCount: number;
  lastMessagePreview?: string;
}

export interface ChatContactDto {
  username: string;
  displayName: string;
  remark?: string;
}

export interface ChatMessageDto {
  id: string;
  timestamp: string;
  sender: string;
  content: string;
  type: string;
  caption?: string;
}

export interface ChatMessagesResponse {
  conversation: ChatConversationDto;
  messages: ChatMessageDto[];
}

export interface ChatMessageItem {
  id: string;
  sender: 'user' | 'system';
  content: string;
  time: string;
  type: string;
  caption?: string;
}

export interface ChatSessionItem {
  id: string;
  title: string;
  unread: number;
  updatedAt: string;
  preview?: string;
  isGroup: boolean;
  participants: string[];
  messages: ChatMessageItem[];
}

export interface ChatContactItem {
  username: string;
  displayName: string;
  remark?: string;
}

export interface CardTypeInfo {
  name: string;
  prefix?: string;
  duration: number;
  price: number;
  remarks?: string;
}

export interface CardTypeListResponse {
  items: {
    name: string;
    prefix?: string;
    duration: number;
    price: number;
    remarks?: string;
  }[];
}

export interface SettlementRateItem {
  cardType: string;
  price: number;
}

export interface SettlementAgentOption {
  username: string;
  displayName: string;
  hasPendingReminder?: boolean;
}

export interface SettlementSummaryItem {
  cardType: string;
  count: number;
  price: number;
  total: number;
}

export interface SettlementCycleInfoDto {
  effectiveDays: number;
  ownDays: number;
  effectiveTimeMinutes: number;
  ownTimeMinutes: number;
  effectiveTimeLabel: string;
  ownTimeLabel: string;
  isInherited: boolean;
  nextDueTimeUtc?: string | null;
  lastSettledTimeUtc?: string | null;
  isDue: boolean;
}

export interface SettlementBillItem {
  id: number;
  cycleStartUtc: string;
  cycleEndUtc: string;
  amount: number;
  isSettled: boolean;
  settledAtUtc?: string | null;
  note?: string | null;
}

export interface SettlementRateListResponse {
  rates: SettlementRateItem[];
  targetAgent?: string;
  agents?: SettlementAgentOption[];
  cycle?: SettlementCycleInfoDto | null;
  bills?: SettlementBillItem[];
  hasPendingReminder?: boolean;
}

export interface GenerateCardsRequestPayload {
  software: string;
  cardType: string;
  quantity: number;
  remarks?: string;
  customPrefix?: string;
}

export interface GenerateCardsResponse {
  generatedCount: number;
  cardType: string;
  sampleCards: string[];
  generatedCards: string[];
  generationId: string;
}

export interface SystemStatusResponse {
  machineName: string;
  osDescription: string;
  serverTime: string;
  bootTime?: string;
  cpuLoadPercentage?: number;
  totalMemoryBytes?: number;
  freeMemoryBytes?: number;
  usedMemoryBytes?: number;
  memoryUsagePercentage?: number;
  uptimeSeconds?: number;
  warnings: string[];
}

export interface CardVerificationRequest {
  cardKey: string;
  software: string;
  softwareCode?: string;
  agentAccount?: string;
}

export interface DownloadLinkDto {
  linkId: number;
  url: string;
  extractionCode: string;
  assignedAt: number;
  isNew: boolean;
}

export interface CardVerificationResult {
  cardKey: string;
  verificationPassed: boolean;
  attemptNumber: number;
  expiresAt?: number;
  download?: DownloadLinkDto | null;
  downloadHistory: DownloadLinkDto[];
  hasReachedLinkLimit: boolean;
  remainingLinkQuota: number;
  message: string;
}

export interface CardVerificationShareContext {
  software: string;
  softwareCode: string;
  softwareDisplayName?: string;
  agentAccount?: string;
  agentDisplayName?: string;
}

export type VerificationStatus = 'success' | 'warning' | 'error' | 'info';

export interface VerificationRecordItem {
  id: number;
  url: string;
  extractionCode: string;
  assignedAt: string;
  isNew: boolean;
}

export interface VerificationStats {
  attemptNumber: number;
  remainingDownloads: number;
  expiresAt?: string;
}

export interface VerificationPayload {
  status: VerificationStatus;
  message: string;
  downloadUrl?: string;
  extractionCode?: string;
  stats?: VerificationStats;
  history: VerificationRecordItem[];
}

export interface BlacklistLogItem {
  timestamp: string;
  software: string;
  ip: string;
  card: string;
  machineCode: string;
  event: string;
}

export interface BlacklistMachineItem {
  value: string;
  software: string;
  type: number;
  remarks?: string;
}

export interface BlacklistMachineCreatePayload {
  value: string;
  type: number;
  remarks?: string;
}

export interface UserProfile {
  id: string;
  name: string;
  role: string;
  avatar?: string;
  permissions: string[];
}