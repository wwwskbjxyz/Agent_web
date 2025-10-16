"use strict";
const common_vendor = require("../common/vendor.js");
const common_api = require("../common/api.js");
const utils_storage = require("../utils/storage.js");
const usePlatformStore = common_vendor.defineStore("platform", () => {
  const agent = common_vendor.ref(null);
  const token = common_vendor.ref(utils_storage.getPlatformToken());
  const expiresAt = common_vendor.ref(null);
  const bindings = common_vendor.ref([]);
  const selectedBinding = common_vendor.ref(null);
  const authorProfile = common_vendor.ref(null);
  const authorToken = common_vendor.ref(utils_storage.getAuthorToken());
  const authorExpiresAt = common_vendor.ref(null);
  const selectedAuthorSoftwareId = common_vendor.ref(null);
  const agentWeChatBinding = common_vendor.ref(null);
  const authorWeChatBinding = common_vendor.ref(null);
  const agentWechatSubscriptions = common_vendor.ref({});
  const authorWechatSubscriptions = common_vendor.ref({});
  const wechatTemplates = common_vendor.ref(null);
  const wechatTemplatePreviews = common_vendor.ref({});
  const wechatTemplatesLoaded = common_vendor.ref(false);
  const loading = common_vendor.reactive({
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
  function refreshWechatSubscriptions(role) {
    const binding = role === "agent" ? agentWeChatBinding.value : authorWeChatBinding.value;
    const target = role === "agent" ? agentWechatSubscriptions : authorWechatSubscriptions;
    if (binding == null ? void 0 : binding.openId) {
      target.value = utils_storage.getWeChatSubscriptions(binding.openId);
    } else {
      target.value = {};
    }
  }
  common_vendor.watch(
    () => {
      var _a;
      return ((_a = agentWeChatBinding.value) == null ? void 0 : _a.openId) ?? null;
    },
    () => refreshWechatSubscriptions("agent")
  );
  common_vendor.watch(
    () => {
      var _a;
      return ((_a = authorWeChatBinding.value) == null ? void 0 : _a.openId) ?? null;
    },
    () => refreshWechatSubscriptions("author")
  );
  function setSession(session) {
    if (!session) {
      agent.value = null;
      token.value = null;
      expiresAt.value = null;
      bindings.value = [];
      selectedBinding.value = null;
      agentWeChatBinding.value = null;
      utils_storage.clearPlatformToken();
      utils_storage.clearToken();
      utils_storage.clearSelectedSoftwareName();
      return;
    }
    agent.value = session.agent;
    token.value = session.token || token.value;
    expiresAt.value = session.expiresAt ?? null;
    bindings.value = session.bindings ?? [];
    if (session.token) {
      utils_storage.setPlatformToken(session.token);
    }
    const storedCode = utils_storage.getSelectedSoftwareName();
    const preferred = bindings.value.find((item) => item.softwareCode === storedCode);
    selectBinding(preferred || bindings.value[0] || null);
  }
  function selectBinding(binding) {
    selectedBinding.value = binding;
    if (!binding) {
      utils_storage.clearSelectedSoftwareName();
      return;
    }
    utils_storage.setSelectedSoftwareName(binding.softwareCode);
  }
  function setAuthorProfile(profile) {
    var _a;
    authorProfile.value = profile;
    if (!profile) {
      selectedAuthorSoftwareId.value = null;
      return;
    }
    const available = ((_a = profile.softwares) == null ? void 0 : _a.map((item) => item.softwareId)) ?? [];
    const preferred = profile.primarySoftwareId ?? selectedAuthorSoftwareId.value ?? null;
    const resolved = preferred && available.includes(preferred) ? preferred : available[0] ?? null;
    selectedAuthorSoftwareId.value = resolved ?? null;
  }
  function selectAuthorSoftware(softwareId) {
    selectedAuthorSoftwareId.value = softwareId ?? null;
  }
  function setAuthorSession(session) {
    if (!session) {
      setAuthorProfile(null);
      authorToken.value = null;
      authorExpiresAt.value = null;
      authorWeChatBinding.value = null;
      utils_storage.clearAuthorToken();
      return;
    }
    setAuthorProfile(session.profile);
    authorToken.value = session.token;
    authorExpiresAt.value = session.expiresAt;
    utils_storage.setAuthorToken(session.token);
  }
  async function registerAgent(payload) {
    loading.registerAgent = true;
    try {
      await common_api.apiRequest({
        url: "/api/agents/register",
        method: "POST",
        data: payload
      });
      return true;
    } finally {
      loading.registerAgent = false;
    }
  }
  async function loginAgent(payload) {
    loading.loginAgent = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/agents/login",
        method: "POST",
        data: payload
      });
      setSession(response);
      await fetchWeChatBinding("agent").catch(() => void 0);
      await fetchWeChatTemplates().catch(() => void 0);
      return response;
    } finally {
      loading.loginAgent = false;
    }
  }
  async function loginAgentWithWeChat(jsCode) {
    if (!jsCode) {
      throw new Error("未获取到 jsCode");
    }
    loading.loginAgentWeChat = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/agents/login/wechat",
        method: "POST",
        skipProxy: true,
        data: { jsCode }
      });
      setSession(response);
      await fetchWeChatBinding("agent").catch(() => void 0);
      await fetchWeChatTemplates().catch(() => void 0);
      return response;
    } finally {
      loading.loginAgentWeChat = false;
    }
  }
  async function fetchAgentProfile() {
    loading.bindings = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/agents/me",
        method: "GET",
        auth: true
      });
      setSession({
        ...response,
        token: token.value || utils_storage.getPlatformToken() || ""
      });
      await fetchWeChatBinding("agent").catch(() => void 0);
      await fetchWeChatTemplates().catch(() => void 0);
      return response;
    } finally {
      loading.bindings = false;
    }
  }
  async function createBinding(payload) {
    loading.createBinding = true;
    try {
      const result = await common_api.apiRequest({
        url: "/api/bindings",
        method: "POST",
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
  async function updateBinding(bindingId, payload) {
    var _a;
    loading.updateBinding = true;
    try {
      const result = await common_api.apiRequest({
        url: `/api/bindings/${bindingId}`,
        method: "PUT",
        auth: true,
        data: {
          authorAccount: payload.authorAccount,
          authorPassword: payload.authorPassword
        }
      });
      bindings.value = bindings.value.map((item) => item.bindingId === bindingId ? result : item);
      if (((_a = selectedBinding.value) == null ? void 0 : _a.bindingId) === bindingId) {
        selectBinding(result);
      }
      return result;
    } finally {
      loading.updateBinding = false;
    }
  }
  async function deleteBinding(bindingId) {
    var _a;
    loading.deleteBinding = true;
    try {
      await common_api.apiRequest({
        url: `/api/bindings/${bindingId}`,
        method: "DELETE",
        auth: true
      });
      bindings.value = bindings.value.filter((item) => item.bindingId !== bindingId);
      if (((_a = selectedBinding.value) == null ? void 0 : _a.bindingId) === bindingId) {
        selectBinding(bindings.value[0] || null);
      }
    } finally {
      loading.deleteBinding = false;
    }
  }
  async function registerAuthor(payload) {
    loading.registerAuthor = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/authors/register",
        method: "POST",
        data: payload
      });
      return response;
    } finally {
      loading.registerAuthor = false;
    }
  }
  async function loginAuthor(payload) {
    loading.loginAuthor = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/authors/login",
        method: "POST",
        data: payload
      });
      setAuthorSession(response);
      await fetchWeChatBinding("author").catch(() => void 0);
      return response.profile;
    } finally {
      loading.loginAuthor = false;
    }
  }
  async function fetchAuthorProfile() {
    loading.authorProfile = true;
    try {
      const profile = await common_api.apiRequest({
        url: "/api/authors/me",
        method: "GET",
        auth: true,
        authRole: "author"
      });
      setAuthorProfile(profile);
      await fetchWeChatBinding("author").catch(() => void 0);
      return profile;
    } finally {
      loading.authorProfile = false;
    }
  }
  async function updateAuthorProfile(payload) {
    loading.updateAuthor = true;
    try {
      const profile = await common_api.apiRequest({
        url: "/api/authors/me",
        method: "PUT",
        auth: true,
        authRole: "author",
        data: payload
      });
      setAuthorProfile(profile);
      return profile;
    } finally {
      loading.updateAuthor = false;
    }
  }
  async function regenerateAuthorCode(softwareId) {
    loading.regenerateAuthorCode = true;
    try {
      const response = await common_api.apiRequest({
        url: softwareId ? `/api/authors/me/regenerate-code?softwareId=${softwareId}` : "/api/authors/me/regenerate-code",
        method: "POST",
        auth: true,
        authRole: "author"
      });
      return response.softwareCode;
    } finally {
      loading.regenerateAuthorCode = false;
    }
  }
  async function createAuthorSoftware(payload) {
    loading.createAuthorSoftware = true;
    try {
      const software = await common_api.apiRequest({
        url: "/api/authors/me/softwares",
        method: "POST",
        auth: true,
        authRole: "author",
        data: payload
      });
      await fetchAuthorProfile();
      selectedAuthorSoftwareId.value = software.softwareId;
      return software;
    } finally {
      loading.createAuthorSoftware = false;
    }
  }
  async function updateAuthorSoftware(softwareId, payload) {
    loading.updateAuthorSoftware = true;
    try {
      await common_api.apiRequest({
        url: `/api/authors/me/softwares/${softwareId}`,
        method: "PUT",
        auth: true,
        authRole: "author",
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
  async function deleteAuthorSoftware(softwareId) {
    var _a;
    loading.deleteAuthorSoftware = true;
    try {
      await common_api.apiRequest({
        url: `/api/authors/me/softwares/${softwareId}`,
        method: "DELETE",
        auth: true,
        authRole: "author"
      });
      const profile = await fetchAuthorProfile();
      if (profile) {
        const exists = profile.softwares.some((item) => item.softwareId === selectedAuthorSoftwareId.value);
        if (!exists) {
          selectedAuthorSoftwareId.value = profile.primarySoftwareId ?? ((_a = profile.softwares[0]) == null ? void 0 : _a.softwareId) ?? null;
        }
      }
    } finally {
      loading.deleteAuthorSoftware = false;
    }
  }
  async function deleteAuthorAccount() {
    loading.deleteAuthor = true;
    try {
      await common_api.apiRequest({
        url: "/api/authors/me",
        method: "DELETE",
        auth: true,
        authRole: "author"
      });
      setAuthorSession(null);
    } finally {
      loading.deleteAuthor = false;
    }
  }
  async function fetchWeChatBinding(role = "agent") {
    const hasToken = role === "agent" ? Boolean(token.value) : Boolean(authorToken.value);
    if (!hasToken) {
      if (role === "agent") {
        agentWeChatBinding.value = null;
      } else {
        authorWeChatBinding.value = null;
      }
      return null;
    }
    loading.wechatBinding = true;
    try {
      const response = await common_api.apiRequest({
        url: "/api/wechat/binding",
        method: "GET",
        auth: true,
        authRole: role,
        skipProxy: true
      });
      if (role === "agent") {
        agentWeChatBinding.value = response ?? null;
        refreshWechatSubscriptions("agent");
      } else {
        authorWeChatBinding.value = response ?? null;
        refreshWechatSubscriptions("author");
      }
      if (role === "agent") {
        await fetchWeChatTemplates().catch(() => void 0);
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
      const response = await common_api.apiRequest({
        url: "/api/wechat/templates",
        method: "GET",
        skipProxy: true
      });
      wechatTemplates.value = response;
      wechatTemplatePreviews.value = (response == null ? void 0 : response.previewData) ?? {};
      wechatTemplatesLoaded.value = true;
      return response;
    } finally {
      loading.wechatTemplates = false;
    }
  }
  async function bindWeChat(role, jsCode, nickname) {
    if (!jsCode) {
      throw new Error("未获取到 jsCode");
    }
    loading.wechatBind = true;
    try {
      const trimmedNickname = typeof nickname === "string" ? nickname.trim() : void 0;
      await common_api.apiRequest({
        url: "/api/wechat/bind",
        method: "POST",
        auth: true,
        authRole: role,
        skipProxy: true,
        data: { jsCode, nickname: trimmedNickname }
      });
      await fetchWeChatBinding(role);
      return role === "agent" ? agentWeChatBinding.value : authorWeChatBinding.value;
    } finally {
      loading.wechatBind = false;
    }
  }
  async function unbindWeChat(role) {
    loading.wechatUnbind = true;
    try {
      await common_api.apiRequest({
        url: "/api/wechat/bind",
        method: "DELETE",
        auth: true,
        authRole: role,
        skipProxy: true
      });
      if (role === "agent") {
        agentWeChatBinding.value = null;
        refreshWechatSubscriptions("agent");
      } else {
        authorWeChatBinding.value = null;
        refreshWechatSubscriptions("author");
      }
    } finally {
      loading.wechatUnbind = false;
    }
  }
  function setAgentWechatSubscription(key, value) {
    const binding = agentWeChatBinding.value;
    if (!(binding == null ? void 0 : binding.openId)) {
      return;
    }
    agentWechatSubscriptions.value = {
      ...agentWechatSubscriptions.value,
      [key]: value
    };
    utils_storage.setWeChatSubscriptions(binding.openId, agentWechatSubscriptions.value);
  }
  function setAuthorWechatSubscription(key, value) {
    const binding = authorWeChatBinding.value;
    if (!(binding == null ? void 0 : binding.openId)) {
      return;
    }
    authorWechatSubscriptions.value = {
      ...authorWechatSubscriptions.value,
      [key]: value
    };
    utils_storage.setWeChatSubscriptions(binding.openId, authorWechatSubscriptions.value);
  }
  async function sendWeChatPreview(role, templateKey, silent = false) {
    var _a, _b;
    const payload = (_a = wechatTemplatePreviews.value) == null ? void 0 : _a[templateKey];
    if (!payload || Object.keys(payload).length === 0) {
      if (!silent) {
        common_vendor.index.showToast({ title: "未配置该提醒的测试内容", icon: "none" });
      }
      return false;
    }
    try {
      const result = await common_api.apiRequest({
        url: "/api/wechat/notify",
        method: "POST",
        auth: true,
        authRole: role,
        skipProxy: true,
        data: {
          template: templateKey,
          data: payload
        }
      });
      if (!(result == null ? void 0 : result.success)) {
        if (!silent) {
          const message = ((_b = result == null ? void 0 : result.errorMessage) == null ? void 0 : _b.trim()) || `模板 ${templateKey} 推送失败`;
          common_vendor.index.showToast({ title: message, icon: "none" });
        }
        return false;
      }
      if (!silent) {
        common_vendor.index.showToast({ title: "已发送测试提醒", icon: "success" });
      }
      return true;
    } catch (error) {
      if (!silent) {
        const message = (error == null ? void 0 : error.errMsg) || (error == null ? void 0 : error.message) || "推送失败，请稍后重试";
        common_vendor.index.showToast({ title: message, icon: "none" });
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
  const isAuthenticated = common_vendor.computed(() => Boolean(token.value));
  const isAuthorAuthenticated = common_vendor.computed(() => Boolean(authorToken.value));
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
exports.usePlatformStore = usePlatformStore;
//# sourceMappingURL=../../.sourcemap/mp-weixin/stores/platform.js.map
