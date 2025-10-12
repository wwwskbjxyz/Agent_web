import type {
  Announcement,
  DashboardPayload,
  StatSummary,
  CardKeyItem,
  AgentItem,
  LinkRecordItem,
  ChatMessage,
  ChatSession,
  VerificationPayload
} from './types';

const baseStats: StatSummary[] = [
  { key: 'activeDevices', label: '在线节点', value: '128', delta: 12, trend: 'up' },
  { key: 'requests', label: '今日请求', value: '56.4K', delta: 4, trend: 'up' },
  { key: 'errors', label: '异常拦截', value: '142', delta: -6, trend: 'down' },
  { key: 'latency', label: '平均延迟', value: '182ms', delta: -12, trend: 'down' }
];

const announcements: Announcement[] = [
  {
    id: 1,
    title: 'v5.2 版本发布',
    content: '新增智能巡检、支持 WebHook 推送并升级风险识别模型。',
    date: '2024-03-08',
    author: 'SProtect 核心团队'
  },
  {
    id: 2,
    title: '华北节点维护通知',
    content: '3 月 12 日 02:00-04:00 将对华北节点进行升级，期间业务将自动切换到备节点。',
    date: '2024-03-05',
    author: '运维中心'
  },
  {
    id: 3,
    title: '卡密系统上线',
    content: '面向渠道代理开放卡密发放面板，支持批量导入与导出。',
    date: '2024-02-28',
    author: '渠道运营'
  }
];

const trend = Array.from({ length: 10 }).map((_, index) => ({
  date: `03-${index + 1}`,
  value: 60 + Math.round(Math.sin(index / 2) * 12 + index * 4)
}));

const usageHeatmap = [
  { name: '上海', active: 28, latency: 146 },
  { name: '北京', active: 32, latency: 138 },
  { name: '广州', active: 22, latency: 155 },
  { name: '成都', active: 18, latency: 164 }
];

export const dashboardMock: DashboardPayload = {
  stats: baseStats,
  trend,
  announcements,
  usageHeatmap
};

export const cardKeysMock: CardKeyItem[] = [
  {
    id: 'SP-2K38-FH18',
    product: '旗舰版 · 全节点',
    status: 'active',
    owner: '星火安全',
    createdAt: '2024-02-12 10:32',
    usedAt: '2024-02-28 21:10',
    remark: '华东渠道 - 已绑定'
  },
  {
    id: 'SP-9J71-PL09',
    product: '旗舰版 · 基础节点',
    status: 'inactive',
    owner: '渠道未分配',
    createdAt: '2024-02-15 09:12',
    remark: '待关联代理'
  },
  {
    id: 'SP-7K51-ZX66',
    product: '企业版 · 华南节点',
    status: 'expired',
    owner: '云栖科技',
    createdAt: '2023-12-02 16:54',
    usedAt: '2024-01-04 12:15',
    remark: '已自动续费'
  }
];

export const agentsMock: AgentItem[] = [
  {
    id: 'AG-001',
    name: '星火安全',
    level: '钻石代理',
    online: true,
    quota: 400,
    usage: 312,
    contact: 'ops@sprotect.dev'
  },
  {
    id: 'AG-002',
    name: '云栖科技',
    level: '金牌代理',
    online: false,
    quota: 260,
    usage: 198,
    contact: 'partner@yunqi.io'
  },
  {
    id: 'AG-003',
    name: '北辰数安',
    level: '银牌代理',
    online: true,
    quota: 120,
    usage: 88,
    contact: 'support@beichen.cn'
  }
];

export const linksMock: LinkRecordItem[] = [
  {
    id: 'LK-20240310-01',
    channel: 'Webhook · 异常告警',
    status: 'success',
    createdAt: '2024-03-10 19:24',
    description: '已成功推送至安全运营中心'
  },
  {
    id: 'LK-20240310-02',
    channel: '飞书机器人',
    status: 'pending',
    createdAt: '2024-03-10 19:20',
    description: '等待渠道授权确认'
  },
  {
    id: 'LK-20240309-01',
    channel: '钉钉群机器人',
    status: 'failed',
    createdAt: '2024-03-09 23:58',
    description: '签名验证失败，已自动重试'
  }
];

export const chatHistoryMock: ChatMessage[] = [
  {
    id: 'msg-1',
    sender: 'system',
    content: '您好，欢迎联系 SProtect 智能客服，我是巡航助手。',
    time: '09:41'
  },
  {
    id: 'msg-2',
    sender: 'user',
    content: '晚上节点延迟有点高，可以帮忙排查吗？',
    time: '09:42'
  },
  {
    id: 'msg-3',
    sender: 'system',
    content: '已提交巡检任务，预计 3 分钟出结果，同时建议关注运维公告。',
    time: '09:42'
  }
];

export const chatSessionsMock: ChatSession[] = [
  {
    id: 'session-1',
    title: '华北节点巡检',
    unread: 0,
    messages: chatHistoryMock
  },
  {
    id: 'session-2',
    title: '渠道授权咨询',
    unread: 2,
    messages: [
      {
        id: 'msg-4',
        sender: 'user',
        content: '请问如何扩容卡密数量？',
        time: '08:12'
      },
      {
        id: 'msg-5',
        sender: 'system',
        content: '您好，旗舰版支持在线扩容，稍后将会有渠道经理联系您。',
        time: '08:13'
      }
    ]
  }
];

export const verificationMock: VerificationPayload = {
  status: 'success',
  message: '卡密可用，已为您生成最新资源包。',
  downloadUrl: 'https://cdn.sprotect.dev/downloads/sprotect-agent.zip',
  stats: {
    totalActivations: 3,
    remainingDownloads: 2,
    bindDevice: 'DESKTOP-239A · Windows 11'
  },
  history: [
    {
      code: 'SP-2K38-FH18',
      verifiedAt: '2024-03-11 19:26',
      status: 'success',
      device: 'DESKTOP-239A',
      note: '新版本激活'
    },
    {
      code: 'SP-7K51-ZX66',
      verifiedAt: '2024-03-05 11:12',
      status: 'warning',
      device: 'iPhone 15 Pro',
      note: '重复验证提醒'
    },
    {
      code: 'SP-9J71-PL09',
      verifiedAt: '2024-02-28 08:54',
      status: 'error',
      note: '卡密已过期'
    }
  ]
};
