import { computed, reactive, ref, watch } from 'vue';
import { defineStore } from 'pinia';
import { apiRequest } from '@/common/api';
import type {
  PlatformAgentLoginResponse,
  PlatformAgentProfile,
  PlatformAuthorLoginResponse,
  PlatformAuthorProfile,
  PlatformAuthorSoftware,
  PlatformAuthorSoftwareCodeResponse,
  PlatformAuthorUpdatePayload,
  PlatformBinding,
  WeChatBindResult,
  WeChatBindingInfo,
  WeChatTemplateConfig
} from '@/common/types';
import {
  clearAuthorToken,
  clearPlatformToken,
  clearSelectedSoftwareName,
  clearToken,
  getAuthorToken,
  getPlatformToken,
  getSelectedSoftwareName,
  getWeChatSubscriptions,
  setAuthorToken,
  setPlatformToken,
  setSelectedSoftwareName,
  setWeChatSubscriptions
} from '@/utils/storage';

interface AgentRegisterPayload {
  username: string;
  email: string;
  password: string;
  softwareCode: string;
  authorAccount: string;
  authorPassword: string;
}

interface AgentLoginPayload {
  account: string;
  password: string;
}

interface AuthorRegisterPayload {
  username: string;
  email: string;
  password: string;
  displayName: string;
  apiAddress: string;
  apiPort: number;
  softwareType: string;
}

interface AuthorSoftwarePayload {
  displayName: string;
  apiAddress: string;
  apiPort: number;
  softwareType: string;
}

interface AuthorLoginPayload {
  account: string;
  password: string;
}

interface BindingPayload {
  softwareCode: string;
  authorAccount: string;
  authorPassword: string;
}

