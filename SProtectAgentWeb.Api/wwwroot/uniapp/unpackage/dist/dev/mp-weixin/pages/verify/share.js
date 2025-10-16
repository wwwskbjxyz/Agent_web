"use strict";
const common_vendor = require("../../common/vendor.js");
const stores_app = require("../../stores/app.js");
const common_api = require("../../common/api.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "share",
  setup(__props) {
    const appStore = stores_app.useAppStore();
    const { verification } = common_vendor.storeToRefs(appStore);
    const software = common_vendor.ref("");
    const softwareDisplayName = common_vendor.ref("");
    const softwareCode = common_vendor.ref("");
    const agentAccount = common_vendor.ref("");
    const agentDisplayName = common_vendor.ref("");
    const contextError = common_vendor.ref("");
    const cardKey = common_vendor.ref("");
    const loading = common_vendor.computed(() => appStore.loading.verification);
    const payload = common_vendor.computed(() => verification.value);
    const softwareLabel = common_vendor.computed(() => softwareDisplayName.value || software.value);
    const agentLabel = common_vendor.computed(() => agentDisplayName.value || agentAccount.value || "");
    const contextReady = common_vendor.computed(() => !!software.value && !!softwareCode.value);
    const canVerify = common_vendor.computed(() => contextReady.value && !!cardKey.value.trim() && !loading.value);
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
    common_vendor.onLoad(async (options) => {
      contextError.value = "链接解析中，请稍候";
      resetContextState();
      try {
        const context = await resolveContext(options);
        if (context) {
          applyContext(context);
        } else {
          contextError.value = "链接信息缺失，请联系代理重新生成";
        }
      } catch (error) {
        common_vendor.index.__f__("warn", "at pages/verify/share.vue:170", "Failed to resolve share context", error);
        contextError.value = "解析链接信息失败，请稍后重试";
      }
    });
    function applyContext(context) {
      software.value = context.software;
      softwareDisplayName.value = context.softwareDisplayName || context.software;
      softwareCode.value = context.softwareCode;
      agentAccount.value = context.agentAccount ?? "";
      agentDisplayName.value = context.agentDisplayName ?? "";
      contextError.value = "";
      verification.value = null;
      if (context.gateway) {
        common_api.setBaseURL(context.gateway);
      }
    }
    function resetContextState() {
      software.value = "";
      softwareDisplayName.value = "";
      softwareCode.value = "";
      agentAccount.value = "";
      agentDisplayName.value = "";
      verification.value = null;
    }
    function handleVerify() {
      if (!cardKey.value.trim()) {
        common_vendor.index.showToast({ title: "请输入卡密", icon: "none" });
        return;
      }
      if (!contextReady.value) {
        common_vendor.index.showToast({ title: contextError.value || "链接信息缺失", icon: "none" });
        return;
      }
      appStore.loadVerification(cardKey.value.trim(), {
        software: software.value,
        softwareCode: softwareCode.value,
        agentAccount: agentAccount.value || void 0
      });
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
    async function resolveContext(options) {
      const primary = await ensureContext(resolveFromOptions(options));
      if (primary) {
        return primary;
      }
      const fallback = await resolveFromRoute();
      return ensureContext(fallback);
    }
    async function ensureContext(candidate) {
      if (!candidate) {
        return null;
      }
      const gatewayValue = resolveGateway(candidate.gateway ?? "");
      const codeValue = sanitizeString(candidate.softwareCode);
      const softwareValue = sanitizeString(candidate.software);
      const displayValue = sanitizeString(candidate.softwareDisplayName);
      const agentValue = sanitizeString(candidate.agentAccount);
      const agentDisplayValue = sanitizeString(candidate.agentDisplayName);
      if (softwareValue && codeValue) {
        return {
          software: softwareValue,
          softwareCode: codeValue,
          softwareDisplayName: displayValue || softwareValue,
          agentAccount: agentValue || void 0,
          agentDisplayName: agentDisplayValue || void 0,
          gateway: gatewayValue || void 0
        };
      }
      if (!codeValue) {
        return null;
      }
      const remote = await fetchContextByCode(codeValue);
      if (!remote) {
        return null;
      }
      const remoteSoftware = sanitizeString(remote.software);
      const remoteCode = sanitizeString(remote.softwareCode) || codeValue;
      const remoteDisplay = sanitizeString(remote.softwareDisplayName);
      const remoteAgent = sanitizeString(remote.agentAccount);
      const remoteAgentDisplay = sanitizeString(remote.agentDisplayName);
      if (!remoteSoftware) {
        return null;
      }
      return {
        software: remoteSoftware,
        softwareCode: remoteCode,
        softwareDisplayName: displayValue || remoteDisplay || remoteSoftware,
        agentAccount: agentValue || remoteAgent || void 0,
        agentDisplayName: agentDisplayValue || remoteAgentDisplay || void 0,
        gateway: gatewayValue || void 0
      };
    }
    function resolveFromOptions(options) {
      if (!options) {
        return null;
      }
      const shareRaw = decodeComponent(options.share);
      if (shareRaw) {
        const decoded = decodeShareSlug(shareRaw);
        if (decoded) {
          return decoded;
        }
      }
      const softwareValue = decodeComponent(
        options.s ?? options.software ?? options.softwareName ?? options.slot
      );
      const codeValue = decodeComponent(
        options.c ?? options.code ?? options.softwareCode ?? options.binding
      );
      const agentValue = decodeComponent(options.a ?? options.agent ?? options.agentAccount);
      const agentDisplayValue = decodeComponent(
        options.agentName ?? options.agentDisplay ?? options.agentDisplayName
      );
      const displayValue = decodeComponent(
        options.display ?? options.softwareDisplay ?? options.softwareDisplayName ?? options.name
      );
      const gatewayValue = resolveGateway(decodeComponent(options.gateway ?? options.api ?? options.gw));
      if (!codeValue) {
        return null;
      }
      return {
        software: softwareValue || void 0,
        softwareCode: codeValue,
        agentAccount: agentValue || void 0,
        agentDisplayName: agentDisplayValue || void 0,
        softwareDisplayName: displayValue || void 0,
        gateway: gatewayValue || void 0
      };
    }
    async function resolveFromRoute() {
      var _a;
      if (typeof getCurrentPages !== "function") {
        return null;
      }
      const pages = getCurrentPages();
      if (!Array.isArray(pages) || !pages.length) {
        return null;
      }
      const current = pages[pages.length - 1];
      const fullPath = ((_a = current == null ? void 0 : current.$page) == null ? void 0 : _a.fullPath) || "";
      if (!fullPath) {
        return null;
      }
      const queryIndex = fullPath.indexOf("?");
      if (queryIndex < 0) {
        const segments = fullPath.split("/").filter(Boolean);
        if (segments.length > 2) {
          const slug = segments[segments.length - 1];
          const decoded = decodeShareSlug(decodeComponent(slug));
          if (decoded) {
            return decoded;
          }
        }
        return null;
      }
      const queryString = fullPath.slice(queryIndex + 1);
      const map = parseQuery(queryString);
      if (map.share) {
        const decoded = decodeShareSlug(decodeComponent(map.share));
        if (decoded) {
          return decoded;
        }
      }
      const softwareValue = decodeComponent(
        map.s || map.software || map.softwarename || map.slot || ""
      );
      const codeValue = decodeComponent(
        map.c || map.code || map.softwarecode || map.binding || ""
      );
      const agentValue = decodeComponent(
        map.a || map.agent || map.agentaccount || ""
      );
      const agentDisplayValue = decodeComponent(
        map.agentname || map.agentdisplay || map.agentdisplayname || ""
      );
      const displayValue = decodeComponent(
        map.display || map.softwaredisplay || map.softwaredisplayname || map.name || ""
      );
      const gatewayValue = resolveGateway(decodeComponent(map.gateway || map.api || map.gw));
      if (!codeValue) {
        return null;
      }
      return {
        software: softwareValue || void 0,
        softwareCode: codeValue,
        agentAccount: agentValue || void 0,
        agentDisplayName: agentDisplayValue || void 0,
        softwareDisplayName: displayValue || void 0,
        gateway: gatewayValue || void 0
      };
    }
    function sanitizeString(value) {
      if (typeof value !== "string") {
        return "";
      }
      return value.trim();
    }
    function decodeComponent(value) {
      const input = sanitizeString(value);
      if (!input) {
        return "";
      }
      try {
        return decodeURIComponent(input);
      } catch (error) {
        return input;
      }
    }
    function parseQuery(query) {
      const map = {};
      if (!query) {
        return map;
      }
      const pairs = query.split("&");
      for (const pair of pairs) {
        if (!pair)
          continue;
        const [key, value = ""] = pair.split("=");
        const normalizedKey = sanitizeString(key);
        if (!normalizedKey)
          continue;
        map[normalizedKey] = value;
      }
      return map;
    }
    function decodeShareSlug(slug) {
      if (!slug) {
        return null;
      }
      try {
        let base64 = slug.replace(/-/g, "+").replace(/_/g, "/");
        const padding = base64.length % 4;
        if (padding) {
          base64 = base64.padEnd(base64.length + (4 - padding), "=");
        }
        const buffer = base64ToArrayBuffer(base64);
        const json = utf8Decode(new Uint8Array(buffer));
        const raw = JSON.parse(json) ?? {};
        const softwareValue = sanitizeString(raw.s ?? raw.software ?? raw.softwareName ?? raw.slot);
        const codeValue = sanitizeString(raw.c ?? raw.code ?? raw.softwareCode ?? raw.binding);
        const agentValue = sanitizeString(raw.a ?? raw.agent ?? raw.agentAccount);
        const agentDisplayValue = sanitizeString(
          raw.an ?? raw.agentName ?? raw.agentDisplay ?? raw.agentDisplayName
        );
        const displayValue = sanitizeString(
          raw.d ?? raw.display ?? raw.softwareDisplay ?? raw.softwareDisplayName
        );
        const gatewayValue = resolveGateway(sanitizeString(raw.g));
        if (!codeValue) {
          return null;
        }
        return {
          software: softwareValue || void 0,
          softwareCode: codeValue,
          agentAccount: agentValue || void 0,
          agentDisplayName: agentDisplayValue || void 0,
          softwareDisplayName: displayValue || void 0,
          gateway: gatewayValue || void 0
        };
      } catch (error) {
        common_vendor.index.__f__("warn", "at pages/verify/share.vue:462", "Failed to decode share slug", error);
        return null;
      }
    }
    async function fetchContextByCode(code) {
      const normalized = sanitizeString(code);
      if (!normalized) {
        return null;
      }
      try {
        const payload2 = await common_api.apiRequest({
          url: "/api/card-verification/context",
          method: "GET",
          data: { softwareCode: normalized },
          skipProxy: true,
          auth: false
        });
        if (!payload2) {
          return null;
        }
        return {
          software: sanitizeString(payload2.software) || void 0,
          softwareCode: sanitizeString(payload2.softwareCode) || normalized,
          softwareDisplayName: sanitizeString(payload2.softwareDisplayName) || void 0,
          agentAccount: sanitizeString(payload2.agentAccount) || void 0,
          agentDisplayName: sanitizeString(payload2.agentDisplayName) || void 0
        };
      } catch (error) {
        common_vendor.index.__f__("warn", "at pages/verify/share.vue:491", "Failed to load verification context", error);
        return null;
      }
    }
    function resolveGateway(value) {
      if (!value) {
        return "";
      }
      const trimmed = value.trim();
      if (!trimmed) {
        return "";
      }
      const lower = trimmed.toLowerCase();
      if (!lower.startsWith("http://") && !lower.startsWith("https://")) {
        return "";
      }
      return trimmed.replace(/\/+$/, "");
    }
    function base64ToArrayBuffer(base64) {
      if (typeof common_vendor.index !== "undefined" && typeof common_vendor.index.base64ToArrayBuffer === "function") {
        return common_vendor.index.base64ToArrayBuffer(base64);
      }
      if (typeof atob === "function") {
        const binary = atob(base64);
        const length = binary.length;
        const bytes = new Uint8Array(length);
        for (let i = 0; i < length; i++) {
          bytes[i] = binary.charCodeAt(i);
        }
        return bytes.buffer;
      }
      if (typeof Buffer !== "undefined") {
        const buffer = Buffer.from(base64, "base64");
        return buffer.buffer.slice(buffer.byteOffset, buffer.byteOffset + buffer.byteLength);
      }
      throw new Error("当前环境不支持 Base64 解码");
    }
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
        a: common_vendor.t(contextReady.value ? "输入卡密即可验证并获取下载链接" : "链接缺少必要信息，请联系代理确认"),
        b: common_vendor.t(softwareLabel.value || "未提供"),
        c: common_vendor.t(softwareCode.value || "未提供"),
        d: agentLabel.value
      }, agentLabel.value ? {
        e: common_vendor.t(agentLabel.value)
      } : {}, {
        f: contextError.value
      }, contextError.value ? {
        g: common_vendor.t(contextError.value)
      } : {}, {
        h: cardKey.value,
        i: common_vendor.o(($event) => cardKey.value = $event.detail.value),
        j: common_vendor.t(loading.value ? "验证中..." : "立即验证"),
        k: loading.value || !canVerify.value,
        l: common_vendor.o(handleVerify),
        m: payload.value
      }, payload.value ? common_vendor.e({
        n: common_vendor.t(statusStyle.value.title),
        o: common_vendor.t(payload.value.message),
        p: common_vendor.n(statusStyle.value.statusClass),
        q: payload.value.stats
      }, payload.value.stats ? common_vendor.e({
        r: common_vendor.t(payload.value.stats.attemptNumber),
        s: common_vendor.t(payload.value.stats.remainingDownloads),
        t: payload.value.stats.expiresAt
      }, payload.value.stats.expiresAt ? {
        v: common_vendor.t(payload.value.stats.expiresAt)
      } : {}) : {}, {
        w: payload.value.downloadUrl
      }, payload.value.downloadUrl ? common_vendor.e({
        x: common_vendor.t(payload.value.downloadUrl),
        y: common_vendor.o(($event) => copyUrl(payload.value.downloadUrl)),
        z: common_vendor.o(($event) => copyUrl(payload.value.downloadUrl)),
        A: payload.value.extractionCode
      }, payload.value.extractionCode ? {
        B: common_vendor.t(payload.value.extractionCode)
      } : {}) : {}, {
        C: !payload.value.history.length
      }, !payload.value.history.length ? {} : {
        D: common_vendor.f(payload.value.history, (item, k0, i0) => {
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
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-48234674"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/verify/share.js.map
