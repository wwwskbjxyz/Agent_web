window.addEventListener('DOMContentLoaded', () => {
  const API_BASE_URL = (() => {
    const override = window.API_BASE_URL_OVERRIDE;
    if (override && typeof override === 'string') {
      return override.replace(/\/$/, '');
    }
    if (window.location && window.location.origin) {
      return `${window.location.origin.replace(/\/$/, '')}/api`;
    }
    return '/api';
  })();
  const TOKEN_STORAGE_KEY = 'sp_agent_token';
  const TOKEN_EXPIRY_STORAGE_KEY = 'sp_agent_token_exp';
  let authToken = null;
  let authTokenExpiry = null;
  let lastUnauthorizedNotice = 0;

  function restoreTokenFromStorage() {
    try {
      const storedToken = localStorage.getItem(TOKEN_STORAGE_KEY);
      const storedExpiry = localStorage.getItem(TOKEN_EXPIRY_STORAGE_KEY);
      authToken = storedToken || null;
      if (storedExpiry) {
        const parsed = Date.parse(storedExpiry);
        authTokenExpiry = Number.isNaN(parsed) ? null : parsed;
      } else {
        authTokenExpiry = null;
      }

      if (!authToken || (authTokenExpiry && authTokenExpiry <= Date.now())) {
        clearAuthToken();
      }
    } catch (err) {
      console.warn('restore token failed', err);
      authToken = null;
      authTokenExpiry = null;
    }
  }

  function updateTokenExpiry(expiresAt) {
    if (!expiresAt) {
      authTokenExpiry = null;
      try {
        localStorage.removeItem(TOKEN_EXPIRY_STORAGE_KEY);
      } catch (err) {
        console.warn('clear token expiry failed', err);
      }
      return;
    }

    const parsed = Date.parse(expiresAt);
    if (Number.isNaN(parsed)) {
      return;
    }

    authTokenExpiry = parsed;
    try {
      localStorage.setItem(TOKEN_EXPIRY_STORAGE_KEY, new Date(parsed).toISOString());
    } catch (err) {
      console.warn('store token expiry failed', err);
    }
  }

  function setAuthToken(token, expiresAt) {
    if (token) {
      authToken = token;
      try {
        localStorage.setItem(TOKEN_STORAGE_KEY, token);
      } catch (err) {
        console.warn('store token failed', err);
      }
    }
    updateTokenExpiry(expiresAt);
  }

  function clearAuthToken() {
    authToken = null;
    authTokenExpiry = null;
    try {
      localStorage.removeItem(TOKEN_STORAGE_KEY);
      localStorage.removeItem(TOKEN_EXPIRY_STORAGE_KEY);
    } catch (err) {
      console.warn('clear token storage failed', err);
    }
  }

  function renderActionButtons(container, actions, options = {}) {
    if (!container || !Array.isArray(actions)) {
      return;
    }
    container.innerHTML = '';
    const helper = window.MobileFriendly;
    if (helper && typeof helper.renderActions === 'function') {
      helper.renderActions(container, actions, options);
      return;
    }
    actions.forEach(action => {
      if (!action) return;
      const elementName = action.element || 'button';
      const el = document.createElement(elementName);
      if (elementName !== 'button') {
        if (elementName === 'a') {
          if (action.href) el.href = action.href;
          if (action.target) el.target = action.target;
          if (action.rel) el.rel = action.rel;
        }
      } else {
        el.type = 'button';
      }
      el.className = action.className || 'px-3 py-1 glass rounded hover:opacity-90 text-sm';
      if (action.html) {
        el.innerHTML = action.html;
      } else {
        el.textContent = action.label || action.text || '操作';
      }
      if (typeof action.onClick === 'function') {
        el.addEventListener('click', action.onClick);
      }
      container.appendChild(el);
    });
  }

  function isTokenValid() {
    if (!authToken) {
      return false;
    }
    if (!authTokenExpiry) {
      return true;
    }
    return authTokenExpiry > Date.now();
  }

  function handleUnauthorized(message = '登录状态已过期，请重新登录') {
    const now = Date.now();
    const shouldNotify = now - lastUnauthorizedNotice >= 1000;
    if (shouldNotify) {
      lastUnauthorizedNotice = now;
    }
    performClientLogout({ message: shouldNotify ? message : undefined, silent: !shouldNotify });
  }

  restoreTokenFromStorage();

  let currentUser = null;
  let currentSoftware = null;
  const cardTypeCache = new Map();
  const agentOptionsCache = new Map();
  let softwareOptions = [];
  let linkRecordsBtn = null;
  let managementLinkRecordsBtn = null;
  let chatBtn = null;
  let chatUnreadCountEl = null;
  let managementChatBtn = null;
  let managementChatUnreadEl = null;
  let chatUnreadTimer = null;

  const pageType = document.body?.dataset?.page || 'login';
  const LOGIN_MESSAGE_STORAGE_KEY = 'sp_agent_login_msg';
  const SELECTED_SOFTWARE_STORAGE_KEY = 'sp_agent_selected_software';

  function pushLoginMessage(message, type = 'error') {
    try {
      if (!message) return;
      sessionStorage.setItem(LOGIN_MESSAGE_STORAGE_KEY, JSON.stringify({ message, type }));
    } catch (err) {
      console.warn('store login message failed', err);
    }
  }

  function popLoginMessage() {
    try {
      const raw = sessionStorage.getItem(LOGIN_MESSAGE_STORAGE_KEY);
      if (raw) {
        sessionStorage.removeItem(LOGIN_MESSAGE_STORAGE_KEY);
        const parsed = JSON.parse(raw);
        if (parsed && parsed.message) {
          return { message: parsed.message, type: parsed.type || 'error' };
        }
      }
    } catch (err) {
      console.warn('restore login message failed', err);
    }
    return null;
  }

  function storeSelectedSoftware(software) {
    try {
      if (software && typeof software === 'object') {
        localStorage.setItem(SELECTED_SOFTWARE_STORAGE_KEY, JSON.stringify(software));
      } else {
        localStorage.removeItem(SELECTED_SOFTWARE_STORAGE_KEY);
      }
    } catch (err) {
      console.warn('store selected software failed', err);
    }
  }

  function restoreSelectedSoftware() {
    try {
      const raw = localStorage.getItem(SELECTED_SOFTWARE_STORAGE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (parsed && typeof parsed === 'object') {
          return parsed;
        }
      }
    } catch (err) {
      console.warn('restore selected software failed', err);
    }
    return null;
  }

  function clearSelectedSoftware() {
    try {
      localStorage.removeItem(SELECTED_SOFTWARE_STORAGE_KEY);
    } catch (err) {
      console.warn('clear selected software failed', err);
    }
  }

  updateLinkRecordsButtons();
  updateChatButtons();

  let trendChart = null;
  let subAgentTrendChart = null;
  let usageChart = null;
  let chinaMapLoaded = false;
  let lastUsageData = null;
  let lastGeneratedCards = [];
  let lastGeneratedCardType = '';
  let systemStatusLoading = false;

  const trendChartEl = document.getElementById('trend-chart');
  const trendEmpty = document.getElementById('trend-empty');
  const trendSummary = document.getElementById('trend-summary');
  const subAgentTrendChartEl = document.getElementById('subagent-trend-chart');
  const subAgentTrendEmpty = document.getElementById('subagent-trend-empty');
  const subAgentTrendSummary = document.getElementById('subagent-trend-summary');

  // ===== util =====
  const toast = document.getElementById('toast');
  const toastIcon = document.getElementById('toast-icon');
  const toastMessage = document.getElementById('toast-message');
  const echartsAvailable = typeof window.echarts !== 'undefined';
  const announcementCardEl = document.getElementById('announcement-card');
  const announcementContentEl = document.getElementById('announcement-content');
  const announcementUpdatedEl = document.getElementById('announcement-updated');
  function showToast(message, type = 'success') {
    toastMessage.textContent = message;
    if (type === 'success') toastIcon.className = 'fa fa-check-circle text-green-400';
    else if (type === 'error') toastIcon.className = 'fa fa-exclamation-circle text-red-400';
    else toastIcon.className = 'fa fa-info-circle text-blue-400';
    toast.classList.remove('translate-x-full');
    setTimeout(() => toast.classList.add('translate-x-full'), 2200);
  }
  function api(url, opts = {}) {
    const headers = { 'Content-Type': 'application/json', ...(opts.headers || {}) };
    if (authToken && isTokenValid()) {
      headers.Authorization = `Bearer ${authToken}`;
    }
    const final = { credentials: 'include', ...opts, headers };
    console.log('[API]', url, final);
    return fetch(url, final).then(response => {
      if (response.status === 401) {
        handleUnauthorized();
      }
      return response;
    });
  }

  function openLinkRecordsPage() {
    window.location.href = '/pages/link-records/index.html';
  }

  function openChatPage() {
    window.location.href = '/pages/chat/index.html';
  }

  function updateLinkRecordsButtons() {
    const visible = !!(currentUser && currentUser.isSuper);
    if (linkRecordsBtn) linkRecordsBtn.classList.toggle('hidden', !visible);
    if (managementLinkRecordsBtn) managementLinkRecordsBtn.classList.toggle('hidden', !visible);
  }

  function updateChatButtons() {
    const visible = !!currentUser;
    if (chatBtn) {
      chatBtn.classList.toggle('hidden', !visible);
      if (!visible) {
        setChatUnreadBadge(chatUnreadCountEl, 0);
      }
    }
    if (managementChatBtn) {
      managementChatBtn.classList.toggle('hidden', !visible);
      if (!visible) {
        setChatUnreadBadge(managementChatUnreadEl, 0);
      }
    }
  }

  function setChatUnreadBadge(element, count) {
    if (!element) return;
    if (count > 0) {
      element.textContent = count > 99 ? '99+' : String(count);
      element.classList.remove('hidden');
    } else {
      element.textContent = '0';
      element.classList.add('hidden');
    }
  }

  async function refreshChatUnread({ silent = false } = {}) {
    if (!authToken || !isTokenValid()) {
      return;
    }

    try {
      const resp = await api(`${API_BASE_URL}/Chat/unreadTotal`);
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      const data = await resp.json();
      if (data?.code === 0) {
        const value = data?.data?.count ?? data?.data?.Count ?? 0;
        const count = Number(value) || 0;
        setChatUnreadBadge(chatUnreadCountEl, count);
        setChatUnreadBadge(managementChatUnreadEl, count);
      } else if (!silent) {
        showToast(data?.message || '未能获取聊天未读消息', 'error');
      }
    } catch (err) {
      console.error('refreshChatUnread error', err);
      if (!silent) {
        showToast('未能获取聊天未读消息：' + err.message, 'error');
      }
    }
  }

  function startChatUnreadPolling() {
    stopChatUnreadPolling();
    refreshChatUnread({ silent: true });
    chatUnreadTimer = setInterval(() => refreshChatUnread({ silent: true }), 30000);
  }

  function stopChatUnreadPolling() {
    if (chatUnreadTimer) {
      clearInterval(chatUnreadTimer);
      chatUnreadTimer = null;
    }
    setChatUnreadBadge(chatUnreadCountEl, 0);
    setChatUnreadBadge(managementChatUnreadEl, 0);
  }

  function formatBytes(bytes) {
    const value = Number(bytes);
    if (!Number.isFinite(value)) return '--';
    if (value < 0) return '--';
    if (value === 0) return '0B';
    const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
    let size = value;
    let index = 0;
    while (size >= 1024 && index < units.length - 1) {
      size /= 1024;
      index += 1;
    }
    const precision = size >= 100 ? 0 : size >= 10 ? 1 : 2;
    return `${size.toFixed(precision).replace(/\.0+$/, '')}${units[index]}`;
  }

  function formatDuration(seconds) {
    const totalSeconds = Number(seconds);
    if (!Number.isFinite(totalSeconds)) return '--';
    if (totalSeconds <= 0) return '0秒';
    const parts = [];
    let remaining = Math.floor(totalSeconds);
    const days = Math.floor(remaining / 86400);
    if (days > 0) { parts.push(`${days}天`); remaining -= days * 86400; }
    const hours = Math.floor(remaining / 3600);
    if (hours > 0) { parts.push(`${hours}小时`); remaining -= hours * 3600; }
    const minutes = Math.floor(remaining / 60);
    if (minutes > 0) { parts.push(`${minutes}分钟`); remaining -= minutes * 60; }
    if (parts.length === 0 || remaining > 0) {
      parts.push(`${remaining}秒`);
    }
    return parts.slice(0, 3).join('');
  }

  function formatDateTime(value) {
    if (!value) return '--';
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return '--';
    return date.toLocaleString();
  }

  function updateAnnouncementCard(data) {
    if (!announcementCardEl || !announcementContentEl || !announcementUpdatedEl) return;
    const content = (data?.content ?? '').toString().trim();
    if (content) {
      announcementContentEl.textContent = content;
      const ts = Number(data?.updatedAt ?? 0);
      if (Number.isFinite(ts) && ts > 0) {
        announcementUpdatedEl.textContent = '更新时间：' + fmtFullDateTime(ts);
      } else {
        announcementUpdatedEl.textContent = '';
      }
      announcementCardEl.classList.add('active');
    } else {
      announcementContentEl.textContent = '暂无公告';
      announcementUpdatedEl.textContent = '';
      announcementCardEl.classList.remove('active');
    }
  }

  async function loadAnnouncement({ silent = false } = {}) {
    if (!announcementCardEl) return;
    try {
      const resp = await api(`${API_BASE_URL}/System/announcement`);
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      const data = await resp.json();
      if (data?.code === 0) {
        updateAnnouncementCard(data?.data || {});
      } else {
        updateAnnouncementCard({});
        if (!silent) showToast(data?.message || '公告获取失败', 'error');
      }
    } catch (err) {
      console.error('loadAnnouncement error:', err);
      updateAnnouncementCard({});
      if (!silent) showToast('公告获取失败：' + err.message, 'error');
    }
  }

  function normalizeProvinceForMap(name) {
    if (!name) return '';
    let normalized = String(name).trim();
    if (!normalized) return '';
    normalized = normalized
      .replace(/省$/u, '')
      .replace(/市$/u, '')
      .replace(/特别行政区$/u, '')
      .replace(/壮族自治区$/u, '')
      .replace(/回族自治区$/u, '')
      .replace(/维吾尔自治区$/u, '')
      .replace(/自治区$/u, '');
    return normalized;
  }

  function resetSystemStatusDisplay() {
    if (systemStatusCpu) systemStatusCpu.textContent = '--';
    if (systemStatusMemory) systemStatusMemory.textContent = '--';
    if (systemStatusBoot) systemStatusBoot.textContent = '--';
    if (systemStatusUptime) systemStatusUptime.textContent = '--';
    if (systemStatusTime) systemStatusTime.textContent = '--';
    if (systemStatusUpdated) systemStatusUpdated.textContent = '';
    if (systemStatusMessage) systemStatusMessage.classList.add('hidden');
  }

  function showGeneratedCards(cards = [], cardType = '') {
    if (!modalGenerated) return;
    const normalized = Array.isArray(cards) ? cards.filter(item => typeof item === 'string' && item.trim()).map(item => item.trim()) : [];
    lastGeneratedCards = normalized;
    lastGeneratedCardType = cardType || lastGeneratedCardType || '';

    if (generatedTitle) {
      generatedTitle.textContent = lastGeneratedCardType ? `卡密类型：${lastGeneratedCardType}` : '生成卡密';
    }

    if (btnCopyGenerated) btnCopyGenerated.disabled = !normalized.length;
    if (btnExportGenerated) btnExportGenerated.disabled = !normalized.length;

    if (generatedList) {
      generatedList.innerHTML = '';
      if (normalized.length) {
        const frag = document.createDocumentFragment();
        normalized.forEach((card, idx) => {
          const row = document.createElement('div');
          row.className = 'px-3 py-2 bg-white/5 border border-white/10 rounded break-all flex items-center justify-between gap-3';
          const cardText = document.createElement('span');
          cardText.className = 'font-mono text-sm text-white flex-1';
          cardText.textContent = card;
          row.appendChild(cardText);
          const indexTag = document.createElement('span');
          indexTag.className = 'text-xs text-gray-400 ml-3 whitespace-nowrap';
          indexTag.textContent = '#' + (idx + 1);
          row.appendChild(indexTag);
          frag.appendChild(row);
        });
        generatedList.appendChild(frag);
      } else {
        const empty = document.createElement('div');
        empty.className = 'text-sm text-gray-400';
        empty.textContent = '接口未返回卡密内容';
        generatedList.appendChild(empty);
      }
    }

    if (generatedCount) {
      generatedCount.textContent = normalized.length ? `共 ${normalized.length} 张` : '';
    }

    modalGenerated.style.display = 'flex';
  }

  async function handleCopyGeneratedCards() {
    if (!lastGeneratedCards.length) {
      showToast('暂无可复制的卡密', 'error');
      return;
    }
    const text = lastGeneratedCards.join('\n');
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        await navigator.clipboard.writeText(text);
      } else {
        throw new Error('clipboard api not available');
      }
      showToast('已复制到剪贴板');
    } catch (err) {
      try {
        const textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.setAttribute('readonly', 'readonly');
        textarea.style.position = 'absolute';
        textarea.style.left = '-9999px';
        document.body.appendChild(textarea);
        textarea.select();
        const success = document.execCommand('copy');
        document.body.removeChild(textarea);
        if (success) {
          showToast('已复制到剪贴板');
          return;
        }
        throw new Error('execCommand copy failed');
      } catch (fallbackErr) {
        console.error('copy failed', err, fallbackErr);
        showToast('复制失败，请手动复制', 'error');
      }
    }
  }

  function handleExportGeneratedCards() {
    if (!lastGeneratedCards.length) {
      showToast('暂无可导出的卡密', 'error');
      return;
    }
    const now = new Date();
    const pad = (n) => String(n).padStart(2, '0');
    const filename = `${lastGeneratedCardType || 'cards'}-${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}.txt`;
    const blob = new Blob([lastGeneratedCards.join('\r\n')], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    showToast('TXT 已导出');
  }
  function fmtYYYYMMDD(sec) {
    if (!sec) return '-';
    const ms = Number(sec) * 1000;
    const d = new Date(ms);
    const y = d.getFullYear();
    const m = String(d.getMonth()+1).padStart(2, '0');
    const da = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${da}`;
  }
  function fmtFullDateTime(sec) {
    const numeric = Number(sec);
    if (!Number.isFinite(numeric) || numeric <= 0) return '-';
    const ms = numeric > 1e12 ? numeric : numeric * 1000;
    const d = new Date(ms);
    if (Number.isNaN(d.getTime())) return '-';
    const pad = (v) => String(v).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
  }

  function toDatetimeLocal(date) {
    if (!(date instanceof Date)) return '';
    const pad = (v) => String(v).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  function formatDateTimeForApi(value) {
    if (!value) return '';
    // datetime-local value already uses local time, convert to yyyy-MM-dd HH:mm
    const normalized = value.replace('T', ' ').trim();
    return normalized.length >= 16 ? normalized.slice(0, 16) : normalized;
  }

  async function ensureCardTypes(software) {
    if (!software) return [];
    if (cardTypeCache.has(software)) return cardTypeCache.get(software);
    try {
      const resp = await api(`${API_BASE_URL}/CardType/getCardTypeList`, { method: 'POST', body: JSON.stringify({ software }) });
      const data = await resp.json();
      const items = data?.data?.items || data?.data?.list || [];
      const names = items
        .map(it => it?.name || it?.cardType || it?.cardTypeName || it?.typeName || '')
        .filter(name => typeof name === 'string' && name.trim().length > 0)
        .map(name => name.trim());
      cardTypeCache.set(software, names);
      return names;
    } catch (err) {
      console.error('ensureCardTypes error:', err);
      return [];
    }
  }

  const NBSP = String.fromCharCode(160);

  function extractAgentName(agent) {
    if (!agent || typeof agent !== 'object') return '';
    const name = agent.username ?? agent.user ?? agent.account ?? agent.name;
    if (typeof name !== 'string') return '';
    const trimmed = name.trim();
    return trimmed.length ? trimmed : '';
  }

  function extractAgentParent(agent) {
    if (!agent || typeof agent !== 'object') return '';
    const parentFields = [agent.parent, agent.parentUser, agent.parent_name, agent.parentName];
    for (const field of parentFields) {
      if (typeof field === 'string') {
        const trimmed = field.trim();
        if (trimmed.length) return trimmed;
      }
    }
    const hierarchy = parseAgentHierarchy(agent);
    if (hierarchy.length >= 2) {
      return hierarchy[hierarchy.length - 2];
    }
    return '';
  }

  function parseAgentHierarchy(agent) {
    const hierarchy = agent?.hierarchy;
    if (Array.isArray(hierarchy)) {
      return hierarchy
        .map(item => (typeof item === 'string' ? item.trim() : ''))
        .filter(Boolean);
    }
    const fnode = typeof agent?.fnode === 'string' ? agent.fnode : '';
    if (fnode) {
      const matches = fnode.match(/\[([^\]]+)\]/g) || [];
      return matches
        .map(token => token.slice(1, -1).trim())
        .filter(Boolean);
    }
    return [];
  }

  function buildAgentTreeOptions(rawList) {
    const list = Array.isArray(rawList) ? rawList : [];
    const me = (currentUser?.username || '').trim();
    const meKey = me.toLowerCase();
    const nodes = new Map();

    const ensureNode = (username) => {
      const key = username.toLowerCase();
      if (!nodes.has(key)) {
        nodes.set(key, { username, parent: '', children: [] });
      }
      return nodes.get(key);
    };

    list.forEach(item => {
      const username = extractAgentName(item);
      if (!username) return;
      const node = ensureNode(username);
      const parent = extractAgentParent(item);
      if (parent) node.parent = parent;
      const hierarchy = parseAgentHierarchy(item);
      if (!node.parent && hierarchy.length >= 2) node.parent = hierarchy[hierarchy.length - 2];
    });

    nodes.forEach(node => { node.children = []; });
    nodes.forEach(node => {
      const parentName = (node.parent || '').trim();
      if (!parentName) return;
      const parentNode = nodes.get(parentName.toLowerCase());
      if (parentNode && parentNode !== node) {
        parentNode.children.push(node);
      }
    });

    const roots = [];
    nodes.forEach(node => {
      const parentName = (node.parent || '').trim();
      const parentKey = parentName.toLowerCase();
      if (!parentName || !nodes.has(parentKey) || (me && parentKey === meKey)) {
        if (!(me && node.username.toLowerCase() === meKey)) {
          roots.push(node);
        }
      }
    });

    const result = [];
    if (me) {
      result.push({ username: me, label: `${me}（自己）`, depth: 0 });
    }

    const visited = new Set();
    const toKey = (name) => name.toLowerCase();

    function flatten(nodesList, depth, prefixSegments) {
      if (!Array.isArray(nodesList) || !nodesList.length) return;
      const sorted = nodesList.slice().sort((a, b) => a.username.localeCompare(b.username, 'zh-CN'));
      sorted.forEach((node, index) => {
        const key = toKey(node.username);
        if (visited.has(key)) return;
        visited.add(key);
        const isLast = index === sorted.length - 1;
        let labelPrefix = '';
        prefixSegments.forEach(hasNext => {
          labelPrefix += hasNext ? `│${NBSP}` : `${NBSP}${NBSP}`;
        });
        const showBranch = depth > 0 || !me;
        if (showBranch) {
          labelPrefix += isLast ? `└─${NBSP}` : `├─${NBSP}`;
        }
        result.push({ username: node.username, label: `${labelPrefix}${node.username}`, depth });
        const childSegments = prefixSegments.slice();
        childSegments.push(!isLast);
        if (Array.isArray(node.children) && node.children.length) {
          flatten(node.children, depth + 1, childSegments);
        }
      });
    }

    const initialDepth = me ? 1 : 0;
    flatten(roots, initialDepth, []);

    if (!result.length && me) {
      return [{ username: me, label: `${me}（自己）`, depth: 0 }];
    }

    return result;
  }

  function defaultAgentOptionLabel() {
    return currentUser?.username
      ? `${currentUser.username}（默认，含下级）`
      : '默认（当前代理）';
  }

  function populateAgentSelect(selectEl, options, defaultLabel, previousValue = '') {
    if (!selectEl) return;
    const prior = typeof previousValue === 'string' ? previousValue.trim() : '';
    selectEl.innerHTML = '';
    const defaultOpt = document.createElement('option');
    defaultOpt.value = '';
    defaultOpt.textContent = defaultLabel;
    selectEl.appendChild(defaultOpt);
    options.forEach(opt => {
      if (!opt || !opt.username) return;
      const option = document.createElement('option');
      option.value = opt.username;
      option.textContent = opt.label || opt.username;
      if (typeof opt.depth === 'number') option.dataset.depth = String(opt.depth);
      selectEl.appendChild(option);
    });
    if (prior && options.some(opt => opt.username === prior)) {
      selectEl.value = prior;
    } else {
      selectEl.value = '';
    }
  }

  async function ensureAgentOptions(software) {
    if (!software) return [];
    if (agentOptionsCache.has(software)) return agentOptionsCache.get(software);
    try {
      const payload = { software, searchType: 1, keyword: '', page: 1, limit: 2000 };
      const resp = await api(`${API_BASE_URL}/Agent/getSubAgentList`, { method: 'POST', body: JSON.stringify(payload) });
      const data = await resp.json();
      const inner = data?.data || {};
      const list = inner.items || inner.list || inner.agents || inner.data || [];
      const options = buildAgentTreeOptions(list);
      agentOptionsCache.set(software, options);
      return options;
    } catch (err) {
      console.error('ensureAgentOptions error:', err);
      const fallback = currentUser?.username
        ? [{ username: currentUser.username.trim(), label: `${currentUser.username.trim()}（自己）`, depth: 0 }]
        : [];
      agentOptionsCache.set(software, fallback);
      return fallback;
    }
  }

  function populateCardFilterAgents(options) {
    const filterValue = (typeof filter !== 'undefined' && filter && typeof filter.whom === 'string')
      ? filter.whom.trim()
      : '';
    const previous = filterValue || (filterWhom?.value || '');
    populateAgentSelect(filterWhom, options, defaultAgentOptionLabel(), previous);
    if (previous && options.some(opt => opt.username === previous)) {
      filterWhom.value = previous;
    } else {
      filterWhom.value = '';
    }
  }

  function populateSalesAgentOptions(options) {
    populateAgentSelect(salesAgentSelect, options, defaultAgentOptionLabel(), salesAgentSelect?.value || '');
  }

  function setSalesDefaultRange() {
    const now = new Date();
    const end = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const start = new Date(end.getTime() - 2 * 24 * 60 * 60 * 1000);
    salesStartInput.value = toDatetimeLocal(start);
    salesEndInput.value = toDatetimeLocal(end);
  }

  function populateSalesSoftwareOptions(list) {
    salesSoftwareSelect.innerHTML = '';
    if (!list.length) {
      const opt = document.createElement('option');
      opt.value = '';
      opt.textContent = '暂无可用软件';
      salesSoftwareSelect.appendChild(opt);
      salesSoftwareSelect.disabled = true;
      salesQueryBtn.disabled = true;
      return;
    }
    salesSoftwareSelect.disabled = false;
    salesQueryBtn.disabled = false;
    list.forEach((soft, idx) => {
      const name = soft?.softwareName || soft?.name || soft?.id || soft?.software || '';
      if (!name) return;
      const option = document.createElement('option');
      option.value = name;
      option.textContent = name;
      salesSoftwareSelect.appendChild(option);
      if (idx === 0) salesSoftwareSelect.value = name;
    });
  }

  async function refreshSalesCardTypes(software) {
    salesCardTypeSelect.innerHTML = '<option value="">全部</option>';
    if (!software) return;
    const types = await ensureCardTypes(software);
    types.forEach(name => {
      const opt = document.createElement('option');
      opt.value = name;
      opt.textContent = name;
      salesCardTypeSelect.appendChild(opt);
    });
    salesCardTypeSelect.value = '';
  }

  async function refreshSalesAgents(software) {
    const options = await ensureAgentOptions(software);
    populateSalesAgentOptions(options);
  }

  function invalidateAgentCache(software) {
    if (!software) return;
    agentOptionsCache.delete(software);
  }

  async function refreshAgentOptionsForCurrentSoftware() {
    if (!currentSoftware) return;
    const options = await ensureAgentOptions(currentSoftware.softwareName);
    populateCardFilterAgents(options);
  }

  async function runSalesQuery() {
    const software = salesSoftwareSelect.value;
    if (!software) {
      salesError.textContent = '请选择软件位';
      salesError.classList.remove('hidden');
      salesResultWrap.classList.add('hidden');
      return;
    }

    const payload = {
      software,
      cardTypes: [],
      status: (salesStatusSelect.value || '').trim(),
      includeDescendants: !!salesIncludeSub.checked,
      whomList: [],
    };
    const cardType = (salesCardTypeSelect.value || '').trim();
    if (cardType) payload.cardTypes = [cardType];
    const agent = (salesAgentSelect.value || '').trim();
    if (agent) payload.whomList = [agent];
    const start = formatDateTimeForApi(salesStartInput.value);
    const end = formatDateTimeForApi(salesEndInput.value);
    if (start) payload.startTime = start;
    if (end) payload.endTime = end;

    salesError.classList.add('hidden');
    salesLoading.classList.remove('hidden');
    const originalText = salesQueryBtn.innerHTML;
    salesQueryBtn.disabled = true;
    salesQueryBtn.innerHTML = '<i class="fa fa-circle-notch fa-spin mr-2"></i>查询中...';

    try {
      const resp = await api(`${API_BASE_URL}/Card/countActivatedCards`, { method: 'POST', body: JSON.stringify(payload) });
      const data = await resp.json();
      if (data?.code === 0) {
        renderSalesResults(data.data || {}, payload);
      } else {
        salesResultWrap.classList.add('hidden');
        salesError.textContent = data?.message || '查询失败';
        salesError.classList.remove('hidden');
      }
    } catch (err) {
      console.error('runSalesQuery error:', err);
      salesResultWrap.classList.add('hidden');
      salesError.textContent = '查询失败：' + err.message;
      salesError.classList.remove('hidden');
    } finally {
      salesLoading.classList.add('hidden');
      salesQueryBtn.disabled = false;
      salesQueryBtn.innerHTML = originalText;
    }
  }

  function renderSalesResults(result, payload) {
    const count = Number(result?.count ?? 0);
    salesCount.textContent = count;
    const cards = Array.isArray(result?.cards) ? result.cards : [];
    salesResultBody.innerHTML = '';
    if (!cards.length) {
      const tr = document.createElement('tr');
      tr.innerHTML = '<td colspan="2" class="px-4 py-4 text-center text-gray-400">暂无数据</td>';
      salesResultBody.appendChild(tr);
    } else {
      cards.forEach(item => {
        const card = item?.card || item?.prefix_Name || item?.key || '-';
        const timeText = item?.activateTimeText || fmtFullDateTime(item?.activateTime);
        const tr = document.createElement('tr');
        tr.className = 'border-b border-white/10';
        tr.innerHTML = `
          <td class="px-4 py-3 font-mono text-sm">${card}</td>
          <td class="px-4 py-3">${timeText || '-'}</td>`;
        salesResultBody.appendChild(tr);
      });
    }

    const start = payload.startTime || '不限';
    const end = payload.endTime || '不限';
    const agentText = payload.whomList && payload.whomList.length
      ? `${payload.whomList.join(', ')}${payload.includeDescendants ? '（含下级）' : '（仅自身）'}`
      : `${currentUser?.username || '当前代理'}${payload.includeDescendants ? '（含下级）' : '（仅自身）'}`;
    salesSummary.textContent = `时间：${start} ~ ${end}，代理：${agentText}`;
    salesResultWrap.classList.remove('hidden');
  }


  function ensureTrendChart() {
    if (!echartsAvailable || !trendChartEl) return null;
    if (!trendChart) { trendChart = echarts.init(trendChartEl); }
    return trendChart;
  }

  function ensureSubAgentTrendChart() {
    if (!echartsAvailable || !subAgentTrendChartEl) return null;
    if (!subAgentTrendChart) { subAgentTrendChart = echarts.init(subAgentTrendChartEl); }
    return subAgentTrendChart;
  }

  function buildTrendChartUpdater({ ensureChart, emptyEl, summaryEl, summaryPrefix = '最近7天合计：' }) {
    return function update(payload = {}) {
      if (!echartsAvailable) return;
      const chart = ensureChart();
      if (!chart) return;
      const points = Array.isArray(payload?.points) ? payload.points : [];
      const categories = Array.isArray(payload?.categories) ? payload.categories : [];

      let labels = [];
      let values = [];

      if (Array.isArray(categories) && categories.length) {
        const normalized = categories
          .map(label => (typeof label === 'string' ? label.trim() : ''))
          .filter(label => label.length > 0);
        const pointMap = new Map();
        points.forEach(item => {
          const key = (item?.date || item?.Date || '').trim();
          if (!key) return;
          const value = Number(item?.count ?? item?.Count ?? 0);
          pointMap.set(key, Number.isFinite(value) ? value : 0);
        });
        labels = normalized;
        values = labels.map(label => {
          const value = pointMap.has(label) ? pointMap.get(label) : 0;
          return Number.isFinite(value) ? value : 0;
        });
      } else {
        labels = points
          .map(item => (item?.date || item?.Date || '').trim())
          .filter(label => label.length > 0);
        values = points.map(item => {
          const value = Number(item?.count ?? item?.Count ?? 0);
          return Number.isFinite(value) ? value : 0;
        });
      }

      const total = values.reduce((sum, val) => sum + (Number.isFinite(val) ? val : 0), 0);
      const hasData = labels.length && values.some(val => Number.isFinite(val) && val > 0);
      if (!hasData) {
        chart.clear();
        if (emptyEl) emptyEl.classList.remove('hidden');
        if (summaryEl) summaryEl.textContent = '';
        return;
      }
      if (emptyEl) emptyEl.classList.add('hidden');
      chart.setOption({
        grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
        tooltip: { trigger: 'axis' },
        xAxis: { type: 'category', boundaryGap: false, data: labels },
        yAxis: { type: 'value', minInterval: 1 },
        series: [{
          type: 'line',
          smooth: true,
          areaStyle: { opacity: 0.15 },
          symbol: 'circle',
          data: values
        }]
      });
      if (summaryEl) summaryEl.textContent = summaryPrefix + total + ' 张';
    };
  }

  const updateTotalTrendChart = buildTrendChartUpdater({
    ensureChart: ensureTrendChart,
    emptyEl: trendEmpty,
    summaryEl: trendSummary,
    summaryPrefix: '最近7天合计：'
  });

  function updateSubAgentTrendChart(payload = {}) {
    if (!echartsAvailable) return;
    const chart = ensureSubAgentTrendChart();
    if (!chart) return;

    const rawSeries = Array.isArray(payload?.series) ? payload.series : [];
    const aggregatePoints = Array.isArray(payload?.points) ? payload.points : [];
    const categoriesRaw = Array.isArray(payload?.categories) ? payload.categories : [];

    let categories = [];
    if (categoriesRaw.length) {
      categories = categoriesRaw
        .map(item => (typeof item === 'string' ? item.trim() : ''))
        .filter(item => item.length > 0);
    } else if (aggregatePoints.length) {
      categories = aggregatePoints
        .map(item => (item?.date || item?.Date || '').trim())
        .filter(item => item.length > 0);
    }

    const series = [];
    rawSeries.forEach(item => {
      const name = (item?.displayName || item?.agent || '').toString().trim() || '未命名代理';
      const points = Array.isArray(item?.points) ? item.points : [];
      const valueMap = new Map();
      points.forEach(point => {
        const key = (point?.date || point?.Date || '').trim();
        if (!key) return;
        const value = Number(point?.count ?? point?.Count ?? 0);
        valueMap.set(key, Number.isFinite(value) ? value : 0);
      });
      const values = categories.map(day => {
        const value = valueMap.has(day) ? valueMap.get(day) : 0;
        return Number.isFinite(value) ? value : 0;
      });
      series.push({
        name,
        type: 'line',
        smooth: true,
        symbol: 'circle',
        areaStyle: { opacity: 0.12 },
        data: values
      });
    });

    if (!series.length || !categories.length) {
      chart.clear();
      if (subAgentTrendEmpty) subAgentTrendEmpty.classList.remove('hidden');
      if (subAgentTrendSummary) subAgentTrendSummary.textContent = '';
      return;
    }

    if (subAgentTrendEmpty) subAgentTrendEmpty.classList.add('hidden');

    chart.setOption({
      grid: { left: '3%', right: '4%', bottom: '10%', containLabel: true },
      tooltip: { trigger: 'axis' },
      legend: { data: series.map(item => item.name), top: 0 },
      xAxis: { type: 'category', boundaryGap: false, data: categories },
      yAxis: { type: 'value', minInterval: 1 },
      series
    });

    const aggregateTotal = aggregatePoints.reduce((sum, point) => {
      const value = Number(point?.count ?? point?.Count ?? 0);
      return sum + (Number.isFinite(value) ? value : 0);
    }, 0);
    const summaryTotal = aggregatePoints.length ? aggregateTotal : series.reduce((sum, item) => {
      const subtotal = item.data.reduce((inner, value) => inner + (Number.isFinite(value) ? value : 0), 0);
      return sum + subtotal;
    }, 0);

    if (subAgentTrendSummary) {
      subAgentTrendSummary.textContent = `最近子代理7天合计：${summaryTotal} 张`;
    }
  }

  async function loadTrendData({ software, onlyDescendants = false, updater, toastOnError = false }) {
    if (!software) {
      updater({ points: [] });
      return;
    }
    if (!echartsAvailable) return;
    try {
      const resp = await api(`${API_BASE_URL}/Card/getRecentActivationTrend`, {
        method: 'POST',
        body: JSON.stringify({ software, OnlyDescendants: onlyDescendants })
      });
      const data = await resp.json();
      if (data?.code === 0) {
        updater(data.data || {});
      } else {
        updater({ points: [] });
        if (toastOnError) showToast(data?.message || '趋势获取失败', 'error');
      }
    } catch (err) {
      console.error('loadRecentTrend error', err);
      updater({ points: [] });
      if (toastOnError) showToast('趋势获取失败：' + err.message, 'error');
    }
  }

  async function loadRecentTrend(software) {
    if (!software) {
      updateTotalTrendChart({ points: [] });
      updateSubAgentTrendChart({ points: [] });
      return;
    }
    await Promise.all([
      loadTrendData({ software, updater: updateTotalTrendChart, toastOnError: true }),
      loadTrendData({ software, onlyDescendants: true, updater: updateSubAgentTrendChart, toastOnError: false })
    ]);
  }

  function ensureUsageChart() {
    if (!echartsAvailable || !usageMapEl) return null;
    if (!usageChart) { usageChart = echarts.init(usageMapEl); }
    return usageChart;
  }

  async function ensureChinaMap() {
    if (!echartsAvailable) return false;
    if (chinaMapLoaded) return true;
    const sources = [
      '/assets/maps/china-simple.json',
      'https://fastly.jsdelivr.net/npm/echarts@5/map/json/china.json',
      'https://geo.datav.aliyun.com/areas_v3/bound/geojson?code=100000_full'
    ];
    for (const src of sources) {
      try {
        const resp = await fetch(src);
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        const json = await resp.json();
        if (!json || !json.features) throw new Error('无效的地图数据');
        echarts.registerMap('china', json);
        chinaMapLoaded = true;
        return true;
      } catch (err) {
        console.warn('map source failed', src, err);
      }
    }
    if (usageMapEmpty) {
      usageMapEmpty.classList.remove('hidden');
      usageMapEmpty.textContent = '地图加载失败';
    }
    return false;
  }

  function renderUsageList(container, list, formatter) {
    if (!container) return;
    container.innerHTML = '';
    if (!Array.isArray(list) || !list.length) {
      container.innerHTML = '<div class="text-xs text-gray-500">暂无数据</div>';
      return;
    }
    list.slice(0, 10).forEach(item => {
      const div = document.createElement('div');
      div.className = 'flex items-center justify-between gap-2';
      const count = Number(item?.count ?? item?.Count ?? 0);
      const countText = Number.isFinite(count) && count >= 0 ? count : 0;
      div.innerHTML = '<span class="truncate">' + formatter(item) + '</span><span class="text-xs text-[var(--secondary)]">' + countText + '</span>';
      container.appendChild(div);
    });
  }

  async function loadSystemStatus({ silent = false } = {}) {
    if (!systemStatusWrapper) return;
    if (!currentUser?.isSuper) {
      resetSystemStatusDisplay();
      systemStatusWrapper.classList.add('hidden');
      systemStatusLoading = false;
      return;
    }

    if (systemStatusLoading) return;
    systemStatusLoading = true;

    systemStatusWrapper.classList.remove('hidden');
    if (systemStatusMessage) systemStatusMessage.classList.add('hidden');

    if (systemStatusRefreshBtn) {
      systemStatusRefreshBtn.disabled = true;
      systemStatusRefreshBtn.innerHTML = '<i class="fa fa-circle-notch fa-spin"></i> 刷新中';
    }

    try {
      const resp = await api(`${API_BASE_URL}/System/status`, { method: 'GET' });
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      const payload = await resp.json();
      if (!payload || payload.code !== 0) throw new Error((payload && payload.message) || '获取失败');
      const data = payload.data || {};

      const cpu = Number(data.cpuLoadPercentage);
      if (systemStatusCpu) {
        systemStatusCpu.textContent = Number.isFinite(cpu) ? `${cpu.toFixed(1).replace(/\.0$/, '')}%` : '--';
      }

      const totalMemory = Number(data.totalMemoryBytes);
      const rawUsed = Number(data.usedMemoryBytes);
      const rawFree = Number(data.freeMemoryBytes);
      let usedMemory = Number.isFinite(rawUsed) ? rawUsed : NaN;
      if (!Number.isFinite(usedMemory) && Number.isFinite(totalMemory) && Number.isFinite(rawFree)) {
        usedMemory = totalMemory - rawFree;
      }
      const memoryPercent = Number(data.memoryUsagePercentage);
      if (systemStatusMemory) {
        if (Number.isFinite(totalMemory) && totalMemory > 0 && Number.isFinite(usedMemory) && usedMemory >= 0) {
          const percentText = Number.isFinite(memoryPercent) ? ` (${memoryPercent.toFixed(1).replace(/\.0$/, '')}%)` : '';
          systemStatusMemory.textContent = `${formatBytes(usedMemory)} / ${formatBytes(totalMemory)}${percentText}`;
        } else {
          systemStatusMemory.textContent = '--';
        }
      }

      if (systemStatusBoot) systemStatusBoot.textContent = formatDateTime(data.bootTime);
      if (systemStatusTime) systemStatusTime.textContent = formatDateTime(data.serverTime);
      if (systemStatusUptime) systemStatusUptime.textContent = formatDuration(data.uptimeSeconds);
      if (systemStatusUpdated) systemStatusUpdated.textContent = '最近刷新：' + new Date().toLocaleString();

      if (systemStatusMessage) {
        const warnings = Array.isArray(data.warnings) ? data.warnings.filter(item => typeof item === 'string' && item.trim()) : [];
        if (warnings.length) {
          systemStatusMessage.textContent = warnings.join('；');
          systemStatusMessage.classList.remove('hidden');
        } else {
          systemStatusMessage.classList.add('hidden');
        }
      }
    } catch (err) {
      if (systemStatusMessage) {
        systemStatusMessage.textContent = '获取失败：' + err.message;
        systemStatusMessage.classList.remove('hidden');
      }
      if (!silent) {
        showToast('系统状态获取失败：' + err.message, 'error');
      }
    } finally {
      if (systemStatusRefreshBtn) {
        systemStatusRefreshBtn.disabled = false;
        systemStatusRefreshBtn.innerHTML = '<i class="fa fa-rotate"></i> 刷新';
      }
      systemStatusLoading = false;
    }
  }

  function updateUsageMap(data) {
    if (!echartsAvailable) return;
    if (usageMapEmpty) usageMapEmpty.textContent = '暂无数据或缺少位置信息';
    const chart = ensureUsageChart();
    if (!chart) return;
    const provinces = Array.isArray(data?.provinces) ? data.provinces : [];
    const provinceAggregate = new Map();
    provinces.forEach(item => {
      const mappedName = normalizeProvinceForMap(item.province || item.Province || '');
      const count = Number(item.count ?? item.Count ?? 0);
      if (!mappedName || !Number.isFinite(count) || count <= 0) return;
      const current = provinceAggregate.get(mappedName) || 0;
      provinceAggregate.set(mappedName, current + count);
    });
    const mapSeries = Array.from(provinceAggregate.entries()).map(([name, value]) => ({ name, value }));
    const hasData = mapSeries.some(item => Number.isFinite(item.value) && item.value > 0);
    if (!hasData) {
      chart.clear();
      if (usageMapEmpty) usageMapEmpty.classList.remove('hidden');
    } else {
      if (usageMapEmpty) usageMapEmpty.classList.add('hidden');
      const maxValue = Math.max(...mapSeries.map(item => Number(item.value) || 0), 1);
      chart.setOption({
        backgroundColor: 'transparent',
        tooltip: {
          trigger: 'item',
          borderWidth: 0,
          padding: [8, 12],
          backgroundColor: 'rgba(15, 23, 42, 0.85)',
          formatter: info => `${info.name || '未知'}<br/>卡密：${info.value || 0}`
        },
        visualMap: {
          min: 0,
          max: maxValue,
          orient: 'vertical',
          left: 'left',
          bottom: 10,
          text: ['高', '低'],
          textStyle: { color: '#94a3b8' },
          inRange: {
            color: ['#0f172a', '#1d4ed8', '#22d3ee']
          }
        },
        geo: {
          map: 'china',
          roam: true,
          zoom: 1.1,
          layoutCenter: ['50%', '55%'],
          layoutSize: '95%',
          scaleLimit: { min: 0.8, max: 8 },
          itemStyle: {
            areaColor: '#081226',
            borderColor: '#38bdf8',
            borderWidth: 0.8,
            shadowColor: 'rgba(56,189,248,0.25)',
            shadowBlur: 25
          },
          emphasis: {
            itemStyle: { areaColor: '#2563eb' },
            label: { show: true, color: '#fff' }
          }
        },
        series: [
          {
            name: '卡密分布',
            type: 'map',
            geoIndex: 0,
            data: mapSeries,
            roam: true,
            emphasis: { label: { show: true, color: '#fff' } }
          }
        ]
      }, true);
      setTimeout(() => {
        try { chart.resize(); } catch (err) { console.warn('map resize error', err); }
      }, 0);
    }
    renderUsageList(usageProvinceList, provinces, item => item.province || '未知');
    renderUsageList(usageCityList, Array.isArray(data?.cities) ? data.cities : [], item => ((item.province || '') + (item.city ? ' - ' + item.city : '')).trim() || '未知');
    renderUsageList(usageDistrictList, Array.isArray(data?.districts) ? data.districts : [], item => {
      const parts = [item.province, item.city, item.district].filter(Boolean);
      return parts.length ? parts.join(' - ') : '未知';
    });
    if (usageMapSummary) {
      let total = Number(data?.resolvedTotal);
      if (!Number.isFinite(total) || total <= 0) {
        total = provinces.reduce((sum, item) => sum + (Number(item.count) || 0), 0);
      }
      usageMapSummary.textContent = '统计卡密：' + total + ' 张';
    }
  }

  async function loadUsageDistribution() {
    if (!currentSoftware || !currentSoftware.softwareName) {
      if (usageMapEmpty) usageMapEmpty.classList.remove('hidden');
      return;
    }
    if (!(await ensureChinaMap())) {
      return;
    }
    try {
      const resp = await api(`${API_BASE_URL}/Card/getUsageDistribution`, { method:'POST', body: JSON.stringify({ software: currentSoftware.softwareName, includeDescendants: true }) });
      const data = await resp.json();
      if (data?.code === 0) {
        lastUsageData = data.data || {};
        updateUsageMap(lastUsageData);
      } else {
        showToast(data?.message || '地区分布获取失败', 'error');
      }
    } catch (err) {
      console.error('loadUsageDistribution error', err);
      showToast('地区分布获取失败：' + err.message, 'error');
    }
  }

  // ===== 登录 & 软件 =====
  const loginPage = document.getElementById('login-page');
  const loginForm = document.getElementById('login-form');
  const passwordInput = document.getElementById('password-input');
  const togglePassword = document.getElementById('toggle-password');

  const systemStatusWrapper = document.getElementById('system-status-wrapper');
  const systemStatusCpu = document.getElementById('system-status-cpu');
  const systemStatusMemory = document.getElementById('system-status-memory');
  const systemStatusBoot = document.getElementById('system-status-boot');
  const systemStatusUptime = document.getElementById('system-status-uptime');
  const systemStatusTime = document.getElementById('system-status-time');
  const systemStatusUpdated = document.getElementById('system-status-updated');
  const systemStatusMessage = document.getElementById('system-status-message');
  const systemStatusRefreshBtn = document.getElementById('system-status-refresh');

  const softwarePage = document.getElementById('software-page');
  const softwareList = document.getElementById('software-list');
  const softwareLoading = document.getElementById('software-loading');
  const logoutBtn = document.getElementById('logout-btn');
  chatBtn = document.getElementById('chat-btn');
  chatUnreadCountEl = document.getElementById('chat-unread-count');
  linkRecordsBtn = document.getElementById('link-records-btn');

  resetSystemStatusDisplay();

  const pendingLoginMessage = popLoginMessage();
  if (pendingLoginMessage && pageType === 'login') {
    showToast(pendingLoginMessage.message, pendingLoginMessage.type);
  }

  const modalGenerated = document.getElementById('modal-generated');
  const btnCloseGenerated = document.getElementById('btn-close-generated');
  const btnCopyGenerated = document.getElementById('btn-copy-cards');
  const btnExportGenerated = document.getElementById('btn-export-cards');
  const generatedTitle = document.getElementById('generated-cards-title');
  const generatedList = document.getElementById('generated-cards-list');
  const generatedCount = document.getElementById('generated-cards-count');

  if (btnCopyGenerated) btnCopyGenerated.disabled = true;
  if (btnExportGenerated) btnExportGenerated.disabled = true;

  const salesSoftwareSelect = document.getElementById('sales-software');
  const salesCardTypeSelect = document.getElementById('sales-cardType');
  const salesStatusSelect = document.getElementById('sales-status');
  const salesAgentSelect = document.getElementById('sales-agent');
  const salesIncludeSub = document.getElementById('sales-include-sub');
  const salesStartInput = document.getElementById('sales-start');
  const salesEndInput = document.getElementById('sales-end');
  const salesQueryBtn = document.getElementById('sales-query');
  const salesResetBtn = document.getElementById('sales-reset');
  const salesLoading = document.getElementById('sales-loading');
  const salesError = document.getElementById('sales-error');
  const salesResultWrap = document.getElementById('sales-result');
  const salesCount = document.getElementById('sales-count');
  const salesSummary = document.getElementById('sales-summary');
  const salesResultBody = document.getElementById('sales-result-body');
  const managementPage = document.getElementById('management-page');
  const backToSoftwareBtn = document.getElementById('back-to-software');
  const managementLogoutBtn = document.getElementById('management-logout');
  managementChatBtn = document.getElementById('management-chat-btn');
  managementChatUnreadEl = document.getElementById('management-chat-unread');
  managementLinkRecordsBtn = document.getElementById('management-link-records');
  const currentSoftwareName = document.getElementById('current-software-name');

  updateChatButtons();

  togglePassword.addEventListener('click', () => {
    const t = passwordInput.getAttribute('type') === 'password' ? 'text' : 'password';
    passwordInput.setAttribute('type', t);
    togglePassword.innerHTML = t === 'password' ? '<i class="fa fa-eye"></i>' : '<i class="fa fa-eye-slash"></i>';
  });
  if (systemStatusRefreshBtn) {
    systemStatusRefreshBtn.addEventListener('click', () => loadSystemStatus());
  }
  async function afterLogin() {
    if (loginPage) {
      loginPage.classList.add('hidden');
    }
    updateLinkRecordsButtons();
    updateChatButtons();

    if (pageType === 'login') {
      window.location.href = './dashboard.html';
      return;
    }

    startChatUnreadPolling();

    if (pageType === 'dashboard') {
      if (managementPage) managementPage.classList.add('hidden');
      if (softwarePage) softwarePage.classList.remove('hidden');
      if (systemStatusWrapper) systemStatusWrapper.classList.remove('hidden');
      await Promise.all([
        loadSystemStatus({ silent: true }),
        fetchSoftwareList(),
        loadAnnouncement({ silent: true })
      ]);
    } else if (pageType === 'management') {
      if (softwarePage) softwarePage.classList.add('hidden');
      if (managementPage) managementPage.classList.remove('hidden');
      await fetchSoftwareList();
      const params = new URLSearchParams(window.location.search);
      const desiredName = (params.get('software') || '').trim().toLowerCase();
      let targetSoftware = restoreSelectedSoftware();
      if (desiredName) {
        const matched = softwareOptions.find(item => {
          const name = (item?.softwareName || item?.name || item?.id || '').toString().trim().toLowerCase();
          return name === desiredName;
        });
        if (matched) {
          targetSoftware = matched;
        }
      }
      if (!targetSoftware && softwareOptions.length) {
        targetSoftware = softwareOptions[0];
      }
      if (targetSoftware) {
        await selectSoftware(targetSoftware, { skipNavigation: true });
      } else {
        showToast('未找到可管理的软件，请返回软件列表', 'error');
        window.location.href = './dashboard.html';
      }
    } else {
      if (softwarePage) softwarePage.classList.remove('hidden');
      await Promise.all([
        loadSystemStatus({ silent: true }),
        fetchSoftwareList(),
        loadAnnouncement({ silent: true })
      ]);
    }
  }


  if (loginForm) {
    loginForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const username = loginForm.querySelector('input[name="username"]').value.trim();
      const password = loginForm.querySelector('input[name="password"]').value;
      const btn = loginForm.querySelector('button[type="submit"]');
      const original = btn.innerHTML;
      btn.disabled = true; btn.innerHTML = '<i class="fa fa-circle-notch fa-spin mr-2"></i> 登录中...';
      try {
        const resp = await api(`${API_BASE_URL}/Auth/login`, { method:'POST', body: JSON.stringify({ username, password }) });
        if (!resp.ok) throw new Error('HTTP ' + resp.status);
        const data = await resp.json();
        if (data && data.code === 0) {
          const payload = data.data || {};
          if (payload.token) {
            setAuthToken(payload.token, payload.expiresAt || payload.tokenExpiresAt);
          } else {
            clearAuthToken();
          }
          currentUser = { username: payload.username || username, isSuper: !!payload.isSuper };
          updateLinkRecordsButtons();
          showToast('登录成功');
          await afterLogin();
        } else { showToast((data && data.message) || '登录失败', 'error'); }
      } catch (err) { console.error('login error', err); showToast('登录失败：' + err.message, 'error'); }
      finally { btn.disabled = false; btn.innerHTML = original; }
    });
    if (pageType === 'login') {
      (function autologin(){
        const p = new URLSearchParams(location.search);
        const u = p.get('username'); const pw = p.get('password');
        if (u) loginForm.querySelector('input[name="username"]').value = decodeURIComponent(u);
        if (pw) loginForm.querySelector('input[name="password"]').value = decodeURIComponent(pw);
        if (u && pw) setTimeout(() => loginForm.dispatchEvent(new Event('submit', { cancelable:true, bubbles:true })), 0);
      })();
    }
  }

  function performClientLogout({ message, type = 'error', silent = false } = {}) {
    const finalMessage = message || '登录状态已过期，请重新登录';
    clearAuthToken();
    stopChatUnreadPolling();
    if (modalGenerated) { modalGenerated.style.display = 'none'; }
    if (generatedList) generatedList.innerHTML = '';
    if (generatedCount) generatedCount.textContent = '';
    if (generatedTitle) generatedTitle.textContent = '';
    if (btnCopyGenerated) btnCopyGenerated.disabled = true;
    if (btnExportGenerated) btnExportGenerated.disabled = true;
    lastGeneratedCards = [];
    lastGeneratedCardType = '';
    if (echartsAvailable && trendChart) { try { trendChart.clear(); } catch (err) { console.warn(err); } }
    if (echartsAvailable && subAgentTrendChart) { try { subAgentTrendChart.clear(); } catch (err) { console.warn(err); } }
    if (trendEmpty) trendEmpty.classList.remove('hidden');
    if (trendSummary) trendSummary.textContent = '';
    if (subAgentTrendEmpty) subAgentTrendEmpty.classList.remove('hidden');
    if (subAgentTrendSummary) subAgentTrendSummary.textContent = '';
    if (echartsAvailable && usageChart) { try { usageChart.clear(); } catch (err) { console.warn(err); } }
    if (usageMapEmpty) usageMapEmpty.classList.add('hidden');
    if (usageMapSummary) usageMapSummary.textContent = '';
    renderUsageList(usageProvinceList, [], () => '');
    renderUsageList(usageCityList, [], () => '');
    renderUsageList(usageDistrictList, [], () => '');
    lastUsageData = null;
    updateAnnouncementCard({});
    resetSystemStatusDisplay();
    if (systemStatusWrapper) systemStatusWrapper.classList.add('hidden');
    currentUser = null;
    currentSoftware = null;
    cardTypeCache.clear();
    agentOptionsCache.clear();
    softwareOptions = [];
    clearSelectedSoftware();
    if (!silent && finalMessage) {
      pushLoginMessage(finalMessage, type);
    }
    if (pageType === 'login') {
      loginPage.classList.remove('hidden');
      if (!silent && finalMessage) {
        showToast(finalMessage, type);
      }
    } else {
      window.location.href = './login.html';
    }
    updateLinkRecordsButtons();
    updateChatButtons();
  }
  async function logout(){
    try {
      await api(`${API_BASE_URL}/Auth/logout`, { method:'POST' });
    } catch (err) {
      console.error('logout error', err);
    } finally {
      performClientLogout({ message: '已退出登录', type: 'info' });
    }
  }
  if (logoutBtn) logoutBtn.addEventListener('click', logout);
  if (managementLogoutBtn) managementLogoutBtn.addEventListener('click', logout);

  if (linkRecordsBtn) linkRecordsBtn.addEventListener('click', openLinkRecordsPage);
  if (managementLinkRecordsBtn) managementLinkRecordsBtn.addEventListener('click', openLinkRecordsPage);
  if (chatBtn) chatBtn.addEventListener('click', openChatPage);
  if (managementChatBtn) managementChatBtn.addEventListener('click', openChatPage);

  async function fetchSoftwareList(){
    softwareList.innerHTML = ''; softwareLoading.classList.remove('hidden');
    try {
      const resp = await api(`${API_BASE_URL}/Software/GetSoftwareList`, { method:'POST', body: JSON.stringify({}) });
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      const data = await resp.json();
      const list = data?.data?.softwares || data?.data?.list || [];
      softwareOptions = list;
      populateSalesSoftwareOptions(list);
      setSalesDefaultRange();
      const selectedSoftware = salesSoftwareSelect.value;
      if (selectedSoftware) {
        await refreshSalesCardTypes(selectedSoftware);
        await refreshSalesAgents(selectedSoftware);
        await loadRecentTrend(selectedSoftware);
      } else {
        salesResultWrap.classList.add('hidden');
        updateTotalTrendChart({ points: [] });
        updateSubAgentTrendChart({ points: [] });
      }
      if (!list.length) { softwareList.innerHTML = '<div class="text-gray-400">暂无软件</div>'; return; }
      list.forEach(soft => {
        const isActive = soft.state === 1 || soft.state === '1' || soft.state === '启用';
        const statusText = isActive ? '启用' : '禁用';
        const card = document.createElement('div');
        card.className = 'glass rounded-xl p-5 cursor-pointer hover:opacity-90';
        card.innerHTML = `<div class="flex items-center justify-between mb-2"><div class="font-semibold text-white">${soft.softwareName}</div><span class="text-xs ${isActive?'text-green-400':'text-gray-400'}">${statusText}</span></div><div class="text-sm text-gray-400">IDC：${soft.idc || '-'}</div>`;
        card.addEventListener('click', () => selectSoftware(soft));
        softwareList.appendChild(card);
      });
    } catch (e) { console.error(e); showToast('获取软件失败', 'error'); }
    finally { softwareLoading.classList.add('hidden'); }
  }

  salesSoftwareSelect.addEventListener('change', async () => {
    const software = salesSoftwareSelect.value;
    await refreshSalesCardTypes(software);
    await refreshSalesAgents(software);
    await loadRecentTrend(software);
    salesError.classList.add('hidden');
    salesResultWrap.classList.add('hidden');
  });
  salesQueryBtn.addEventListener('click', (e) => { e.preventDefault(); runSalesQuery(); });
  salesResetBtn.addEventListener('click', async (e) => {
    e.preventDefault();
    if (softwareOptions.length) {
      const first = softwareOptions.find(item => (item?.softwareName || item?.name || item?.id || item?.software));
      if (first) {
        const name = first.softwareName || first.name || first.id || first.software;
        salesSoftwareSelect.value = name || '';
      }
    }
    salesStatusSelect.value = '';
    salesIncludeSub.checked = true;
    setSalesDefaultRange();
    await refreshSalesCardTypes(salesSoftwareSelect.value);
    await refreshSalesAgents(salesSoftwareSelect.value);
    await loadRecentTrend(salesSoftwareSelect.value);
    salesError.classList.add('hidden');
    salesResultWrap.classList.add('hidden');
  });

  async function selectSoftware(soft, { skipNavigation = false } = {}){
    if (!soft) return;
    storeSelectedSoftware(soft);
    if (pageType !== 'management' && !skipNavigation) {
      const name = (soft.softwareName || soft.name || soft.id || '').toString().trim();
      const query = name ? `?software=${encodeURIComponent(name)}` : '';
      window.location.href = './management.html' + query;
      return;
    }
    currentSoftware = soft;
    currentSoftwareName.textContent = soft.softwareName || soft.name || '-';
    if (pageType !== 'management') {
      if (softwarePage) softwarePage.classList.add('hidden');
      if (managementPage) managementPage.classList.remove('hidden');
    }
    agentLoadedOnce = false;
    blacklistLogsLoaded = false;
    blacklistMachinesLoaded = false;
    blacklistLogItems = [];
    agentPage = 1;
    agentSelected.clear();
    agentCheckAll.checked = false;
    currentCardPage = 1;
    filter = { whom: '', cardType: '', status: '', searchType: 0, keyword: '' };
    if (filterWhom) filterWhom.value = '';
    if (filterCardType) filterCardType.value = '';
    if (filterStatus) filterStatus.value = '';
    if (filterSearchType) filterSearchType.value = '0';
    if (filterKeyword) filterKeyword.value = '';
    if (blacklistLogSearchInput) blacklistLogSearchInput.value = '';
    await refreshAgentOptionsForCurrentSoftware();
    await fetchCardTypes();
    showManagementTab('card');
  }

  backToSoftwareBtn.addEventListener('click', () => { window.location.href = './dashboard.html'; });

  // ===== 代理管理 =====
  const cardTab = document.getElementById('card-tab');
  const agentTab = document.getElementById('agent-tab');
  const blacklistLogTab = document.getElementById('blacklist-log-tab');
  const blacklistMachineTab = document.getElementById('blacklist-machine-tab');
  const cardSection = document.getElementById('card-section');
  const agentSection = document.getElementById('agent-section');
  const blacklistLogSection = document.getElementById('blacklist-log-section');
  const blacklistMachineSection = document.getElementById('blacklist-machine-section');
  const usageMapEl = document.getElementById('usage-map');
  const usageMapEmpty = document.getElementById('usage-map-empty');
  const usageMapSummary = document.getElementById('usage-map-summary');
  const usageProvinceList = document.getElementById('usage-province-list');
  const usageCityList = document.getElementById('usage-city-list');
  const usageDistrictList = document.getElementById('usage-district-list');
  const usageRefreshBtn = document.getElementById('usage-refresh');
  const blacklistLogList = document.getElementById('blacklist-log-list');
  const blacklistMachineList = document.getElementById('blacklist-machine-list');
  const blacklistLogSearchInput = document.getElementById('blacklist-log-search');
  const blacklistLogSearchBtn = document.getElementById('blacklist-log-search-btn');
  const blacklistLogResetBtn = document.getElementById('blacklist-log-reset-btn');
  const blacklistMachineForm = document.getElementById('blacklist-machine-form');
  const blacklistMachineValue = document.getElementById('blacklist-machine-value');
  const blacklistMachineType = document.getElementById('blacklist-machine-type');
  const blacklistMachineRemark = document.getElementById('blacklist-machine-remark');
  const blacklistMachineCheckAll = document.getElementById('blacklist-machine-check-all');
  const blacklistMachineDeleteSelected = document.getElementById('blacklist-machine-delete-selected');
  const blacklistMachineRefreshBtn = document.getElementById('blacklist-machine-refresh');

  let agentLoadedOnce = false;
  let blacklistLogsLoaded = false;
  let blacklistMachinesLoaded = false;
  let blacklistLogItems = [];
  const blacklistMachineSelected = new Set();

  function showManagementTab(key) {
    const configs = {
      card: {
        tab: cardTab,
        section: cardSection,
        onShow: () => {
          fetchCardTypes();
          fetchCardList();
          loadUsageDistribution();
        }
      },
      agent: {
        tab: agentTab,
        section: agentSection,
        onShow: () => {
          fetchAgentList(agentLoadedOnce ? false : true);
          agentLoadedOnce = true;
        }
      },
      log: {
        tab: blacklistLogTab,
        section: blacklistLogSection,
        onShow: () => {
          fetchBlacklistLogs();
          blacklistLogsLoaded = true;
        }
      },
      machine: {
        tab: blacklistMachineTab,
        section: blacklistMachineSection,
        onShow: () => {
          fetchBlacklistMachines();
          blacklistMachinesLoaded = true;
        }
      }
    };

    Object.entries(configs).forEach(([name, cfg]) => {
      if (!cfg.tab || !cfg.section) return;
      if (name === key) {
        cfg.tab.classList.add('tab-active');
        cfg.section.classList.remove('hidden');
      } else {
        cfg.tab.classList.remove('tab-active');
        cfg.section.classList.add('hidden');
      }
    });

    const target = configs[key];
    if (target?.onShow) {
      target.onShow();
    }
  }

  if (cardTab) cardTab.addEventListener('click', () => showManagementTab('card'));
  if (agentTab) agentTab.addEventListener('click', () => showManagementTab('agent'));
  if (blacklistLogTab) blacklistLogTab.addEventListener('click', () => showManagementTab('log'));
  if (blacklistMachineTab) blacklistMachineTab.addEventListener('click', () => showManagementTab('machine'));

  if (usageRefreshBtn) { usageRefreshBtn.addEventListener('click', () => loadUsageDistribution()); }

  const agentList = document.getElementById('agent-list');
  const agentSearchKeyword = document.getElementById('agent-search-keyword');
  const agentSearchBtn = document.getElementById('agent-search-btn');
  const agentEnableSelected = document.getElementById('agent-enable-selected');
  const agentDisableSelected = document.getElementById('agent-disable-selected');
  const agentDeleteSelected = document.getElementById('agent-delete-selected');
  const agentCheckAll = document.getElementById('agent-check-all');
  const agentPrev = document.getElementById('agent-prev');
  const agentNext = document.getElementById('agent-next');
  const agentPages = document.getElementById('agent-pages');
  const agentStart = document.getElementById('agent-start');
  const agentEnd = document.getElementById('agent-end');
  const agentTotal = document.getElementById('agent-total');

  const cardList = document.getElementById('card-list');
  const cardLoading = document.getElementById('card-loading');
  const cardPages = document.getElementById('card-pages');
  const cardPrevPage = document.getElementById('card-prev');
  const cardNextPage = document.getElementById('card-next');
  const cardStartItem = document.getElementById('card-start-item');
  const cardEndItem = document.getElementById('card-end-item');
  const cardTotalItems = document.getElementById('card-total-items');

  let agentPage = 1, agentLimit = 15, agentTotalPages = 1, agentSelected = new Set();

  agentCheckAll.addEventListener('change', () => {
    const checked = agentCheckAll.checked;
    document.querySelectorAll('.agent-row input[type="checkbox"]').forEach(cb => { cb.checked = checked; if (checked) agentSelected.add(cb.dataset.username); else agentSelected.clear(); });
  });

  function renderAgentRow(a){
    const username = a.username || a.name || a.account || a.user || '-';
    const balance = a.balance != null ? a.balance : (a.money != null ? a.money : 0);
    const timeStock = a.timeStock != null ? a.timeStock : (a.stockHours != null ? a.stockHours : 0);
    const parities = a.parities != null ? a.parities : 0;
    const totalParities = a.totalParities != null ? a.totalParities : 0;
    const expire = a.expireTime_ ? fmtFullDateTime(a.expireTime_) : (a.expireTime ? a.expireTime : '-');
    const remark = a.remark || a.remarks || '';
    const password = (a.password ?? a.Password ?? '').toString();

    const statusValue = (a.status ?? a.stat ?? a.Stat ?? a.cardsEnable ?? a.cards_enable ?? a.enabled ?? a.state ?? a.stateText);
    let isEnabled = true;
    if (statusValue != null) {
      if (typeof statusValue === 'boolean') {
        isEnabled = statusValue;
      } else if (typeof statusValue === 'number') {
        const numeric = Number(statusValue);
        if (!Number.isNaN(numeric)) {
          if (a.cardsEnable != null || a.cards_enable != null) {
            isEnabled = numeric !== 0;
          } else {
            isEnabled = numeric === 0;
          }
        }
      } else if (typeof statusValue === 'string') {
        const normalized = statusValue.trim();
        if (normalized) {
          if (normalized === '禁用' || normalized === '停用' || normalized === 'false' || normalized === '1') {
            isEnabled = false;
          } else if (normalized === '启用' || normalized === 'true' || normalized === '0') {
            isEnabled = true;
          }
        }
      }
    }

    const statusHtml = () => (isEnabled ? '<span class="tag tag-green">启用</span>' : '<span class="tag tag-gray">禁用</span>');

    const tr = document.createElement('tr');
    tr.className = 'table-row agent-row border-b border-white/10';
    tr.innerHTML = `
      <td class="px-4 py-3"><input type="checkbox" data-username="${username}"></td>
      <td class="px-4 py-3"><div class="agent-actions"></div></td>
      <td class="px-4 py-3">
        <div class="flex flex-col gap-1">
          <span class="font-medium">${username}</span>
          <span class="agent-status-inline text-sm">${statusHtml()}</span>
        </div>
      </td>
      <td class="px-4 py-3 desktop-only"><span class="agent-password-text">${password ? password : '-'}</span></td>
      <td class="px-4 py-3 agent-status-cell agent-status-col desktop-only">${statusHtml()}</td>
      <td class="px-4 py-3">${balance}</td>
      <td class="px-4 py-3">${timeStock}</td>
      <td class="px-4 py-3">${parities}</td>
      <td class="px-4 py-3">${totalParities}</td>
      <td class="px-4 py-3">${expire}</td>
      <td class="px-4 py-3">${remark || '-'}</td>
    `;

    const checkboxEl = tr.querySelector('input[type="checkbox"]');
    checkboxEl.addEventListener('change', (e) => {
      if (e.target.checked) {
        agentSelected.add(username);
      } else {
        agentSelected.delete(username);
      }
    });

    const actions = [];
    actions.push({ label: '加款', className: 'px-2 py-1 glass rounded text-sm', onClick: () => openAgentModal('addMoney', username), closeOnClick: false });
    actions.push({ label: '卡类型', className: 'px-2 py-1 glass rounded text-sm', onClick: () => openAgentModal('cardType', username), closeOnClick: false });
    actions.push({ label: '备注', className: 'px-2 py-1 glass rounded text-sm', onClick: () => openAgentModal('remark', username, remark), closeOnClick: false });
    actions.push({ label: '修改密码', className: 'px-2 py-1 glass rounded text-sm', onClick: () => openAgentModal('password', username, password), closeOnClick: false });
    const toggleAction = {
      label: isEnabled ? '禁用' : '启用',
      className: 'px-2 py-1 rounded text-sm text-white hover:opacity-90 agent-toggle-btn',
      attributes: { 'data-role': 'agent-toggle' },
      closeOnClick: false,
      onClick: () => {
        const targetEnabled = !isEnabled;
        return toggleAgentStatus(username, targetEnabled, (nowEnabled) => {
          applyToggleState(nowEnabled);
        });
      }
    };
    actions.push(toggleAction);

    const actionsContainer = tr.querySelector('.agent-actions');
    renderActionButtons(actionsContainer, actions);

    const statusCell = tr.querySelector('.agent-status-cell');
    const statusInline = tr.querySelector('.agent-status-inline');
    const toggleBtn = actionsContainer.querySelector('[data-role="agent-toggle"]');
    function applyToggleState(nowEnabled) {
      isEnabled = nowEnabled;
      const html = statusHtml();
      if (statusCell) statusCell.innerHTML = html;
      if (statusInline) statusInline.innerHTML = html;
      toggleAction.label = nowEnabled ? '禁用' : '启用';
      if (toggleBtn) {
        toggleBtn.textContent = toggleAction.label;
        toggleBtn.className = 'px-2 py-1 rounded text-sm text-white hover:opacity-90 agent-toggle-btn';
        if (nowEnabled) {
          toggleBtn.classList.add('btn-danger');
        } else {
          toggleBtn.classList.add('bg-blue-600');
        }
      }
    }
    applyToggleState(isEnabled);

    tr._mobileActions = actions;
    tr.dataset.mobileTitle = username;
    return tr;
  }

  function renderAgentPagination(total, page, limit){
    const totalPages = Math.max(1, Math.ceil(total/limit));
    agentTotalPages = totalPages;
    agentPages.innerHTML = '';
    const add = (p) => { const b=document.createElement('button'); b.className='pagination-item '+(p===page?'active':''); b.textContent=p; b.addEventListener('click',()=>{ agentPage=p; fetchAgentList(); }); agentPages.appendChild(b); };
    const span = 2; let s = Math.max(1, page-span), e = Math.min(totalPages, page+span);
    if (s>1) add(1); if (s>2){ const t=document.createElement('span'); t.className='px-2 text-gray-500'; t.textContent='...'; agentPages.appendChild(t); }
    for (let p=s; p<=e; p++) add(p);
    if (e<totalPages-1){ const t=document.createElement('span'); t.className='px-2 text-gray-500'; t.textContent='...'; agentPages.appendChild(t); }
    if (e<totalPages) add(totalPages);
  }

  async function fetchAgentList(reset=false){
    if (!currentSoftware) return;
    if (reset) { agentPage = 1; agentSelected.clear(); agentCheckAll.checked = false; }
    agentList.innerHTML = '<tr><td colspan="11" class="px-4 py-6 text-center text-gray-400">加载中...</td></tr>';
    try {
      const payload = { software: currentSoftware.softwareName, searchType: 1, keyword: (agentSearchKeyword.value || '').trim(), page: agentPage, limit: agentLimit };
      const resp = await api(`${API_BASE_URL}/Agent/getSubAgentList`, { method:'POST', body: JSON.stringify(payload) });
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      const j = await resp.json();
      const inner = j?.data || {};
      const list = inner.items || inner.list || inner.agents || inner.data || [];
      const total = Number(inner.total ?? inner.count ?? list.length ?? 0);
      if (!list.length) {
        agentList.innerHTML = '<tr><td colspan="11" class="px-4 py-6 text-center text-gray-400">暂无数据</td></tr>';
      } else {
        agentList.innerHTML = '';
        list.forEach(a => agentList.appendChild(renderAgentRow(a)));
      }
      agentStart.textContent = total ? ((agentPage-1)*agentLimit + 1) : 0;
      agentEnd.textContent = total ? Math.min(agentPage*agentLimit, total) : 0;
      agentTotal.textContent = total;
      renderAgentPagination(total, agentPage, agentLimit);
    } catch (e) {
      console.error(e); agentList.innerHTML = '<tr><td colspan="11" class="px-4 py-6 text-center text-red-400">获取失败</td></tr>'; showToast('获取子代理失败', 'error');
    }
  }
  agentSearchBtn.addEventListener('click', () => fetchAgentList(true));
  agentPrev.addEventListener('click', () => { if (agentPage>1){ agentPage--; fetchAgentList(); } });
  agentNext.addEventListener('click', () => { if (agentPage<agentTotalPages){ agentPage++; fetchAgentList(); } });


  async function toggleAgentStatus(username, enable, onSuccess){
    const path = enable ? 'enableAgent' : 'disableAgent';
    const payload = { software: currentSoftware.softwareName, username: [username] };
    try {
      const r = await api(`${API_BASE_URL}/Agent/${path}`, { method:'POST', body: JSON.stringify(payload) });
      if (!r.ok) throw new Error('HTTP ' + r.status);

      const j = await r.json();
      if (j.code === 0) {
        showToast(enable?'已启用':'已禁用');
        if (typeof onSuccess === 'function') onSuccess(enable);
        fetchAgentList();
      } else { showToast(j.message||'操作失败','error'); }
    } catch(e){ console.error(e); showToast('操作失败','error'); }
  }

  // 批量操作
  async function batch(path){
    if (agentSelected.size === 0) return showToast('请至少选择一个代理', 'error');
    const payload = { software: currentSoftware.softwareName, username: Array.from(agentSelected) };
    try {
      const r = await api(`${API_BASE_URL}/Agent/${path}`, { method:'POST', body: JSON.stringify(payload) });
      if (!r.ok) throw new Error('HTTP ' + r.status);

      const j = await r.json();
      if (j.code === 0){
        showToast('操作成功');
        if (path === 'deleteSubAgent') {
          invalidateAgentCache(currentSoftware.softwareName);
          await refreshAgentOptionsForCurrentSoftware();
          if (salesSoftwareSelect.value === currentSoftware.softwareName) {
            await refreshSalesAgents(currentSoftware.softwareName);
          }
        }
        fetchAgentList();
      } else { showToast(j.message||'操作失败','error'); }
    } catch(e){ console.error(e); showToast('操作失败','error'); }
  }
  agentEnableSelected.addEventListener('click', () => batch('enableAgent'));
  agentDisableSelected.addEventListener('click', () => batch('disableAgent'));
  agentDeleteSelected.addEventListener('click', () => batch('deleteSubAgent'));

  function formatBlacklistType(value) {
    const numeric = Number(value);
    if (Number.isFinite(numeric)) {
      if (numeric === 2) return '机器码';
      if (numeric === 1) return 'IP';
      if (numeric === 0) return '其他';
    }
    return String(value ?? '-');
  }

  function refreshMobileTableFor(element) {
    if (!element) return;
    const table = element.closest('table');
    const helper = window.MobileFriendly;
    if (table && helper && typeof helper.refreshTable === 'function') {
      helper.refreshTable(table);
    }
  }

  function normalizeSoftwareLabel(value) {
    const text = (value == null ? '' : String(value)).trim();
    return text || '默认软件';
  }

  function getCurrentSoftwareLabel() {
    if (!currentSoftware) return '';
    const name = currentSoftware.softwareName || currentSoftware.name || currentSoftware.id || '';
    return normalizeSoftwareLabel(name);
  }

  function matchesCurrentSoftware(value) {
    if (!currentSoftware) return false;
    return normalizeSoftwareLabel(value).toLowerCase() === getCurrentSoftwareLabel().toLowerCase();
  }

  function updateBlacklistMachineActions() {
    if (blacklistMachineDeleteSelected) {
      const disabled = blacklistMachineSelected.size === 0;
      blacklistMachineDeleteSelected.disabled = disabled;
      if (disabled) blacklistMachineDeleteSelected.classList.add('opacity-50');
      else blacklistMachineDeleteSelected.classList.remove('opacity-50');
    }

    if (!blacklistMachineCheckAll) return;
    const checkboxes = blacklistMachineList?.querySelectorAll('input[type="checkbox"][data-value]');
    if (!checkboxes || checkboxes.length === 0) {
      blacklistMachineCheckAll.checked = false;
      blacklistMachineCheckAll.indeterminate = false;
      return;
    }

    let checkedCount = 0;
    checkboxes.forEach(cb => { if (cb.checked) checkedCount++; });
    if (checkedCount === 0) {
      blacklistMachineCheckAll.checked = false;
      blacklistMachineCheckAll.indeterminate = false;
    } else if (checkedCount === checkboxes.length) {
      blacklistMachineCheckAll.checked = true;
      blacklistMachineCheckAll.indeterminate = false;
    } else {
      blacklistMachineCheckAll.checked = false;
      blacklistMachineCheckAll.indeterminate = true;
    }
  }

  function renderBlacklistLogRows(items, { emptyMessage = '暂无记录', emptyClass = 'text-gray-400' } = {}) {
    if (!blacklistLogList) return;
    if (!Array.isArray(items) || items.length === 0) {
      blacklistLogList.innerHTML = `<tr><td colspan="6" class="px-4 py-6 text-center ${emptyClass}">${emptyMessage}</td></tr>`;
      refreshMobileTableFor(blacklistLogList);
      return;
    }
    blacklistLogList.innerHTML = '';
    items.forEach(item => {
      const tr = document.createElement('tr');
      tr.className = 'table-row border-b border-white/10';
      const time = fmtFullDateTime(item.timestamp ?? item.Timestamp);
      const displaySoftware = normalizeSoftwareLabel(item.software ?? item.Software);
      const ip = item.ip || item.IP || '-';
      const cardValue = item.card || item.Card || '-';
      const machine = item.pcsign || item.PCSign || '-';
      const eventText = item.errEvents || item.ErrEvents || '-';
      tr.innerHTML = `
        <td class="px-4 py-3 text-sm">${time}</td>
        <td class="px-4 py-3 text-sm">${displaySoftware}</td>
        <td class="px-4 py-3 text-sm">${ip}</td>
        <td class="px-4 py-3 text-sm break-all">${cardValue}</td>
        <td class="px-4 py-3 text-sm break-all">${machine}</td>
        <td class="px-4 py-3 text-sm">${eventText}</td>
      `;
      tr.dataset.mobileTitle = cardValue || displaySoftware || '黑名单记录';
      tr._mobileDetailItems = [
        { label: '软件位', content: displaySoftware },
        { label: 'IP', content: ip || '-' },
        { label: '机器码', content: machine || '-' },
        { label: '事件', content: eventText || '-' }
      ];
      blacklistLogList.appendChild(tr);
    });
    refreshMobileTableFor(blacklistLogList);
  }

  function applyBlacklistLogFilter() {
    if (!blacklistLogList) return;
    if (!currentSoftware) {
      renderBlacklistLogRows([], { emptyMessage: '请选择软件位后查看黑名单记录' });
      return;
    }
    const keyword = (blacklistLogSearchInput?.value || '').trim().toLowerCase();
    let filtered = blacklistLogItems.slice();
    if (keyword) {
      filtered = filtered.filter(item => {
        const cardValue = (item.card ?? item.Card ?? '').toString().toLowerCase();
        return cardValue.includes(keyword);
      });
    }
    if (filtered.length === 0) {
      const emptyMessage = keyword
        ? '未找到匹配的记录'
        : (blacklistLogItems.length ? '未找到匹配的记录' : '当前软件暂无黑名单记录');
      renderBlacklistLogRows([], { emptyMessage, emptyClass: 'text-gray-400' });
      return;
    }
    renderBlacklistLogRows(filtered);
  }

  function renderBlacklistMachineRows(items, { emptyMessage = '暂无数据', emptyClass = 'text-gray-400' } = {}) {
    if (!blacklistMachineList) return;
    const previouslySelected = new Set(blacklistMachineSelected);
    if (!Array.isArray(items) || items.length === 0) {
      blacklistMachineList.innerHTML = `<tr><td colspan="6" class="px-4 py-6 text-center ${emptyClass}">${emptyMessage}</td></tr>`;
      blacklistMachineSelected.clear();
      if (blacklistMachineCheckAll) {
        blacklistMachineCheckAll.checked = false;
        blacklistMachineCheckAll.indeterminate = false;
      }
      updateBlacklistMachineActions();
      refreshMobileTableFor(blacklistMachineList);
      return;
    }
    const nextSelected = new Set();
    blacklistMachineList.innerHTML = '';
    items.forEach(item => {
      const tr = document.createElement('tr');
      tr.className = 'table-row border-b border-white/10';
      const rawValue = (item.value || item.Value || '').toString();
      const normalizedValue = rawValue.trim();
      const displaySoftware = normalizeSoftwareLabel(item.software ?? item.Software);
      const typeText = formatBlacklistType(item.type ?? item.Type);
      const remarks = item.remarks || item.Remarks || '-';
      tr.innerHTML = `
        <td class="px-4 py-3"><input type="checkbox" data-value="${normalizedValue}"></td>
        <td class="px-4 py-3 text-sm break-all">${rawValue || '-'}</td>
        <td class="px-4 py-3 text-sm">${displaySoftware}</td>
        <td class="px-4 py-3 text-sm">${typeText}</td>
        <td class="px-4 py-3 text-sm">${remarks}</td>
        <td class="px-4 py-3 text-sm"><div class="blacklist-machine-actions flex flex-wrap gap-2"></div></td>
      `;
      const checkbox = tr.querySelector('input[type="checkbox"][data-value]');
      if (checkbox) {
        const isSelected = normalizedValue && previouslySelected.has(normalizedValue);
        checkbox.checked = !!isSelected;
        if (isSelected) {
          nextSelected.add(normalizedValue);
        }
        checkbox.addEventListener('change', (e) => {
          const value = checkbox.dataset.value || '';
          if (!value) return;
          if (e.target.checked) blacklistMachineSelected.add(value);
          else blacklistMachineSelected.delete(value);
          updateBlacklistMachineActions();
        });
      }
      const actions = [
        {
          label: '删除',
          className: 'px-3 py-1 glass rounded text-red-300 hover:text-red-100',
          onClick: () => {
            if (!normalizedValue) return;
            return deleteBlacklistMachines([normalizedValue]);
          },
          closeOnClick: true
        }
      ];
      const actionContainer = tr.querySelector('.blacklist-machine-actions');
      renderActionButtons(actionContainer, actions);
      const detailItems = [
        { label: '软件位', content: displaySoftware },
        { label: '类型', content: typeText },
        { label: '备注', content: remarks || '-' }
      ];
      tr._mobileDetailItems = detailItems;
      tr._mobileActions = actions;
      tr._mobileInlineDetail = true;
      tr.dataset.mobileTitle = rawValue || normalizedValue || '黑名单';
      if (actionContainer) {
        const detailBtn = document.createElement('button');
        detailBtn.type = 'button';
        detailBtn.className = 'px-3 py-1 glass rounded mobile-inline-action gap-1 text-sm';
        detailBtn.innerHTML = '<i class="fa fa-circle-info"></i><span>详情</span>';
        detailBtn.addEventListener('click', () => {
          const helper = window.MobileFriendly;
          if (helper && typeof helper.showDetail === 'function') {
            helper.showDetail({
              title: tr.dataset.mobileTitle || '黑名单',
              items: detailItems,
              actions
            });
          }
        });
        actionContainer.prepend(detailBtn);
      }
      blacklistMachineList.appendChild(tr);
    });
    blacklistMachineSelected.clear();
    nextSelected.forEach(value => blacklistMachineSelected.add(value));
    if (blacklistMachineCheckAll) {
      const total = items.length;
      const selectedCount = blacklistMachineSelected.size;
      if (selectedCount === 0) {
        blacklistMachineCheckAll.checked = false;
        blacklistMachineCheckAll.indeterminate = false;
      } else if (selectedCount === total) {
        blacklistMachineCheckAll.checked = true;
        blacklistMachineCheckAll.indeterminate = false;
      } else {
        blacklistMachineCheckAll.checked = false;
        blacklistMachineCheckAll.indeterminate = true;
      }
    }
    updateBlacklistMachineActions();
    refreshMobileTableFor(blacklistMachineList);
  }

  async function fetchBlacklistLogs(limit = 200) {
    if (!blacklistLogList) return;
    if (!currentSoftware) {
      blacklistLogItems = [];
      renderBlacklistLogRows([], { emptyMessage: '请选择软件位后查看黑名单记录' });
      return;
    }
    renderBlacklistLogRows([], { emptyMessage: '加载中...', emptyClass: 'text-gray-400' });
    try {
      const resp = await api(`${API_BASE_URL}/Blacklist/logs?limit=${encodeURIComponent(limit)}`);
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      const data = await resp.json();
      if (data?.code === 0) {
        const items = Array.isArray(data?.data?.items) ? data.data.items : [];
        const filtered = items.filter(item => matchesCurrentSoftware(item.software ?? item.Software));
        blacklistLogItems = filtered;
        applyBlacklistLogFilter();
      } else {
        blacklistLogItems = [];
        renderBlacklistLogRows([], { emptyMessage: '获取失败', emptyClass: 'text-red-400' });
        showToast(data?.message || '获取黑名单记录失败', 'error');
      }
    } catch (err) {
      console.error('fetchBlacklistLogs error:', err);
      blacklistLogItems = [];
      renderBlacklistLogRows([], { emptyMessage: '获取失败', emptyClass: 'text-red-400' });
      showToast('获取黑名单记录失败：' + err.message, 'error');
    }
  }

  async function fetchBlacklistMachines() {
    if (!blacklistMachineList) return;
    if (!currentSoftware) {
      renderBlacklistMachineRows([], { emptyMessage: '请选择软件位后管理黑名单机器码' });
      return;
    }
    if (blacklistMachineCheckAll) {
      blacklistMachineCheckAll.checked = false;
      blacklistMachineCheckAll.indeterminate = false;
    }
    renderBlacklistMachineRows([], { emptyMessage: '加载中...', emptyClass: 'text-gray-400' });
    try {
      const resp = await api(`${API_BASE_URL}/Blacklist/machines`);
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      const data = await resp.json();
      if (data?.code === 0) {
        const items = Array.isArray(data?.data?.items) ? data.data.items : [];
        const filtered = items.filter(item => matchesCurrentSoftware(item.software ?? item.Software));
        renderBlacklistMachineRows(filtered, { emptyMessage: '当前软件暂无黑名单记录' });
      } else {
        renderBlacklistMachineRows([], { emptyMessage: '获取失败', emptyClass: 'text-red-400' });
        showToast(data?.message || '获取黑名单失败', 'error');
      }
    } catch (err) {
      console.error('fetchBlacklistMachines error:', err);
      renderBlacklistMachineRows([], { emptyMessage: '获取失败', emptyClass: 'text-red-400' });
      showToast('获取黑名单失败：' + err.message, 'error');
    }
  }

  if (blacklistLogSearchBtn) {
    blacklistLogSearchBtn.addEventListener('click', (e) => {
      e.preventDefault();
      applyBlacklistLogFilter();
    });
  }

  if (blacklistLogResetBtn) {
    blacklistLogResetBtn.addEventListener('click', (e) => {
      e.preventDefault();
      if (blacklistLogSearchInput) {
        blacklistLogSearchInput.value = '';
      }
      applyBlacklistLogFilter();
    });
  }

  if (blacklistLogSearchInput) {
    blacklistLogSearchInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        applyBlacklistLogFilter();
      }
    });
    blacklistLogSearchInput.addEventListener('input', () => {
      if (!blacklistLogSearchInput.value) {
        applyBlacklistLogFilter();
      }
    });
  }

  async function deleteBlacklistMachines(values) {
    const targets = (values || [])
      .map(value => (value || '').toString().trim())
      .filter(value => value.length);

    if (!targets.length) {
      showToast('请选择需要删除的机器码', 'error');
      return;
    }

    try {
      const resp = await api(`${API_BASE_URL}/Blacklist/machines/delete`, {
        method: 'POST',
        body: JSON.stringify({ values: targets })
      });
      const data = await resp.json();
      if (data?.code === 0) {
        showToast(data?.message || '删除成功');
        targets.forEach(value => blacklistMachineSelected.delete(value));
        await fetchBlacklistMachines();
      } else {
        showToast(data?.message || '删除失败', 'error');
      }
    } catch (err) {
      console.error('delete blacklist machine error:', err);
      showToast('删除失败：' + err.message, 'error');
    }
  }

  if (blacklistMachineCheckAll) {
    blacklistMachineCheckAll.addEventListener('change', (e) => {
      const checked = !!e.target.checked;
      const checkboxes = blacklistMachineList?.querySelectorAll('input[type="checkbox"][data-value]');
      if (!checkboxes) return;
      checkboxes.forEach(cb => {
        const value = cb.dataset.value || '';
        if (!value) return;
        cb.checked = checked;
        if (checked) blacklistMachineSelected.add(value);
        else blacklistMachineSelected.delete(value);
      });
      if (!checked) blacklistMachineCheckAll.indeterminate = false;
      updateBlacklistMachineActions();
    });
  }

  if (blacklistMachineDeleteSelected) {
    blacklistMachineDeleteSelected.addEventListener('click', () => {
      deleteBlacklistMachines(Array.from(blacklistMachineSelected));
    });
  }

  if (blacklistMachineRefreshBtn) {
    blacklistMachineRefreshBtn.addEventListener('click', () => fetchBlacklistMachines());
  }

  updateBlacklistMachineActions();

  if (blacklistMachineForm) {
    blacklistMachineForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const value = (blacklistMachineValue?.value || '').trim();
      if (!value) {
        showToast('请输入需要封禁的机器码', 'error');
        return;
      }
      const payload = {
        value,
        type: Number(blacklistMachineType?.value || 2),
        remarks: (blacklistMachineRemark?.value || '').trim() || null
      };
      try {
        const resp = await api(`${API_BASE_URL}/Blacklist/machines`, {
          method: 'POST',
          body: JSON.stringify(payload)
        });
        const data = await resp.json();
        if (data?.code === 0) {
          showToast(data?.message || '已加入黑名单');
          if (blacklistMachineValue) blacklistMachineValue.value = '';
          if (blacklistMachineRemark) blacklistMachineRemark.value = '';
          fetchBlacklistMachines();
        } else {
          showToast(data?.message || '添加失败', 'error');
        }
      } catch (err) {
        console.error('add blacklist machine error:', err);
        showToast('添加失败：' + err.message, 'error');
      }
    });
  }

  // ===== 代理通用弹窗 =====
  const modalAgent = document.getElementById('modal-agent');
  const agentModalTitle = document.getElementById('agent-modal-title');
  const agentModalBody = document.getElementById('agent-modal-body');
  const agentModalClose = document.getElementById('agent-modal-close');
  const agentModalSubmit = document.getElementById('agent-modal-submit');
  let agentModalMode = null; let agentModalUser = null;

  function openAgentModal(mode, username, extra=''){
    agentModalMode = mode; agentModalUser = username; modalAgent.style.display='flex';
    if (mode === 'add') {
      agentModalTitle.textContent = '添加子代理';
      agentModalBody.innerHTML = `
        <div class="grid grid-cols-2 gap-3">
          <div><label class="block text-sm mb-1 text-gray-400">账号</label><input id="fm-username" class="form-input" value=""></div>
          <div><label class="block text-sm mb-1 text-gray-400">初始密码</label><input id="fm-password" class="form-input" value=""></div>
          <div><label class="block text-sm mb-1 text-gray-400">初始余额</label><input id="fm-balance" type="number" class="form-input" value="0"></div>
          <div><label class="block text-sm mb-1 text-gray-400">初始库存时长(小时)</label><input id="fm-timeStock" type="number" class="form-input" value="0"></div>
          <div><label class="block text-sm mb-1 text-gray-400">返利率(%)</label><input id="fm-parities" type="number" class="form-input" value="100"></div>
          <div><label class="block text-sm mb-1 text-gray-400">累计返利(%)</label><input id="fm-totalParities" type="number" class="form-input" value="100"></div>
          <div class="col-span-2"><label class="block text-sm mb-1 text-gray-400">备注</label><input id="fm-remark" class="form-input" value=""></div>
          <div class="col-span-2">
            <label class="block text-sm mb-1 text-gray-400">卡密类型权限（默认全选）</label>
            <div id="fm-add-cardtypes" class="flex flex-wrap gap-2"></div>
            <p id="fm-add-cardtypes-hint" class="text-xs text-gray-500 mt-1"></p>
          </div>
        </div>`;
      populateAddAgentCardTypes();
    } else if (mode === 'addMoney') {
      agentModalTitle.textContent = `给【${username}】加款/时长`;
      agentModalBody.innerHTML = `
        <div class="grid grid-cols-2 gap-3">
          <div><label class="block text-sm mb-1 text-gray-400">金额（可负数扣款）</label><input id="fm-balance" type="number" class="form-input" value="0"></div>
          <div><label class="block text-sm mb-1 text-gray-400">时长库存(小时，正负皆可)</label><input id="fm-timeStock" type="number" class="form-input" value="0"></div>
        </div>`;
    } else if (mode === 'remark') {
      agentModalTitle.textContent = `修改备注 - ${username}`;
      agentModalBody.innerHTML = `<label class="block text-sm mb-1 text-gray-400">备注</label><input id="fm-remark" class="form-input" value="${extra||''}">`;
    } else if (mode === 'password') {
      const currentPwd = extra || '';
      agentModalTitle.textContent = `修改密码 - ${username}`;
      agentModalBody.innerHTML = `
        <div class="space-y-3">
          <div class="text-sm text-gray-400">当前密码：<span class="text-white">${currentPwd ? currentPwd : '-'}</span></div>
          <div>
            <label class="block text-sm mb-1 text-gray-400">新密码</label>
            <input id="fm-newPassword" class="form-input" type="password" value="">
          </div>
        </div>`;
      setTimeout(() => { document.getElementById('fm-newPassword')?.focus(); }, 0);
    } else if (mode === 'cardType') {
      agentModalTitle.textContent = `设置可用卡密类型 - ${username}`;
      agentModalBody.innerHTML = `<label class="block text-sm mb-1 text-gray-400">卡密类型（多选）</label><div id="fm-cardtypes" class="flex flex-wrap gap-2"></div>`;
      // 拉取该代理的可用类型
      loadAgentCardTypes(username);
    }
  }
  agentModalClose.addEventListener('click', () => modalAgent.style.display='none');
  modalAgent.addEventListener('click', (e)=>{ if (e.target===modalAgent) modalAgent.style.display='none'; });

  function getCurrentAgentCardTypes() {
    if (!currentSoftware) return [];
    const raw = currentSoftware.agentInfo?.cardTypes;
    if (!Array.isArray(raw)) return [];
    return raw
      .map(item => (typeof item === 'string' ? item.trim() : '').trim())
      .filter(item => item.length > 0);
  }

  async function populateAddAgentCardTypes() {
    const wrap = document.getElementById('fm-add-cardtypes');
    const hint = document.getElementById('fm-add-cardtypes-hint');
    if (!wrap) return;
    let available = getCurrentAgentCardTypes();

    if ((!available || available.length === 0) && currentSoftware?.softwareName) {
      try {
        available = await ensureCardTypes(currentSoftware.softwareName);
      } catch (err) {
        console.error('populateAddAgentCardTypes error:', err);
        available = [];
      }
    }

    const normalized = Array.from(new Set((available || [])
      .map(name => (typeof name === 'string' ? name.trim() : ''))
      .filter(name => name.length > 0)));

    wrap.innerHTML = '';

    if (normalized.length === 0) {
      wrap.innerHTML = '<div class="text-gray-400">暂无可授权的卡密类型</div>';
      if (hint) hint.textContent = '';
      return;
    }

    normalized.forEach((name, index) => {
      const label = document.createElement('label');
      label.className = 'flex items-center gap-2 px-3 py-1 rounded border border-white/10 text-sm';
      const checkboxId = `fm-add-cardtype-${index}`;
      label.innerHTML = `<input type="checkbox" id="${checkboxId}" value="${name}" checked><span>${name}</span>`;
      wrap.appendChild(label);
    });

    if (hint) {
      hint.textContent = '默认勾选全部卡密类型，可取消不需要授权的类型。';
    }
  }

  async function loadAgentCardTypes(username){
    const wrap = document.getElementById('fm-cardtypes'); wrap.innerHTML = '<div class="text-gray-400">加载中...</div>';
    try {
      const r = await api(`${API_BASE_URL}/Agent/getAgentCardType`, { method:'POST', body: JSON.stringify({ software: currentSoftware.softwareName, username }) });
      const j = await r.json();
      const cur = (j?.data?.cardTypes) || [];
      wrap.innerHTML = '';
      cardTypeNames.forEach(name => {
        const id = 'ct-' + name;
        const chk = document.createElement('label');
        chk.className = 'flex items-center gap-2';
        chk.innerHTML = `<input type="checkbox" id="${id}" value="${name}" ${cur.includes(name)?'checked':''}><span>${name}</span>`;
        wrap.appendChild(chk);
      });
    } catch(e){ console.error(e); wrap.innerHTML = '<div class="text-red-400">获取失败</div>'; }
  }

  agentModalSubmit.addEventListener('click', async () => {
    try {
      if (agentModalMode === 'add') {
        const cardTypeInputs = Array.from(document.querySelectorAll('#fm-add-cardtypes input[type="checkbox"]'));
        const selectedCardTypes = cardTypeInputs
          .filter(input => input.checked)
          .map(input => (input.value || '').trim())
          .filter(value => value.length > 0);
        if (cardTypeInputs.length > 0 && selectedCardTypes.length === 0) {
          showToast('请至少选择一个卡密类型', 'error');
          return;
        }
        const payload = {
          software: currentSoftware.softwareName,
          username: document.getElementById('fm-username').value.trim(),
          password: document.getElementById('fm-password').value,
          initialBalance: Number(document.getElementById('fm-balance').value||0),
          initialTimeStock: Number(document.getElementById('fm-timeStock').value||0),
          parities: Number(document.getElementById('fm-parities').value || '100'),
          totalParities: Number(document.getElementById('fm-totalParities').value || '100'),
          remark: document.getElementById('fm-remark').value.trim() || null,
          cardTypes: selectedCardTypes
        };
        if (!Number.isFinite(payload.parities)) payload.parities = 100;
        if (!Number.isFinite(payload.totalParities)) payload.totalParities = 100;
        const r = await api(`${API_BASE_URL}/Agent/createSubAgent`, { method:'POST', body: JSON.stringify(payload) });

        const j = await r.json(); if (j.code===0){ showToast('创建成功'); modalAgent.style.display='none'; invalidateAgentCache(currentSoftware.softwareName); await refreshAgentOptionsForCurrentSoftware(); if (salesSoftwareSelect.value === currentSoftware.softwareName) { await refreshSalesAgents(currentSoftware.softwareName); } fetchAgentList(true); } else { showToast(j.message||'创建失败','error'); }
      } else if (agentModalMode === 'addMoney') {
        const payload = {
          software: currentSoftware.softwareName,
          username: agentModalUser,
          balance: Number(document.getElementById('fm-balance').value||0),
          timeStock: Number(document.getElementById('fm-timeStock').value||0)
        };
        const r = await api(`${API_BASE_URL}/Agent/addMoney`, { method:'POST', body: JSON.stringify(payload) });
        const j = await r.json(); if (j.code===0){ showToast('已提交'); modalAgent.style.display='none'; fetchAgentList(); } else { showToast(j.message||'失败','error'); }
      } else if (agentModalMode === 'remark') {
        const payload = { software: currentSoftware.softwareName, username: agentModalUser, remark: document.getElementById('fm-remark').value };
        const r = await api(`${API_BASE_URL}/Agent/updateAgentRemark`, { method:'POST', body: JSON.stringify(payload) });
        const j = await r.json(); if (j.code===0){ showToast('备注已更新'); modalAgent.style.display='none'; fetchAgentList(); } else { showToast(j.message||'失败','error'); }
      } else if (agentModalMode === 'password') {
        const newPassword = (document.getElementById('fm-newPassword').value || '').trim();
        if (!newPassword) { showToast('请输入新密码','error'); return; }
        const payload = { software: currentSoftware.softwareName, username: agentModalUser, newPassword };
        const r = await api(`${API_BASE_URL}/Agent/updateAgentPassword`, { method:'POST', body: JSON.stringify(payload) });
        const j = await r.json(); if (j.code===0){ showToast('密码已更新'); modalAgent.style.display='none'; fetchAgentList(); } else { showToast(j.message||'失败','error'); }
      } else if (agentModalMode === 'cardType') {
        const selected = Array.from(document.querySelectorAll('#fm-cardtypes input[type="checkbox"]:checked')).map(x=>x.value);
        const payload = { software: currentSoftware.softwareName, username: agentModalUser, cardTypes: selected };
        const r = await api(`${API_BASE_URL}/Agent/setAgentCardType`, { method:'POST', body: JSON.stringify(payload) });
        const j = await r.json(); if (j.code===0){ showToast('卡类型已更新'); modalAgent.style.display='none'; } else { showToast(j.message||'失败','error'); }
      }
    } catch(e){ console.error(e); showToast('请求失败','error'); }
  });

  // 添加子代理按钮
  const agentAddBtnEl = document.getElementById('agent-add-btn');
  agentAddBtnEl.addEventListener('click', () => openAgentModal('add'));
  const filterWhom = document.getElementById('filter-whom');
  const filterCardType = document.getElementById('filter-cardType');
  const filterStatus = document.getElementById('filter-status');
  const filterSearchType = document.getElementById('filter-searchType');
  const filterKeyword = document.getElementById('filter-keyword');
  const filterApply = document.getElementById('filter-apply');
  const filterReset = document.getElementById('filter-reset');
  const modalCreate = document.getElementById('modal-create');
  const btnCloseCreate = document.getElementById('btn-close-create');
  const createCardType = document.getElementById('create-cardType');
  const createQuantity = document.getElementById('create-quantity');
  const createRemarks = document.getElementById('create-remarks');
  const createCustomPrefix = document.getElementById('create-customPrefix');
  const btnCreate = document.getElementById('btn-create');

  let cardTypeNames = [];
  async function fetchCardTypes(){
    cardTypeNames = [];
    if (!currentSoftware) return;
    try {

      const items = await ensureCardTypes(currentSoftware.softwareName);
      filterCardType.innerHTML = '<option value="">全部</option>';
      createCardType.innerHTML = '';

      items.forEach(name => {
        cardTypeNames.push(name);
        const opt1 = document.createElement('option'); opt1.value = name; opt1.textContent = name; filterCardType.appendChild(opt1);
        const opt2 = document.createElement('option'); opt2.value = name; opt2.textContent = name; createCardType.appendChild(opt2);
      });
    } catch(e){ console.warn(e); }
  }

  let filter = { whom: '', cardType: '', status: '', searchType: 0, keyword: '' };
  let currentCardPage = 1; const cardsPerPage = 10; let totalCardPages = 0;

  filterApply.addEventListener('click', () => {
    const selectedWhom = (filterWhom.value || '').trim();
    filter.whom = selectedWhom;
    filter.cardType = (filterCardType.value || '').trim();
    filter.status = (filterStatus.value || '').trim();
    filter.searchType = parseInt(filterSearchType.value || '0', 10);
    filter.keyword = (filterKeyword.value || '').trim();
    currentCardPage = 1; fetchCardList();
  });
  filterReset.addEventListener('click', () => {

    filter = { whom: filterWhom.value, cardType: '', status: '', searchType: 0, keyword: '' };

    filterWhom.value = '';
    filterCardType.value='';
    filterStatus.value='';
    filterSearchType.value='0';
    filterKeyword.value='';
    filter = { whom: '', cardType: '', status: '', searchType: 0, keyword: '' };
    currentCardPage = 1; fetchCardList();
  });

  cardPrevPage.addEventListener('click', () => { if (currentCardPage>1){ currentCardPage--; fetchCardList(); } });
  cardNextPage.addEventListener('click', () => { if (currentCardPage<totalCardPages){ currentCardPage++; fetchCardList(); } });

  function splitKeywordInput(raw){
    if (!raw) return [];
    return raw
      .replace(/\r\n/g, '\n')
      .split(/[\n,，;；]+/)
      .map(item => item.trim())
      .filter(item => item.length > 0);
  }

  function buildCardListPayload(){
    const payload = { software: currentSoftware.softwareName, page: currentCardPage, limit: cardsPerPage };
    const whom = (filter.whom || '').trim();
    if (whom) {
      payload.agent = whom;
      payload.includeDescendants = false;
    } else {
      payload.includeDescendants = true;
    }
    if ((filter.status||'').trim() !== '') payload.status = filter.status.trim();
    const rawKeyword = filter.keyword || '';
    const keywordParts = splitKeywordInput(rawKeyword);
    const firstKeyword = keywordParts[0] || (rawKeyword || '').trim();
    const ipRegex = /^(\d{1,3}\.){3}\d{1,3}$/;
    const isSingleIP = keywordParts.length === 1 && ipRegex.test(keywordParts[0] || '');
    let resolvedSearchType = filter.searchType;
    if (!isSingleIP && filter.cardType && (resolvedSearchType === 0 || resolvedSearchType === 3)) resolvedSearchType = 3;
    if (isSingleIP && (resolvedSearchType === 0 || resolvedSearchType === 2)) resolvedSearchType = 2;
    payload.searchType = Number.isFinite(resolvedSearchType) ? resolvedSearchType : 0;
    let keywords = [];
    if (payload.searchType === 3 && filter.cardType) {
      keywords = [filter.cardType];
    } else if (payload.searchType === 2) {
      const ipKeywords = keywordParts.length ? keywordParts : splitKeywordInput(firstKeyword);
      keywords = ipKeywords.filter(item => ipRegex.test(item));
    } else {
      keywords = keywordParts.length ? keywordParts : (firstKeyword ? [firstKeyword.trim()] : []);
    }
    if (keywords.length) {
      payload.keywords = Array.from(new Set(keywords));
    }
    return payload;
  }

  function renderCardPagination(total, page, pageSize){
    totalCardPages = Math.max(1, Math.ceil(total / pageSize));
    cardPages.innerHTML = '';
    const addButton = (p) => {
      const btn = document.createElement('button');
      btn.className = 'pagination-item' + (p === page ? ' active' : '');
      btn.textContent = p;
      btn.addEventListener('click', () => {
        if (currentCardPage === p) return;
        currentCardPage = p;
        fetchCardList();
      });
      cardPages.appendChild(btn);
    };

    const span = 2;
    let start = Math.max(1, page - span);
    let end = Math.min(totalCardPages, page + span);
    if (start > 1) addButton(1);
    if (start > 2) {
      const ellipsis = document.createElement('span');
      ellipsis.className = 'px-2 text-gray-500';
      ellipsis.textContent = '...';
      cardPages.appendChild(ellipsis);
    }
    for (let p = start; p <= end; p++) addButton(p);
    if (end < totalCardPages - 1) {
      const ellipsis = document.createElement('span');
      ellipsis.className = 'px-2 text-gray-500';
      ellipsis.textContent = '...';
      cardPages.appendChild(ellipsis);
    }
    if (end < totalCardPages) addButton(totalCardPages);

    cardPrevPage.disabled = page <= 1;
    cardNextPage.disabled = page >= totalCardPages;
  }

  function createCardRowActions(cardKey, isEnabled){
    const actions = [
      {
        label: '解绑',
        className: 'px-3 py-1 glass rounded hover:opacity-90',
        onClick: () => unbindCard(cardKey),
        closeOnClick: false
      }
    ];
    if (isEnabled) {
      actions.push({
        label: '禁用',
        className: 'px-3 py-1 glass rounded hover:opacity-90',
        onClick: () => updateCardStatus(cardKey, 'disableCard', '已禁用'),
        closeOnClick: false
      });
    } else {
      actions.push({
        label: '启用',
        className: 'px-3 py-1 glass rounded hover:opacity-90',
        onClick: () => updateCardStatus(cardKey, 'enableCard', '已启用'),
        closeOnClick: false
      });
      actions.push({
        label: '解封启用',
        className: 'px-3 py-1 glass rounded hover:opacity-90',
        onClick: () => updateCardStatus(cardKey, 'enableCardWithBanTimeReturn', '已解封并启用'),
        closeOnClick: false
      });
    }
    return actions;
  }

  function renderCardRow(item){
    const cardKey = item.card || item.Prefix_Name || item.prefix_Name || '-';
    const cardType = item.cardType || item.CardType || '-';
    const stateText = (item.state || item.State || '').trim() || '未知';
    const isEnabled = stateText === '启用';
    const ip = item.IP || item.ip || '-';
    const creator = (item.whom || item.Whom || '').trim();
    const activatedAt = fmtFullDateTime(item.ActivateTime_ ?? item.activateTime_ ?? item.activateTime ?? item.activate_at);
    const expireAt = fmtFullDateTime(item.ExpiredTime__ ?? item.expiredTime__ ?? item.expiredTime_ ?? item.expiredTime ?? item.expireTime_ ?? item.expireTime);
    const createdAt = fmtFullDateTime(item.CreateData_ ?? item.createData_ ?? item.createData ?? item.create_at);
    const machineCodes = Array.isArray(item.machineCodes)
      ? item.machineCodes.filter(code => typeof code === 'string' && code.trim().length)
      : [];
    const primaryMachine = typeof item.machineCode === 'string' ? item.machineCode.trim() : '';
    if (primaryMachine && !machineCodes.includes(primaryMachine)) {
      machineCodes.unshift(primaryMachine);
    }
    const machineHtml = machineCodes.length
      ? machineCodes.map(code => `<span class="inline-flex items-center px-2 py-1 rounded bg-white/5 text-xs text-[var(--secondary)] mb-1 mr-1">${code}</span>`).join('')
      : '<span class="text-gray-500">-</span>';

    const statusHtml = isEnabled ? '<span class="tag tag-green">启用</span>' : '<span class="tag tag-gray">禁用</span>';

    const tr = document.createElement('tr');
    tr.className = 'table-row border-b border-white/10';
    tr.innerHTML = `
      <td class="px-4 py-4">
        <div class="card-actions"></div>
      </td>
      <td class="px-4 py-4 text-sm text-white break-all card-key-cell">
        <div class="card-key-text">${cardKey}</div>
        <div class="card-key-status mobile-only">${statusHtml}</div>
      </td>
      <td class="px-4 py-4 text-sm machine-cell">${machineHtml}</td>
      <td class="px-4 py-4 text-sm">${creator || '-'}</td>
      <td class="px-4 py-4 text-sm">${cardType || '-'}</td>
      <td class="px-4 py-4 card-status-col desktop-only">${statusHtml}</td>
      <td class="px-4 py-4 text-sm">${ip || '-'}</td>
      <td class="px-4 py-4 text-sm">${activatedAt}</td>
      <td class="px-4 py-4 text-sm">${expireAt}</td>
      <td class="px-4 py-4 text-sm">${createdAt}</td>
    `;

    const actions = createCardRowActions(cardKey, isEnabled);
    const actionsContainer = tr.querySelector('.card-actions');
    renderActionButtons(actionsContainer, actions);
    const detailItems = [
      { label: '机器码', content: machineHtml },
      { label: '制卡人', content: creator || '-' },
      { label: '类型', content: cardType || '-' },
      { label: '状态', content: statusHtml },
      { label: 'IP', content: ip || '-' },
      { label: '激活时间', content: activatedAt || '-' },
      { label: '到期时间', content: expireAt || '-' },
      { label: '生成时间', content: createdAt || '-' }
    ];
    if (actionsContainer) {
      const detailBtn = document.createElement('button');
      detailBtn.type = 'button';
      detailBtn.className = 'px-3 py-1 glass rounded mobile-inline-action gap-1 text-sm';
      detailBtn.innerHTML = '<i class="fa fa-circle-info"></i><span>详情</span>';
      detailBtn.addEventListener('click', () => {
        const helper = window.MobileFriendly;
        if (helper && typeof helper.showDetail === 'function') {
          helper.showDetail({
            title: cardKey || '卡密详情',
            items: detailItems,
            actions
          });
        }
      });
      actionsContainer.prepend(detailBtn);
    }
    tr._mobileActions = actions;
    tr._mobileDetailItems = detailItems;
    tr._mobileInlineDetail = true;
    tr.dataset.mobileTitle = cardKey;
    return tr;
  }

  async function updateCardStatus(cardKey, action, successMessage){
    if (!currentSoftware) return;
    try {
      const resp = await api(`${API_BASE_URL}/Card/${action}`, {
        method: 'POST',
        body: JSON.stringify({ software: currentSoftware.softwareName, cardKey })
      });
      const data = await resp.json();
      if (data?.code === 0) {
        showToast(successMessage || '操作成功');
        fetchCardList();
      } else {
        showToast(data?.message || '操作失败', 'error');
      }
    } catch (e) {
      console.error(`${action} error:`, e);
      showToast('操作失败：' + e.message, 'error');
    }
  }

  async function unbindCard(cardKey) {
    if (!currentSoftware) return;
    try {
      const resp = await api(`${API_BASE_URL}/Card/unbindCard`, {
        method: 'POST',
        body: JSON.stringify({ software: currentSoftware.softwareName, cardKey })
      });
      const data = await resp.json();
      if (data?.code === 0) {
        showToast(data?.message || '解绑成功');
        fetchCardList();
      } else {
        showToast(data?.message || '解绑失败', 'error');
      }
    } catch (err) {
      console.error('unbindCard error:', err);
      showToast('解绑失败：' + err.message, 'error');
    }
  }

  async function fetchCardList(){
    if (!currentSoftware) return;
    const payload = buildCardListPayload();
    cardLoading.classList.remove('hidden');
    cardList.innerHTML = '<tr><td colspan="9" class="px-6 py-8 text-center text-gray-400">加载中...</td></tr>';
    try {
      const resp = await api(`${API_BASE_URL}/Card/getCardList`, { method:'POST', body: JSON.stringify(payload) });
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      const json = await resp.json();
      if (json?.code !== 0) throw new Error(json?.message || '获取失败');
      const inner = json.data || {};
      const list = Array.isArray(inner.data) ? inner.data : (Array.isArray(inner.items) ? inner.items : []);
      const total = Number(inner.total ?? inner.count ?? list.length ?? 0);
      if (!list.length) {
        cardList.innerHTML = '<tr><td colspan="9" class="px-6 py-8 text-center text-gray-400">暂无数据</td></tr>';
      } else {
        cardList.innerHTML = '';
        list.forEach(item => cardList.appendChild(renderCardRow(item)));
      }
      const start = total ? ((currentCardPage - 1) * cardsPerPage + 1) : 0;
      const end = total ? Math.min(currentCardPage * cardsPerPage, total) : 0;
      cardStartItem.textContent = start;
      cardEndItem.textContent = end;
      cardTotalItems.textContent = total;
      renderCardPagination(total, currentCardPage, cardsPerPage);
    } catch (e) {
      console.error('fetchCardList error:', e);
      cardList.innerHTML = `<tr><td colspan="7" class="px-6 py-8 text-center text-red-400">${e.message || '获取失败'}</td></tr>`;
      showToast(e.message || '获取卡密失败', 'error');
    } finally {
      cardLoading.classList.add('hidden');
    }
  }

  // 新增卡密 modal
  const btnOpenCreateEl = document.getElementById('btn-open-create');
  const btnCloseCreateEl = document.getElementById('btn-close-create');
  btnOpenCreateEl.addEventListener('click', () => { if (!currentSoftware) return showToast('请先选择软件','error'); modalCreate.style.display='flex'; if (cardTypeNames.length && !createCardType.value) createCardType.value = cardTypeNames[0]; });
  btnCloseCreateEl.addEventListener('click', () => modalCreate.style.display='none');
  modalCreate.addEventListener('click', (e)=>{ if (e.target===modalCreate) modalCreate.style.display='none'; });
  if (btnCloseGenerated) {
    btnCloseGenerated.addEventListener('click', () => { modalGenerated.style.display = 'none'; });
  }
  if (modalGenerated) {
    modalGenerated.addEventListener('click', (e)=>{ if (e.target===modalGenerated) modalGenerated.style.display='none'; });
  }
  if (btnCopyGenerated) {
    btnCopyGenerated.addEventListener('click', handleCopyGeneratedCards);
  }
  if (btnExportGenerated) {
    btnExportGenerated.addEventListener('click', handleExportGeneratedCards);
  }
  btnCreate.addEventListener('click', async () => {
    const cardType = (createCardType.value || '').trim();
    const qty = Math.max(1, Math.min(500, parseInt(createQuantity.value || '1', 10)));
    const remarks = (createRemarks.value || '').trim();
    const customPrefix = (createCustomPrefix.value || '').trim();
    if (!cardType) return showToast('请选择卡密类型', 'error');
    if (customPrefix && customPrefix.length > 32) return showToast('自定义前缀长度不能超过32位', 'error');
    const payload = { software: currentSoftware.softwareName, cardType, quantity: qty };
    if (remarks) payload.remarks = remarks;
    if (customPrefix) payload.customPrefix = customPrefix;
    try {
      const resp = await api(`${API_BASE_URL}/Card/generateCards`, { method:'POST', body: JSON.stringify(payload) });
      const data = await resp.json();
      if (data && data.code === 0) {
        const result = data.data || {};
        const generated = Array.isArray(result.generatedCards) && result.generatedCards.length
          ? result.generatedCards
          : (Array.isArray(result.sampleCards) ? result.sampleCards : []);
        showToast('生成成功');
        modalCreate.style.display = 'none';
        fetchCardList();
        showGeneratedCards(generated, result.cardType || cardType);
      } else { showToast((data && data.message) || '生成失败', 'error'); }
    } catch (e) { console.error('generate error:', e); showToast('生成失败：' + e.message, 'error'); }
  });

  window.addEventListener('resize', () => {
    if (trendChart && echartsAvailable) { try { trendChart.resize(); } catch (err) { console.warn(err); } }
    if (usageChart && echartsAvailable) { try { usageChart.resize(); } catch (err) { console.warn(err); } }
  });

  // 启动
  (async function boot(){
    if (!isTokenValid()) {
      clearAuthToken();
      if (pageType === 'login') {
        loginPage.classList.remove('hidden');
      } else {
        pushLoginMessage('请先登录', 'error');
        window.location.href = './login.html';
      }
      return;
    }

    try {
      const resp = await api(`${API_BASE_URL}/Auth/getUserInfo`, { method:'POST' });
      if (resp.ok) {
        const data = await resp.json();
        if (data && data.code === 0) {
          const payload = data.data || {};
          if (payload.tokenExpiresAt) {
            updateTokenExpiry(payload.tokenExpiresAt);
          }
          currentUser = { username: payload.username || currentUser?.username || '', isSuper: !!payload.isSuper };
          updateLinkRecordsButtons();
          await afterLogin();
          return;
        }
      }
    } catch (err) {
      console.warn('auto login skipped', err);
    }

    clearAuthToken();
    if (pageType === 'login') {
      loginPage.classList.remove('hidden');
    } else {
      pushLoginMessage('登录状态已过期，请重新登录', 'error');
      window.location.href = './login.html';
    }
  })();
});

