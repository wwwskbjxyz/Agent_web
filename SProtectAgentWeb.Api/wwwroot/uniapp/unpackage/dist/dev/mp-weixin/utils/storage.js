"use strict";
const common_vendor = require("../common/vendor.js");
const TOKEN_KEY = "SProtect:authToken";
const PLATFORM_TOKEN_KEY = "SProtect:platformToken";
const AUTHOR_TOKEN_KEY = "SProtect:authorToken";
const SELECTED_SOFTWARE_KEY = "SProtect:selectedSoftware";
const SELECTED_SOFTWARE_DISPLAY_KEY = "SProtect:selectedSoftwareDisplay";
const THEME_KEY = "SProtect:theme";
const WECHAT_SUBSCRIPTIONS_KEY = "SProtect:wechatSubscriptions";
function setTokenValue(token) {
  if (!token) {
    common_vendor.index.removeStorageSync(TOKEN_KEY);
    return;
  }
  common_vendor.index.setStorageSync(TOKEN_KEY, token);
}
function getTokenValue() {
  try {
    const value = common_vendor.index.getStorageSync(TOKEN_KEY);
    return value || null;
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:24", "读取 Token 失败", error);
    return null;
  }
}
function clearToken() {
  common_vendor.index.removeStorageSync(TOKEN_KEY);
}
function setPlatformToken(token) {
  if (!token) {
    common_vendor.index.removeStorageSync(PLATFORM_TOKEN_KEY);
    return;
  }
  common_vendor.index.setStorageSync(PLATFORM_TOKEN_KEY, token);
}
function getPlatformToken() {
  try {
    const value = common_vendor.index.getStorageSync(PLATFORM_TOKEN_KEY);
    return value || null;
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:50", "读取平台 Token 失败", error);
    return null;
  }
}
function clearPlatformToken() {
  common_vendor.index.removeStorageSync(PLATFORM_TOKEN_KEY);
}
function setAuthorToken(token) {
  if (!token) {
    common_vendor.index.removeStorageSync(AUTHOR_TOKEN_KEY);
    return;
  }
  common_vendor.index.setStorageSync(AUTHOR_TOKEN_KEY, token);
}
function getAuthorToken() {
  try {
    const value = common_vendor.index.getStorageSync(AUTHOR_TOKEN_KEY);
    return value || null;
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:72", "读取作者 Token 失败", error);
    return null;
  }
}
function clearAuthorToken() {
  common_vendor.index.removeStorageSync(AUTHOR_TOKEN_KEY);
}
function setSelectedSoftwareName(value) {
  const target = typeof value === "string" ? value.trim() : "";
  if (!target) {
    try {
      common_vendor.index.removeStorageSync(SELECTED_SOFTWARE_KEY);
    } catch (error) {
      common_vendor.index.__f__("warn", "at utils/storage.ts:87", "清除软件位缓存失败", error);
    }
    clearSelectedSoftwareDisplayName();
    return;
  }
  try {
    common_vendor.index.setStorageSync(SELECTED_SOFTWARE_KEY, target);
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:95", "保存软件位失败", error);
  }
}
function getSelectedSoftwareName() {
  try {
    const value = common_vendor.index.getStorageSync(SELECTED_SOFTWARE_KEY);
    if (typeof value === "string") {
      const normalized = value.trim();
      if (normalized) {
        return normalized;
      }
    }
    return null;
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:110", "读取软件位失败", error);
    return null;
  }
}
function clearSelectedSoftwareName() {
  try {
    common_vendor.index.removeStorageSync(SELECTED_SOFTWARE_KEY);
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:119", "清除软件位失败", error);
  }
}
function setSelectedSoftwareDisplayName(value) {
  const target = typeof value === "string" ? value.trim() : "";
  try {
    if (!target) {
      common_vendor.index.removeStorageSync(SELECTED_SOFTWARE_DISPLAY_KEY);
    } else {
      common_vendor.index.setStorageSync(SELECTED_SOFTWARE_DISPLAY_KEY, target);
    }
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:132", "保存展示软件位失败", error);
  }
}
function getSelectedSoftwareDisplayName() {
  try {
    const value = common_vendor.index.getStorageSync(SELECTED_SOFTWARE_DISPLAY_KEY);
    if (typeof value === "string") {
      const normalized = value.trim();
      if (normalized) {
        return normalized;
      }
    }
    return null;
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:147", "读取展示软件位失败", error);
    return null;
  }
}
function clearSelectedSoftwareDisplayName() {
  try {
    common_vendor.index.removeStorageSync(SELECTED_SOFTWARE_DISPLAY_KEY);
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:156", "清除展示软件位失败", error);
  }
}
function setThemePreference(value) {
  try {
    common_vendor.index.setStorageSync(THEME_KEY, value);
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:164", "保存主题偏好失败", error);
  }
}
function getThemePreference() {
  try {
    const value = common_vendor.index.getStorageSync(THEME_KEY);
    if (typeof value === "string") {
      const normalized = value.trim().toLowerCase();
      if (["dark", "light", "cartoon"].includes(normalized)) {
        return normalized;
      }
    }
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:178", "读取主题偏好失败", error);
  }
  return "dark";
}
function readWeChatSubscriptionMap() {
  try {
    const raw = common_vendor.index.getStorageSync(WECHAT_SUBSCRIPTIONS_KEY);
    if (typeof raw === "string" && raw.trim()) {
      const parsed = JSON.parse(raw);
      if (parsed && typeof parsed === "object") {
        return parsed;
      }
    }
    if (raw && typeof raw === "object") {
      return raw;
    }
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:196", "读取微信订阅偏好失败", error);
  }
  return {};
}
function writeWeChatSubscriptionMap(value) {
  try {
    common_vendor.index.setStorageSync(WECHAT_SUBSCRIPTIONS_KEY, JSON.stringify(value));
  } catch (error) {
    common_vendor.index.__f__("warn", "at utils/storage.ts:205", "保存微信订阅偏好失败", error);
  }
}
function getWeChatSubscriptions(openId) {
  if (!openId) {
    return {};
  }
  const map = readWeChatSubscriptionMap();
  const preferences = map[openId];
  if (preferences && typeof preferences === "object") {
    return { ...preferences };
  }
  return {};
}
function setWeChatSubscriptions(openId, preferences) {
  if (!openId) {
    return;
  }
  const map = readWeChatSubscriptionMap();
  map[openId] = { ...preferences };
  writeWeChatSubscriptionMap(map);
}
exports.clearAuthorToken = clearAuthorToken;
exports.clearPlatformToken = clearPlatformToken;
exports.clearSelectedSoftwareDisplayName = clearSelectedSoftwareDisplayName;
exports.clearSelectedSoftwareName = clearSelectedSoftwareName;
exports.clearToken = clearToken;
exports.getAuthorToken = getAuthorToken;
exports.getPlatformToken = getPlatformToken;
exports.getSelectedSoftwareDisplayName = getSelectedSoftwareDisplayName;
exports.getSelectedSoftwareName = getSelectedSoftwareName;
exports.getThemePreference = getThemePreference;
exports.getTokenValue = getTokenValue;
exports.getWeChatSubscriptions = getWeChatSubscriptions;
exports.setAuthorToken = setAuthorToken;
exports.setPlatformToken = setPlatformToken;
exports.setSelectedSoftwareDisplayName = setSelectedSoftwareDisplayName;
exports.setSelectedSoftwareName = setSelectedSoftwareName;
exports.setThemePreference = setThemePreference;
exports.setTokenValue = setTokenValue;
exports.setWeChatSubscriptions = setWeChatSubscriptions;
//# sourceMappingURL=../../.sourcemap/mp-weixin/utils/storage.js.map
