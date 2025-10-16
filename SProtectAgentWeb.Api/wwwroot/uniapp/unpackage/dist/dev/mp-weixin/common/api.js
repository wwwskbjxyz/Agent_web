"use strict";
const common_vendor = require("./vendor.js");
const utils_env = require("../utils/env.js");
const utils_storage = require("../utils/storage.js");
let baseURL = utils_env.resolveApiBaseUrl();
let mockEnabled = utils_env.isMockMode(baseURL);
function resolveUrl(rawUrl, skipProxy) {
  if (!rawUrl.startsWith("/")) {
    return { url: rawUrl, proxied: false };
  }
  if (skipProxy) {
    return { url: rawUrl, proxied: false };
  }
  if (rawUrl.startsWith("/api/agents") || rawUrl.startsWith("/api/authors") || rawUrl.startsWith("/api/bindings") || rawUrl.startsWith("/api/proxy") || rawUrl.startsWith("/api/wechat")) {
    return { url: rawUrl, proxied: rawUrl.startsWith("/api/proxy") };
  }
  const software = utils_storage.getSelectedSoftwareName();
  if (!software) {
    throw new Error("未选择软件码，无法发起代理请求");
  }
  const normalized = rawUrl.replace(/^\//, "");
  return { url: `/api/proxy/${encodeURIComponent(software)}/${normalized}`, proxied: true };
}
async function apiRequest(options) {
  const allowMock = !options.disableMock && mockEnabled && options.mockData;
  if (allowMock) {
    return Promise.resolve(options.mockData);
  }
  let requestUrl;
  let proxied = false;
  try {
    const resolved = resolveUrl(options.url, options.skipProxy ?? false);
    requestUrl = resolved.url;
    proxied = resolved.proxied;
  } catch (error) {
    return Promise.reject(error);
  }
  function isApiEnvelope(payload) {
    return payload && typeof payload === "object" && "code" in payload && typeof payload.code === "number" && "message" in payload;
  }
  return new Promise((resolve, reject) => {
    const headers = {
      "Content-Type": "application/json",
      ...options.headers ?? {}
    };
    const shouldAuth = options.auth ?? proxied;
    if (shouldAuth) {
      const role = options.authRole || (options.url.startsWith("/api/authors") ? "author" : "agent");
      const token = role === "author" ? utils_storage.getAuthorToken() : utils_storage.getPlatformToken();
      if (token) {
        headers.Authorization = `Bearer ${token}`;
      }
    }
    if (proxied) {
      const remoteToken = utils_storage.getTokenValue();
      if (remoteToken) {
        headers["X-SProtect-Remote-Token"] = remoteToken;
      }
    }
    const isAbsoluteUrl = /^[a-z][a-z0-9+.-]*:\/\//i.test(requestUrl);
    const finalUrl = isAbsoluteUrl ? requestUrl : `${baseURL}${requestUrl}`;
    common_vendor.index.request({
      url: finalUrl,
      method: options.method ?? "GET",
      data: options.data,
      header: headers,
      success: (res) => {
        if (res.statusCode && res.statusCode >= 200 && res.statusCode < 300) {
          const payload = res.data;
          if (isApiEnvelope(payload)) {
            if (payload.code === 0) {
              resolve(payload.data);
              return;
            }
            if (allowMock) {
              resolve(options.mockData);
              return;
            }
            const error = new Error(payload.message || "请求失败");
            error.code = payload.code;
            error.response = payload;
            reject(error);
            return;
          }
          resolve(payload);
          return;
        }
        if (res.statusCode === 401) {
          if (options.authRole === "author" || options.url.startsWith("/api/authors")) {
            utils_storage.clearAuthorToken();
          } else {
            utils_storage.clearPlatformToken();
          }
          utils_storage.clearToken();
          common_vendor.index.$emit("app:unauthorized", { message: "登录已过期，请重新登录" });
        }
        if (allowMock) {
          resolve(options.mockData);
          return;
        }
        reject(res);
      },
      fail: (err) => {
        if (allowMock) {
          resolve(options.mockData);
        } else {
          reject(err);
        }
      }
    });
  });
}
function refreshBaseURL() {
  baseURL = utils_env.resolveApiBaseUrl();
  mockEnabled = utils_env.isMockMode(baseURL);
  return baseURL;
}
function setBaseURL(url) {
  baseURL = url;
  mockEnabled = utils_env.isMockMode(baseURL);
}
exports.apiRequest = apiRequest;
exports.refreshBaseURL = refreshBaseURL;
exports.setBaseURL = setBaseURL;
//# sourceMappingURL=../../.sourcemap/mp-weixin/common/api.js.map
