"use strict";
const common_vendor = require("../../common/vendor.js");
const common_api = require("../../common/api.js");
const stores_app = require("../../stores/app.js");
const stores_platform = require("../../stores/platform.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "index",
  setup(__props) {
    const appStore = stores_app.useAppStore();
    const platformStore = stores_platform.usePlatformStore();
    const { verification } = common_vendor.storeToRefs(appStore);
    const { selectedSoftware, softwareList } = common_vendor.storeToRefs(appStore);
    const { bindings, selectedBinding } = common_vendor.storeToRefs(platformStore);
    const systemInfo = typeof common_vendor.index !== "undefined" && typeof common_vendor.index.getSystemInfoSync === "function" ? common_vendor.index.getSystemInfoSync() : null;
    const uniPlatform = ((systemInfo == null ? void 0 : systemInfo.uniPlatform) || "").toLowerCase();
    const isMiniProgram = uniPlatform.startsWith("mp");
    const isH5 = typeof window !== "undefined" && typeof document !== "undefined";
    const code = common_vendor.ref("");
    const shareQuery = common_vendor.ref("");
    const shareOrigin = common_vendor.ref(null);
    const linkContext = common_vendor.ref(null);
    const shareContextError = common_vendor.ref("");
    const generatorBindingId = common_vendor.ref(null);
    const generatorSoftware = common_vendor.ref("");
    const defaultGateway = common_vendor.ref("");
    const payload = common_vendor.computed(() => verification.value);
    const loading = common_vendor.computed(() => appStore.loading.verification);
    const isGeneratorMode = common_vendor.computed(() => !linkContext.value);
    const canVerify = common_vendor.computed(() => {
      const ctx = activeContext.value;
      return !!ctx && !!ctx.softwareCode && !shareContextError.value;
    });
    const bindingOptions = common_vendor.computed(
      () => bindings.value.map((item) => ({
        value: item.bindingId,
        label: `${item.authorDisplayName} (${item.softwareCode})`
      }))
    );
    const softwareOptions = common_vendor.computed(
      () => softwareList.value.map((item) => ({
        value: item.softwareName,
        label: item.softwareName
      }))
    );
    const shareLabel = common_vendor.computed(() => isH5 ? "分享链接" : "分享路径");
    const shareNavigatePath = common_vendor.computed(() => {
      if (!shareQuery.value) {
        return "";
      }
      return `/pages/verify/share?${shareQuery.value}`;
    });
    const shareHref = common_vendor.computed(() => {
      if (!shareQuery.value) {
        return "";
      }
      if (isH5 && typeof window !== "undefined" && window.location) {
        return `${window.location.origin}/#/pages/verify/share?${shareQuery.value}`;
      }
      return shareNavigatePath.value;
    });
    const shareDisplayValue = common_vendor.computed(() => isH5 ? shareHref.value : shareNavigatePath.value);
    const shareOriginIsGenerated = common_vendor.computed(() => shareOrigin.value === "generated");
    const hasShareInfo = common_vendor.computed(() => !!shareQuery.value);
    const showShareInfo = common_vendor.computed(() => shareOriginIsGenerated.value && hasShareInfo.value);
    const generatorContext = common_vendor.computed(() => {
      var _a, _b, _c;
      if (!isGeneratorMode.value) {
        return null;
      }
      const softwareName = (generatorSoftware.value || selectedSoftware.value || "").trim();
      if (!softwareName) {
        return null;
      }
      const binding = bindings.value.find((item) => item.bindingId === generatorBindingId.value) || selectedBinding.value;
      const softwareCodeValue = (_a = binding == null ? void 0 : binding.softwareCode) == null ? void 0 : _a.trim();
      const agentAccountValue = (_c = (_b = platformStore.agent) == null ? void 0 : _b.username) == null ? void 0 : _c.trim();
      return {
        software: softwareName,
        softwareCode: softwareCodeValue || void 0,
        agentAccount: agentAccountValue || void 0,
        displayName: softwareName
      };
    });
    const activeContext = common_vendor.computed(() => linkContext.value ?? generatorContext.value);
    const contextLabel = common_vendor.computed(
      () => {
        var _a, _b;
        return ((_a = activeContext.value) == null ? void 0 : _a.displayName) || ((_b = activeContext.value) == null ? void 0 : _b.software) || "未选择";
      }
    );
    const contextCodeLabel = common_vendor.computed(() => {
      var _a;
      return ((_a = activeContext.value) == null ? void 0 : _a.softwareCode) || "";
    });
    const contextAgentLabel = common_vendor.computed(
      () => {
        var _a, _b;
        return ((_a = activeContext.value) == null ? void 0 : _a.agentDisplayName) || ((_b = activeContext.value) == null ? void 0 : _b.agentAccount) || "";
      }
    );
    const currentBindingLabel = common_vendor.computed(() => {
      if (!isGeneratorMode.value) {
        return contextCodeLabel.value || "未提供绑定信息";
      }
      const option = bindingOptions.value.find((item) => item.value === generatorBindingId.value);
      return (option == null ? void 0 : option.label) || "请选择绑定软件码";
    });
    const currentSoftwareLabel = common_vendor.computed(() => {
      if (!isGeneratorMode.value) {
        return contextLabel.value;
      }
      const option = softwareOptions.value.find((item) => item.value === generatorSoftware.value);
      return (option == null ? void 0 : option.label) || "请选择软件位";
    });
    common_vendor.watch(
      bindings,
      (list) => {
        if (!isGeneratorMode.value) {
          return;
        }
        if (list.length > 0 && generatorBindingId.value == null) {
          generatorBindingId.value = list[0].bindingId;
        }
      },
      { immediate: true }
    );
    common_vendor.watch(
      selectedBinding,
      (binding) => {
        if (!isGeneratorMode.value || !binding) {
          return;
        }
        if (generatorBindingId.value == null) {
          generatorBindingId.value = binding.bindingId;
        }
      },
      { immediate: true }
    );
    common_vendor.watch(
      selectedSoftware,
      (software) => {
        if (!isGeneratorMode.value) {
          return;
        }
        if (!generatorSoftware.value && software) {
          generatorSoftware.value = software;
        }
      },
      { immediate: true }
    );
    common_vendor.watch(
      bindingOptions,
      (options) => {
        if (!isGeneratorMode.value) {
          return;
        }
        if (!options.length) {
          generatorBindingId.value = null;
          return;
        }
        if (!options.some((item) => item.value === generatorBindingId.value)) {
          generatorBindingId.value = options[0].value;
        }
      },
      { immediate: true }
    );
    common_vendor.watch(
      softwareOptions,
      (options) => {
        if (!isGeneratorMode.value) {
          return;
        }
        if (!options.length) {
          generatorSoftware.value = "";
          return;
        }
        if (!options.some((item) => item.value === generatorSoftware.value)) {
          generatorSoftware.value = options[0].value;
        }
      },
      { immediate: true }
    );
    common_vendor.watch([generatorBindingId, generatorSoftware], () => {
      if (shareOrigin.value === "generated") {
        shareQuery.value = "";
        shareOrigin.value = null;
      }
    });
    function handleBindingChange(event) {
      var _a;
      const index = Number(((_a = event == null ? void 0 : event.detail) == null ? void 0 : _a.value) ?? 0);
      const option = bindingOptions.value[index];
      generatorBindingId.value = (option == null ? void 0 : option.value) ?? null;
    }
    function handleSoftwareChange(event) {
      var _a;
      const index = Number(((_a = event == null ? void 0 : event.detail) == null ? void 0 : _a.value) ?? 0);
      const option = softwareOptions.value[index];
      generatorSoftware.value = (option == null ? void 0 : option.value) ?? "";
    }
    function handleVerify() {
      if (!code.value.trim()) {
        common_vendor.index.showToast({ title: "请输入卡密", icon: "none" });
        return;
      }
      if (!canVerify.value) {
        const message = shareContextError.value || "未选择软件位，无法验证";
        common_vendor.index.showToast({ title: message, icon: "none" });
        return;
      }
      const ctx = activeContext.value;
      if (!ctx) {
        common_vendor.index.showToast({ title: "未选择软件位，无法验证", icon: "none" });
        return;
      }
      appStore.loadVerification(code.value.trim(), ctx);
    }
    function copyUrl(url) {
      if (!url) {
        common_vendor.index.showToast({ title: "暂无链接可复制", icon: "none" });
        return;
      }
      common_vendor.index.setClipboardData({
        data: url,
        success: () => {
          common_vendor.index.showToast({ title: "链接已复制", icon: "success" });
        }
      });
    }
    function copyShareLink() {
      if (!showShareInfo.value || !shareDisplayValue.value) {
        common_vendor.index.showToast({ title: "暂无分享信息", icon: "none" });
        return;
      }
      copyUrl(shareDisplayValue.value);
    }
    function openSharePage() {
      if (!shareNavigatePath.value) {
        common_vendor.index.showToast({ title: "暂无分享路径", icon: "none" });
        return;
      }
      common_vendor.index.navigateTo({ url: shareNavigatePath.value });
    }
    function generateShareLink() {
      const ctx = generatorContext.value;
      if (!ctx) {
        common_vendor.index.showToast({ title: "请先选择绑定和软件位", icon: "none" });
        return;
      }
      const binding = bindings.value.find((item) => item.bindingId === generatorBindingId.value) || selectedBinding.value;
      if (!ctx.softwareCode) {
        const codeValue = (binding == null ? void 0 : binding.softwareCode) ? `（${binding.softwareCode}）` : "";
        common_vendor.index.showToast({ title: `绑定缺少软件码${codeValue}`, icon: "none" });
        return;
      }
      shareQuery.value = buildShareQueryString(ctx);
      shareOrigin.value = "generated";
      shareContextError.value = "";
      common_vendor.index.showToast({ title: isH5 ? "已生成分享链接" : "已生成分享路径", icon: "success" });
    }
    function buildShareCandidate(source) {
      const candidate = {};
      let detected = false;
      if (!source) {
        return { detected: false, candidate };
      }
      const readValue = (...keys) => {
        for (const key of keys) {
          const direct = extractValue(key);
          if (direct) {
            return direct;
          }
        }
        return "";
      };
      const extractValue = (key) => {
        const raw = source[key];
        if (typeof raw === "string" && raw.trim()) {
          return decodeURIComponentSafe(raw);
        }
        const lower = key.toLowerCase();
        if (lower !== key) {
          const fallback = source[lower];
          if (typeof fallback === "string" && fallback.trim()) {
            return decodeURIComponentSafe(fallback);
          }
        }
        return "";
      };
      const softwareValue = normalizeParam(readValue("software", "s", "softwareName", "slot"));
      if (softwareValue) {
        candidate.software = softwareValue;
        detected = true;
      }
      const displayValue = normalizeParam(
        readValue("display", "softwareDisplay", "softwareDisplayName", "name")
      );
      if (displayValue) {
        candidate.softwareDisplayName = displayValue;
        detected = true;
      }
      const codeValue = normalizeParam(readValue("code", "c", "softwareCode", "binding"));
      if (codeValue) {
        candidate.softwareCode = codeValue;
        detected = true;
      }
      const agentValue = normalizeParam(readValue("agent", "a", "agentAccount"));
      if (agentValue) {
        candidate.agentAccount = agentValue;
        detected = true;
      }
      const agentDisplayValue = normalizeParam(
        readValue("agentName", "agentDisplay", "agentDisplayName")
      );
      if (agentDisplayValue) {
        candidate.agentDisplayName = agentDisplayValue;
        detected = true;
      }
      const gatewayRaw = normalizeParam(readValue("gateway", "api", "gw"));
      if (gatewayRaw) {
        detected = true;
        const normalizedGateway = resolveGateway(gatewayRaw);
        if (normalizedGateway) {
          candidate.gateway = normalizedGateway;
        }
      }
      const slugValue = normalizeParam(readValue("share", "context", "slug", "token"));
      if (slugValue) {
        candidate.slug = slugValue;
        detected = true;
      }
      return { detected, candidate };
    }
    async function detectShareCandidateFromRoute() {
      var _a;
      if (typeof getCurrentPages !== "function") {
        return { detected: false, candidate: {} };
      }
      const pages = getCurrentPages();
      if (!Array.isArray(pages) || !pages.length) {
        return { detected: false, candidate: {} };
      }
      const current = pages[pages.length - 1];
      const fullPath = ((_a = current == null ? void 0 : current.$page) == null ? void 0 : _a.fullPath) || (current == null ? void 0 : current.route) || "";
      if (!fullPath) {
        return { detected: false, candidate: {} };
      }
      const queryIndex = fullPath.indexOf("?");
      if (queryIndex >= 0) {
        const queryString = fullPath.slice(queryIndex + 1);
        const map = parseQuery(queryString);
        return buildShareCandidate(map);
      }
      const segments = fullPath.split("/").filter(Boolean);
      if (segments.length > 2) {
        const slug = decodeURIComponentSafe(segments[segments.length - 1]);
        if (slug) {
          return buildShareCandidate({ share: slug });
        }
      }
      return { detected: false, candidate: {} };
    }
    async function resolveContextFromCandidate(candidate) {
      const slugContext = candidate.slug ? decodeLegacySlug(candidate.slug) : null;
      const gatewayValue = resolveGateway(candidate.gateway) || resolveGateway(slugContext == null ? void 0 : slugContext.gateway);
      const softwareValue = normalizeParam(candidate.software) || normalizeParam(slugContext == null ? void 0 : slugContext.software);
      const codeValue = normalizeParam(candidate.softwareCode) || normalizeParam(slugContext == null ? void 0 : slugContext.softwareCode);
      const agentAccountValue = normalizeParam(candidate.agentAccount) || normalizeParam(slugContext == null ? void 0 : slugContext.agentAccount);
      const displayValue = normalizeParam(candidate.softwareDisplayName);
      const agentDisplayValue = normalizeParam(candidate.agentDisplayName);
      if (softwareValue && codeValue) {
        return {
          software: softwareValue,
          softwareCode: codeValue,
          agentAccount: agentAccountValue || void 0,
          displayName: displayValue || softwareValue,
          agentDisplayName: agentDisplayValue || void 0,
          gateway: gatewayValue || void 0
        };
      }
      if (!codeValue) {
        return null;
      }
      const remote = await fetchShareContextByCode(codeValue, gatewayValue || void 0);
      if (!remote) {
        return null;
      }
      const remoteSoftware = normalizeParam(remote.software);
      const remoteCode = normalizeParam(remote.softwareCode) || codeValue;
      if (!remoteSoftware) {
        return null;
      }
      return {
        software: remoteSoftware,
        softwareCode: remoteCode,
        agentAccount: agentAccountValue || normalizeParam(remote.agentAccount) || void 0,
        displayName: displayValue || normalizeParam(remote.softwareDisplayName) || remoteSoftware,
        agentDisplayName: agentDisplayValue || normalizeParam(remote.agentDisplayName) || void 0,
        gateway: gatewayValue || void 0
      };
    }
    async function fetchShareContextByCode(code2, gateway) {
      const normalized = normalizeParam(code2);
      if (!normalized) {
        return null;
      }
      const endpoint = gateway ? `${gateway}/api/card-verification/context` : "/api/card-verification/context";
      try {
        const payload2 = await common_api.apiRequest({
          url: endpoint,
          method: "GET",
          data: { softwareCode: normalized },
          skipProxy: true,
          auth: false
        });
        if (!payload2) {
          return null;
        }
        return payload2;
      } catch (error) {
        common_vendor.index.__f__("warn", "at pages/verify/index.vue:639", "Failed to load verification context", error);
        return null;
      }
    }
    function applyLinkContext(context) {
      const sanitizedGateway = resolveGateway(context.gateway);
      if (sanitizedGateway) {
        defaultGateway.value = sanitizedGateway;
        common_api.setBaseURL(sanitizedGateway);
      }
      const nextContext = {
        software: context.software,
        softwareCode: context.softwareCode,
        agentAccount: context.agentAccount,
        displayName: context.displayName || context.software,
        agentDisplayName: context.agentDisplayName || context.agentAccount,
        gateway: sanitizedGateway || void 0
      };
      linkContext.value = nextContext;
      shareQuery.value = buildShareQueryString(nextContext);
      verification.value = null;
      shareContextError.value = "";
      code.value = "";
      shareOrigin.value = "link";
    }
    function parseQuery(input) {
      const map = {};
      if (!input) {
        return map;
      }
      input.split("&").forEach((segment) => {
        if (!segment) {
          return;
        }
        const [rawKey, rawValue = ""] = segment.split("=");
        const key = (rawKey || "").trim();
        if (!key) {
          return;
        }
        const decoded = decodeURIComponentSafe(rawValue);
        map[key] = decoded;
        const lower = key.toLowerCase();
        if (lower !== key && map[lower] == null) {
          map[lower] = decoded;
        }
      });
      return map;
    }
    function decodeURIComponentSafe(value) {
      if (typeof value !== "string") {
        return "";
      }
      try {
        return decodeURIComponent(value);
      } catch (error) {
        return value;
      }
    }
    function normalizeParam(value) {
      if (typeof value !== "string") {
        return "";
      }
      return value.trim();
    }
    function buildShareQueryString(context) {
      const parts = [`software=${encodeURIComponent(context.software)}`];
      if (context.softwareCode) {
        parts.push(`code=${encodeURIComponent(context.softwareCode)}`);
      }
      if (context.agentAccount) {
        parts.push(`agent=${encodeURIComponent(context.agentAccount)}`);
      }
      const gateway = context.gateway || defaultGateway.value;
      const normalizedGateway = resolveGateway(gateway);
      if (normalizedGateway) {
        parts.push(`gateway=${encodeURIComponent(normalizedGateway)}`);
      }
      return parts.join("&");
    }
    function resolveGateway(raw) {
      if (!raw) {
        return "";
      }
      const trimmed = raw.trim();
      if (!trimmed) {
        return "";
      }
      const lower = trimmed.toLowerCase();
      if (!lower.startsWith("http://") && !lower.startsWith("https://")) {
        return "";
      }
      return trimmed.replace(/\/+$/, "");
    }
    function decodeLegacySlug(slug) {
      if (!slug) {
        return null;
      }
      try {
        let base64 = slug.replace(/-/g, "+").replace(/_/g, "/");
        const padding = base64.length % 4;
        if (padding) {
          base64 = base64.padEnd(base64.length + (4 - padding), "=");
        }
        let buffer;
        if (typeof common_vendor.index !== "undefined" && typeof common_vendor.index.base64ToArrayBuffer === "function") {
          buffer = common_vendor.index.base64ToArrayBuffer(base64);
        } else if (typeof atob === "function") {
          const binary = atob(base64);
          const bytes = new Uint8Array(binary.length);
          for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
          }
          buffer = bytes.buffer;
        } else if (typeof Buffer !== "undefined") {
          const buf = Buffer.from(base64, "base64");
          buffer = buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength);
        } else {
          return null;
        }
        const json = typeof TextDecoder !== "undefined" ? new TextDecoder().decode(new Uint8Array(buffer)) : utf8Decode(new Uint8Array(buffer));
        const raw = JSON.parse(json) ?? {};
        const software = normalizeParam(raw.s || raw.software || raw.softwareName || raw.slot);
        const softwareCode = normalizeParam(raw.c || raw.code || raw.softwareCode || raw.binding);
        const agentAccount = normalizeParam(raw.a || raw.agent || raw.agentAccount);
        const gateway = resolveGateway(normalizeParam(raw.g || raw.gateway));
        if (!software || !softwareCode) {
          return null;
        }
        return {
          software,
          softwareCode,
          agentAccount: agentAccount || void 0,
          gateway: gateway || void 0
        };
      } catch (error) {
        common_vendor.index.__f__("warn", "at pages/verify/index.vue:789", "Failed to decode legacy share slug", error);
        return null;
      }
    }
    common_vendor.onLoad(async (options) => {
      shareOrigin.value = null;
      shareQuery.value = "";
      shareContextError.value = "";
      linkContext.value = null;
      const primaryDetection = buildShareCandidate(options);
      let detection = primaryDetection;
      if (!detection.detected) {
        detection = await detectShareCandidateFromRoute();
      }
      if (!detection.detected) {
        return;
      }
      shareOrigin.value = "link";
      shareContextError.value = "链接解析中，请稍候";
      try {
        const context = await resolveContextFromCandidate(detection.candidate);
        if (context) {
          applyLinkContext(context);
        } else {
          shareContextError.value = "链接信息缺失或已失效，请联系代理重新生成";
        }
      } catch (error) {
        common_vendor.index.__f__("warn", "at pages/verify/index.vue:821", "Failed to resolve share link context", error);
        shareContextError.value = "解析分享信息失败，请稍后再试";
      }
    });
    const statusStyle = common_vendor.computed(() => {
      var _a;
      switch ((_a = payload.value) == null ? void 0 : _a.status) {
        case "success":
          return { title: "验证成功", statusClass: "status-success" };
        case "warning":
          return { title: "请注意", statusClass: "status-warning" };
        case "error":
          return { title: "验证失败", statusClass: "status-error" };
        default:
          return { title: "提醒", statusClass: "status-info" };
      }
    });
    const shareHint = common_vendor.computed(() => isH5 ? "复制链接分享给用户" : "复制或打开小程序路径");
    function utf8Decode(bytes) {
      if (typeof TextDecoder !== "undefined") {
        return new TextDecoder().decode(bytes);
      }
      let result = "";
      for (let i = 0; i < bytes.length; ) {
        const byte1 = bytes[i++];
        if (byte1 < 128) {
          result += String.fromCharCode(byte1);
        } else if (byte1 < 224) {
          const byte2 = bytes[i++];
          result += String.fromCharCode((byte1 & 31) << 6 | byte2 & 63);
        } else if (byte1 < 240) {
          const byte2 = bytes[i++];
          const byte3 = bytes[i++];
          result += String.fromCharCode((byte1 & 15) << 12 | (byte2 & 63) << 6 | byte3 & 63);
        } else {
          const byte2 = bytes[i++];
          const byte3 = bytes[i++];
          const byte4 = bytes[i++];
          const codePoint = (byte1 & 7) << 18 | (byte2 & 63) << 12 | (byte3 & 63) << 6 | byte4 & 63;
          const offset = codePoint - 65536;
          const high = 55296 + (offset >> 10);
          const low = 56320 + (offset & 1023);
          result += String.fromCharCode(high, low);
        }
      }
      return result;
    }
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: common_vendor.t(contextLabel.value),
        b: contextCodeLabel.value
      }, contextCodeLabel.value ? {
        c: common_vendor.t(contextCodeLabel.value)
      } : {}, {
        d: contextAgentLabel.value
      }, contextAgentLabel.value ? {
        e: common_vendor.t(contextAgentLabel.value)
      } : {}, {
        f: shareContextError.value
      }, shareContextError.value ? {
        g: common_vendor.t(shareContextError.value)
      } : {}, {
        h: !shareContextError.value && showShareInfo.value
      }, !shareContextError.value && showShareInfo.value ? common_vendor.e({
        i: common_vendor.t(shareLabel.value),
        j: common_vendor.t(shareDisplayValue.value),
        k: common_vendor.o(copyShareLink),
        l: common_vendor.o(copyShareLink),
        m: common_vendor.unref(isMiniProgram)
      }, common_vendor.unref(isMiniProgram) ? {
        n: common_vendor.o(openSharePage)
      } : {}, {
        o: common_vendor.t(shareHint.value)
      }) : {}, {
        p: isGeneratorMode.value
      }, isGeneratorMode.value ? common_vendor.e({
        q: common_vendor.t(currentBindingLabel.value),
        r: bindingOptions.value,
        s: common_vendor.o(handleBindingChange),
        t: common_vendor.t(currentSoftwareLabel.value),
        v: softwareOptions.value,
        w: common_vendor.o(handleSoftwareChange),
        x: common_vendor.t(common_vendor.unref(isH5) ? "链接" : "路径"),
        y: !generatorContext.value,
        z: common_vendor.o(generateShareLink),
        A: showShareInfo.value && shareOriginIsGenerated.value
      }, showShareInfo.value && shareOriginIsGenerated.value ? {
        B: common_vendor.t(common_vendor.unref(isH5) ? "链接" : "路径"),
        C: common_vendor.o(copyShareLink)
      } : {}, {
        D: showShareInfo.value && shareOriginIsGenerated.value && common_vendor.unref(isMiniProgram)
      }, showShareInfo.value && shareOriginIsGenerated.value && common_vendor.unref(isMiniProgram) ? {
        E: common_vendor.o(openSharePage)
      } : {}, {
        F: showShareInfo.value && shareOriginIsGenerated.value
      }, showShareInfo.value && shareOriginIsGenerated.value ? {
        G: common_vendor.t(shareDisplayValue.value)
      } : {}) : {}, {
        H: code.value,
        I: common_vendor.o(($event) => code.value = $event.detail.value),
        J: common_vendor.t(loading.value ? "验证中..." : "立即验证"),
        K: loading.value || !canVerify.value,
        L: common_vendor.o(handleVerify),
        M: payload.value
      }, payload.value ? common_vendor.e({
        N: common_vendor.t(statusStyle.value.title),
        O: common_vendor.t(payload.value.message),
        P: common_vendor.n(statusStyle.value.statusClass),
        Q: payload.value.stats
      }, payload.value.stats ? common_vendor.e({
        R: common_vendor.t(payload.value.stats.attemptNumber),
        S: common_vendor.t(payload.value.stats.remainingDownloads),
        T: payload.value.stats.expiresAt
      }, payload.value.stats.expiresAt ? {
        U: common_vendor.t(payload.value.stats.expiresAt)
      } : {}) : {}, {
        V: payload.value.downloadUrl
      }, payload.value.downloadUrl ? common_vendor.e({
        W: common_vendor.t(payload.value.downloadUrl),
        X: common_vendor.o(($event) => copyUrl(payload.value.downloadUrl)),
        Y: common_vendor.o(($event) => copyUrl(payload.value.downloadUrl)),
        Z: payload.value.extractionCode
      }, payload.value.extractionCode ? {
        aa: common_vendor.t(payload.value.extractionCode)
      } : {}) : {}, {
        ab: !payload.value.history.length
      }, !payload.value.history.length ? {} : {
        ac: common_vendor.f(payload.value.history, (item, k0, i0) => {
          return {
            a: common_vendor.t(item.url),
            b: common_vendor.t(item.extractionCode || "—"),
            c: common_vendor.t(item.assignedAt),
            d: item.id
          };
        })
      }) : {});
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-64565cbf"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/verify/index.js.map
