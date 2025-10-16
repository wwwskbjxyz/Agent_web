"use strict";
const common_vendor = require("../common/vendor.js");
const STORAGE_KEY = "SProtect:apiBaseUrl";
const STORAGE_VERSION_KEY = "SProtect:apiBaseUrl:version";
const STORAGE_VERSION = "2";
const DEFAULT_BASE_URL = "https://chen.roccloudiot.cn:5001";
function normalizeBaseUrl(value) {
  if (!value)
    return void 0;
  const trimmed = value.trim();
  if (!trimmed)
    return void 0;
  const lower = trimmed.toLowerCase();
  if (lower === "mock" || lower.startsWith("mock://")) {
    return trimmed;
  }
  if (/^[a-z][a-z0-9+.-]*:\/\//i.test(trimmed)) {
    return trimmed;
  }
  return `http://${trimmed}`;
}
function readEnv(key) {
  var _a;
  try {
    const metaObj = (_a = globalThis == null ? void 0 : globalThis.import) == null ? void 0 : _a.meta;
    if (metaObj && metaObj.env && metaObj.env[key]) {
      return metaObj.env[key];
    }
  } catch (_) {
  }
  try {
    if (typeof process !== "undefined" && process.env) {
      const value = process.env[key];
      if (value)
        return value;
    }
  } catch (_) {
  }
  try {
    if (typeof common_vendor.index !== "undefined" && typeof common_vendor.index.getStorageSync === "function") {
      const val = common_vendor.index.getStorageSync(key);
      if (val)
        return val;
    }
  } catch (_) {
  }
  return void 0;
}
function resolveApiBaseUrl() {
  try {
    ensureStorageVersion();
    if (typeof common_vendor.index !== "undefined" && typeof common_vendor.index.getStorageSync === "function") {
      const stored = normalizeBaseUrl(common_vendor.index.getStorageSync(STORAGE_KEY));
      if (stored)
        return stored;
    }
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/env.ts:73", "读取自定义 API 地址失败", error);
  }
  const runtimeConfig = globalThis == null ? void 0 : globalThis.__SPROTECT_RUNTIME_CONFIG__;
  const globalValue = normalizeBaseUrl(runtimeConfig == null ? void 0 : runtimeConfig.apiBaseUrl) || normalizeBaseUrl(globalThis == null ? void 0 : globalThis.__SPROTECT_API_BASE_URL__);
  if (globalValue) {
    return globalValue;
  }
  const envValue = normalizeBaseUrl(readEnv("VITE_API_BASE_URL")) || normalizeBaseUrl(readEnv("API_BASE_URL"));
  if (envValue) {
    return envValue;
  }
  return DEFAULT_BASE_URL;
}
function isMockMode(baseUrl) {
  if (!baseUrl)
    return true;
  const normalized = baseUrl.toLowerCase();
  return normalized === "mock" || normalized.startsWith("mock://");
}
function ensureStorageVersion() {
  try {
    if (typeof common_vendor.index === "undefined" || typeof common_vendor.index.getStorageSync !== "function") {
      return;
    }
    const current = common_vendor.index.getStorageSync(STORAGE_VERSION_KEY);
    if (current !== STORAGE_VERSION) {
      common_vendor.index.removeStorageSync(STORAGE_KEY);
      if (STORAGE_VERSION) {
        common_vendor.index.setStorageSync(STORAGE_VERSION_KEY, STORAGE_VERSION);
      }
    }
  } catch (_) {
  }
}
exports.isMockMode = isMockMode;
exports.resolveApiBaseUrl = resolveApiBaseUrl;
//# sourceMappingURL=../../.sourcemap/mp-weixin/utils/env.js.map