export const usePlatformStore = defineStore('platform', () => {
  const agent = ref<PlatformAgentProfile | null>(null);
  const token = ref<string | null>(getPlatformToken());
  const expiresAt = ref<string | null>(null);
  const bindings = ref<PlatformBinding[]>([]);
  const selectedBinding = ref<PlatformBinding | null>(null);
  const authorProfile = ref<PlatformAuthorProfile | null>(null);
  const authorToken = ref<string | null>(getAuthorToken());
  const authorExpiresAt = ref<string | null>(null);
  const selectedAuthorSoftwareId = ref<number | null>(null);
  const agentWeChatBinding = ref<WeChatBindingInfo | null>(null);
  const authorWeChatBinding = ref<WeChatBindingInfo | null>(null);
  const agentWechatSubscriptions = ref<Record<string, boolean>>({});
  const authorWechatSubscriptions = ref<Record<string, boolean>>({});
  const wechatTemplates = ref<WeChatTemplateConfig | null>(null);
  const wechatTemplatePreviews = ref<Record<string, Record<string, string>>>({});

  const wechatTemplatesLoaded = ref(false);

  const loading = reactive({
    registerAgent: false,
    loginAgent: false,
    loginAgentWeChat: false,
    registerAuthor: false,
    loginAuthor: false,
    bindings: false,
    createBinding: false,
    deleteBinding: false,
    updateBinding: false,
    authorProfile: false,
    updateAuthor: false,
    regenerateAuthorCode: false,
    deleteAuthor: false,
    createAuthorSoftware: false,
    updateAuthorSoftware: false,
    deleteAuthorSoftware: false,
    wechatBinding: false,
    wechatBind: false,
    wechatUnbind: false,
    wechatTemplates: false
  });

  function refreshWechatSubscriptions(role: 'agent' | 'author') {
    const binding = role === 'agent' ? agentWeChatBinding.value : authorWeChatBinding.value;
    const target = role === 'agent' ? agentWechatSubscriptions : authorWechatSubscriptions;
    if (binding?.openId) {
      target.value = getWeChatSubscriptions(binding.openId);
    } else {
      target.value = {};
    }
  }

  watch(
    () => agentWeChatBinding.value?.openId ?? null,
    () => refreshWechatSubscriptions('agent')
  );

  watch(
    () => authorWeChatBinding.value?.openId ?? null,
    () => refreshWechatSubscriptions('author')
  );

  function setSession(session?: PlatformAgentLoginResponse | null) {
    if (!session) {
      agent.value = null;
      token.value = null;
      expiresAt.value = null;
      bindings.value = [];
      selectedBinding.value = null;
      agentWeChatBinding.value = null;
      clearPlatformToken();
      clearToken();
      clearSelectedSoftwareName();
      return;
    }

    agent.value = session.agent;
    token.value = session.token || token.value;
    expiresAt.value = session.expiresAt ?? null;
    bindings.value = session.bindings ?? [];

    if (session.token) {
      setPlatformToken(session.token);
    }

    const storedCode = getSelectedSoftwareName();
    const preferred = bindings.value.find((item) => item.softwareCode === storedCode);
    selectBinding(preferred || bindings.value[0] || null);
  }

  function selectBinding(binding: PlatformBinding | null) {
    selectedBinding.value = binding;
    if (!binding) {
      clearSelectedSoftwareName();
      return;
    }
    setSelectedSoftwareName(binding.softwareCode);
  }

  function setAuthorProfile(profile: PlatformAuthorProfile | null) {
    authorProfile.value = profile;
    if (!profile) {
      selectedAuthorSoftwareId.value = null;
      return;
    }

    const available = profile.softwares?.map((item) => item.softwareId) ?? [];
    const preferred = profile.primarySoftwareId ?? selectedAuthorSoftwareId.value ?? null;
    const resolved = preferred && available.includes(preferred) ? preferred : available[0] ?? null;
    selectedAuthorSoftwareId.value = resolved ?? null;
  }

  function selectAuthorSoftware(softwareId: number | null) {
    selectedAuthorSoftwareId.value = softwareId ?? null;
  }

  function setAuthorSession(session?: PlatformAuthorLoginResponse | null) {
    if (!session) {
      setAuthorProfile(null);
      authorToken.value = null;
      authorExpiresAt.value = null;
      authorWeChatBinding.value = null;
      clearAuthorToken();
      return;
    }

    setAuthorProfile(session.profile);
    authorToken.value = session.token;
    authorExpiresAt.value = session.expiresAt;
    setAuthorToken(session.token);
  }

  async function registerAgent(payload: AgentRegisterPayload) {
    loading.registerAgent = true;
    try {
      await apiRequest<PlatformAgentProfile>({
        url: '/api/agents/register',
        method: 'POST',
        data: payload
      });
      return true;
    } finally {
      loading.registerAgent = false;
    }
  }

  async function loginAgent(payload: AgentLoginPayload) {
    loading.loginAgent = true;
    try {
      const response = await apiRequest<PlatformAgentLoginResponse>({
        url: '/api/agents/login',
        method: 'POST',
        data: payload
      });
      setSession(response);
      await fetchWeChatBinding('agent').catch(() => undefined);
      await fetchWeChatTemplates().catch(() => undefined);
      return response;
    } finally {
      loading.loginAgent = false;
    }
  }

  async function loginAgentWithWeChat(jsCode: string) {
    if (!jsCode) {
      throw new Error('未获取到 jsCode');
    }

    loading.loginAgentWeChat = true;
    try {
      const response = await apiRequest<PlatformAgentLoginResponse>({
        url: '/api/agents/login/wechat',
        method: 'POST',
        skipProxy: true,
        data: { jsCode }
      });
      setSession(response);
      await fetchWeChatBinding('agent').catch(() => undefined);
      await fetchWeChatTemplates().catch(() => undefined);
      return response;
    } finally {
      loading.loginAgentWeChat = false;
    }
  }

  async function fetchAgentProfile() {
    loading.bindings = true;
    try {
      const response = await apiRequest<PlatformAgentLoginResponse>({
        url: '/api/agents/me',
        method: 'GET',
        auth: true
      });
      setSession({
        ...response,
        token: token.value || getPlatformToken() || ''
      });
      await fetchWeChatBinding('agent').catch(() => undefined);
      await fetchWeChatTemplates().catch(() => undefined);
      return response;
    } finally {
      loading.bindings = false;
    }
  }

  async function createBinding(payload: BindingPayload) {
    loading.createBinding = true;
    try {
      const result = await apiRequest<PlatformBinding>({
        url: '/api/bindings',
        method: 'POST',
        auth: true,
        data: payload
      });
      bindings.value = [result, ...bindings.value];
      if (!selectedBinding.value) {
        selectBinding(result);
      }
      return result;
    } finally {
      loading.createBinding = false;
    }
  }

  async function updateBinding(bindingId: number, payload: BindingPayload) {
    loading.updateBinding = true;
    try {
      const result = await apiRequest<PlatformBinding>({
        url: `/api/bindings/${bindingId}`,
        method: 'PUT',
        auth: true,
        data: {
          authorAccount: payload.authorAccount,
          authorPassword: payload.authorPassword
        }
      });
      bindings.value = bindings.value.map((item) => (item.bindingId === bindingId ? result : item));
      if (selectedBinding.value?.bindingId === bindingId) {
        selectBinding(result);
      }
      return result;
    } finally {
      loading.updateBinding = false;
    }
  }

  async function deleteBinding(bindingId: number) {
    loading.deleteBinding = true;
    try {
      await apiRequest<string>({
        url: `/api/bindings/${bindingId}`,
        method: 'DELETE',
        auth: true
      });
      bindings.value = bindings.value.filter((item) => item.bindingId !== bindingId);
      if (selectedBinding.value?.bindingId === bindingId) {
        selectBinding(bindings.value[0] || null);
      }
    } finally {
      loading.deleteBinding = false;
    }
  }

  async function registerAuthor(payload: AuthorRegisterPayload) {
    loading.registerAuthor = true;
    try {
      const response = await apiRequest<PlatformAuthorProfile>({
        url: '/api/authors/register',
        method: 'POST',
        data: payload
      });
      return response;
    } finally {
      loading.registerAuthor = false;
    }
  }

  async function loginAuthor(payload: AuthorLoginPayload) {
    loading.loginAuthor = true;
    try {
      const response = await apiRequest<PlatformAuthorLoginResponse>({
        url: '/api/authors/login',
        method: 'POST',
        data: payload
      });
      setAuthorSession(response);
      await fetchWeChatBinding('author').catch(() => undefined);
      return response.profile;
    } finally {
      loading.loginAuthor = false;
    }
  }

  async function fetchAuthorProfile() {
    loading.authorProfile = true;
    try {
      const profile = await apiRequest<PlatformAuthorProfile>({
        url: '/api/authors/me',
        method: 'GET',
        auth: true,
        authRole: 'author'
      });
      setAuthorProfile(profile);
      await fetchWeChatBinding('author').catch(() => undefined);
      return profile;
    } finally {
      loading.authorProfile = false;
    }
  }

  async function updateAuthorProfile(payload: PlatformAuthorUpdatePayload) {
    loading.updateAuthor = true;
    try {
      const profile = await apiRequest<PlatformAuthorProfile>({
        url: '/api/authors/me',
        method: 'PUT',
        auth: true,
        authRole: 'author',
        data: payload
      });
      setAuthorProfile(profile);
      return profile;
    } finally {
      loading.updateAuthor = false;
    }
  }

  async function regenerateAuthorCode(softwareId?: number) {
    loading.regenerateAuthorCode = true;
    try {
      const response = await apiRequest<PlatformAuthorSoftwareCodeResponse>({
        url: softwareId ? `/api/authors/me/regenerate-code?softwareId=${softwareId}` : '/api/authors/me/regenerate-code',
        method: 'POST',
        auth: true,
        authRole: 'author'
      });
      return response.softwareCode;
    } finally {
      loading.regenerateAuthorCode = false;
    }
  }

  async function createAuthorSoftware(payload: AuthorSoftwarePayload) {
    loading.createAuthorSoftware = true;
    try {
      const software = await apiRequest<PlatformAuthorSoftware>({
        url: '/api/authors/me/softwares',
        method: 'POST',
        auth: true,
        authRole: 'author',
        data: payload
      });
      await fetchAuthorProfile();
      selectedAuthorSoftwareId.value = software.softwareId;
      return software;
    } finally {
      loading.createAuthorSoftware = false;
    }
  }

  async function updateAuthorSoftware(softwareId: number, payload: AuthorSoftwarePayload) {
    loading.updateAuthorSoftware = true;
    try {
      await apiRequest<PlatformAuthorSoftware>({
        url: `/api/authors/me/softwares/${softwareId}`,
        method: 'PUT',
        auth: true,
        authRole: 'author',
        data: payload
      });
      const profile = await fetchAuthorProfile();
      if (profile) {
        selectedAuthorSoftwareId.value = softwareId;
      }
    } finally {
      loading.updateAuthorSoftware = false;
    }
  }

  async function deleteAuthorSoftware(softwareId: number) {
    loading.deleteAuthorSoftware = true;
    try {
      await apiRequest<string>({
        url: `/api/authors/me/softwares/${softwareId}`,
        method: 'DELETE',
        auth: true,
        authRole: 'author'
      });
      const profile = await fetchAuthorProfile();
      if (profile) {
        const exists = profile.softwares.some((item) => item.softwareId === selectedAuthorSoftwareId.value);
        if (!exists) {
          selectedAuthorSoftwareId.value = profile.primarySoftwareId ?? profile.softwares[0]?.softwareId ?? null;
        }
      }
    } finally {
      loading.deleteAuthorSoftware = false;
    }
  }

  async function deleteAuthorAccount() {
    loading.deleteAuthor = true;
    try {
      await apiRequest<string>({
        url: '/api/authors/me',
        method: 'DELETE',
        auth: true,
        authRole: 'author'
      });
      setAuthorSession(null);
    } finally {
      loading.deleteAuthor = false;
    }
  }

  async function fetchWeChatBinding(role: 'agent' | 'author' = 'agent') {
    const hasToken = role === 'agent' ? Boolean(token.value) : Boolean(authorToken.value);
    if (!hasToken) {
      if (role === 'agent') {
        agentWeChatBinding.value = null;
      } else {
        authorWeChatBinding.value = null;
      }
      return null;
    }

    loading.wechatBinding = true;
    try {
      const response = await apiRequest<WeChatBindingInfo | null>({
        url: '/api/wechat/binding',
        method: 'GET',
        auth: true,
        authRole: role,
        skipProxy: true
      });

      if (role === 'agent') {
        agentWeChatBinding.value = response ?? null;
        refreshWechatSubscriptions('agent');
      } else {
        authorWeChatBinding.value = response ?? null;
        refreshWechatSubscriptions('author');
      }

      if (role === 'agent') {
        await fetchWeChatTemplates().catch(() => undefined);
      }

      return response ?? null;
    } finally {
      loading.wechatBinding = false;
    }
  }

  async function fetchWeChatTemplates(force = false) {
    if (wechatTemplatesLoaded.value && !force && wechatTemplates.value) {
      return wechatTemplates.value;
    }

    loading.wechatTemplates = true;
    try {
      const response = await apiRequest<WeChatTemplateConfig>({
        url: '/api/wechat/templates',
        method: 'GET',
        skipProxy: true
      });
      wechatTemplates.value = response;
      wechatTemplatePreviews.value = response?.previewData ?? {};
      wechatTemplatesLoaded.value = true;
      return response;
    } finally {
      loading.wechatTemplates = false;
    }
  }

  async function bindWeChat(role: 'agent' | 'author', jsCode: string, nickname?: string | null) {
    if (!jsCode) {
      throw new Error('未获取到 jsCode');
    }

    loading.wechatBind = true;
    try {
      const trimmedNickname = typeof nickname === 'string' ? nickname.trim() : undefined;
      await apiRequest<WeChatBindResult>({
        url: '/api/wechat/bind',
        method: 'POST',
        auth: true,
        authRole: role,
        skipProxy: true,
        data: { jsCode, nickname: trimmedNickname }
      });

      await fetchWeChatBinding(role);

      return role === 'agent' ? agentWeChatBinding.value : authorWeChatBinding.value;
    } finally {
      loading.wechatBind = false;
    }
  }

  async function unbindWeChat(role: 'agent' | 'author') {
    loading.wechatUnbind = true;
    try {
      await apiRequest<string>({
        url: '/api/wechat/bind',
        method: 'DELETE',
        auth: true,
        authRole: role,
        skipProxy: true
      });

      if (role === 'agent') {
        agentWeChatBinding.value = null;
        refreshWechatSubscriptions('agent');
      } else {
        authorWeChatBinding.value = null;
        refreshWechatSubscriptions('author');
      }
    } finally {
      loading.wechatUnbind = false;
    }
  }

  function setAgentWechatSubscription(key: string, value: boolean) {
    const binding = agentWeChatBinding.value;
    if (!binding?.openId) {
      return;
    }
    agentWechatSubscriptions.value = {
      ...agentWechatSubscriptions.value,
      [key]: value
    };
    setWeChatSubscriptions(binding.openId, agentWechatSubscriptions.value);
  }

  function setAuthorWechatSubscription(key: string, value: boolean) {
    const binding = authorWeChatBinding.value;
    if (!binding?.openId) {
      return;
    }
    authorWechatSubscriptions.value = {
      ...authorWechatSubscriptions.value,
      [key]: value
    };
    setWeChatSubscriptions(binding.openId, authorWechatSubscriptions.value);
  }

  async function sendWeChatPreview(role: 'agent' | 'author', templateKey: string, silent = false) {
    const payload = wechatTemplatePreviews.value?.[templateKey];
    if (!payload || Object.keys(payload).length === 0) {
      if (!silent) {
        uni.showToast({ title: '未配置该提醒的测试内容', icon: 'none' });
      }
      return false;
    }

    try {
      const result = await apiRequest<WeChatNotificationResult>({
        url: '/api/wechat/notify',
        method: 'POST',
        auth: true,
        authRole: role,
        skipProxy: true,
        data: {
          template: templateKey,
          data: payload
        }
      });

      if (!result?.success) {
        if (!silent) {
          const message = result?.errorMessage?.trim() || `模板 ${templateKey} 推送失败`;
          uni.showToast({ title: message, icon: 'none' });
        }
        return false;
      }

      if (!silent) {
        uni.showToast({ title: '已发送测试提醒', icon: 'success' });
      }

      return true;
    } catch (error) {
      if (!silent) {
        const message = (error as any)?.errMsg || (error as any)?.message || '推送失败，请稍后重试';
        uni.showToast({ title: message, icon: 'none' });
      }
      return false;
    }
  }

  function logout() {
    setSession(null);
    agentWechatSubscriptions.value = {};
  }

  function logoutAuthor() {
    setAuthorSession(null);
    authorWechatSubscriptions.value = {};
  }

  const isAuthenticated = computed(() => Boolean(token.value));
  const isAuthorAuthenticated = computed(() => Boolean(authorToken.value));

  return {
    agent,
    token,
    expiresAt,
    bindings,
    selectedBinding,
    authorProfile,
    authorToken,
    authorExpiresAt,
    selectedAuthorSoftwareId,
    agentWeChatBinding,
    authorWeChatBinding,
    agentWechatSubscriptions,
    authorWechatSubscriptions,
    loading,
    wechatTemplates,
    wechatTemplatePreviews,
    isAuthenticated,
    isAuthorAuthenticated,
    registerAgent,
    loginAgent,
    loginAgentWithWeChat,
    fetchAgentProfile,
    createBinding,
    updateBinding,
    deleteBinding,
    registerAuthor,
    loginAuthor,
    fetchAuthorProfile,
    updateAuthorProfile,
    regenerateAuthorCode,
    createAuthorSoftware,
    updateAuthorSoftware,
    deleteAuthorSoftware,
    deleteAuthorAccount,
    selectBinding,
    selectAuthorSoftware,
    fetchWeChatBinding,
    fetchWeChatTemplates,
    bindWeChat,
    unbindWeChat,
    refreshWechatSubscriptions,
    setAgentWechatSubscription,
    setAuthorWechatSubscription,
    sendWeChatPreview,
    logout,
    logoutAuthor,
    setAuthorSession
  };
});
