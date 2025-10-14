const TOKEN_KEY = 'SProtect:authToken';
const PLATFORM_TOKEN_KEY = 'SProtect:platformToken';
const AUTHOR_TOKEN_KEY = 'SProtect:authorToken';
const SELECTED_SOFTWARE_KEY = 'SProtect:selectedSoftware';
const SELECTED_SOFTWARE_DISPLAY_KEY = 'SProtect:selectedSoftwareDisplay';
const THEME_KEY = 'SProtect:theme';
const WECHAT_SUBSCRIPTIONS_KEY = 'SProtect:wechatSubscriptions';

export type ThemePreference = 'dark' | 'light' | 'cartoon';

export function setTokenValue(token: string) {
  if (!token) {
    uni.removeStorageSync(TOKEN_KEY);
    return;
  }
  uni.setStorageSync(TOKEN_KEY, token);
}

export function getTokenValue(): string | null {
  try {
    const value = uni.getStorageSync(TOKEN_KEY);
    return value || null;
  } catch (error) {
    console.warn('读取 Token 失败', error);
    return null;
  }
}

export function clearToken() {
  uni.removeStorageSync(TOKEN_KEY);
}

export function getTokenStorageKey() {
  return TOKEN_KEY;
}

export function setPlatformToken(token: string) {
  if (!token) {
    uni.removeStorageSync(PLATFORM_TOKEN_KEY);
    return;
  }
  uni.setStorageSync(PLATFORM_TOKEN_KEY, token);
}

export function getPlatformToken(): string | null {
  try {
    const value = uni.getStorageSync(PLATFORM_TOKEN_KEY);
    return value || null;
  } catch (error) {
    console.warn('读取平台 Token 失败', error);
    return null;
  }
}

export function clearPlatformToken() {
  uni.removeStorageSync(PLATFORM_TOKEN_KEY);
}

export function setAuthorToken(token: string) {
  if (!token) {
    uni.removeStorageSync(AUTHOR_TOKEN_KEY);
    return;
  }
  uni.setStorageSync(AUTHOR_TOKEN_KEY, token);
}

export function getAuthorToken(): string | null {
  try {
    const value = uni.getStorageSync(AUTHOR_TOKEN_KEY);
    return value || null;
  } catch (error) {
    console.warn('读取作者 Token 失败', error);
    return null;
  }
}

export function clearAuthorToken() {
  uni.removeStorageSync(AUTHOR_TOKEN_KEY);
}

export function setSelectedSoftwareName(value?: string | null) {
  const target = typeof value === 'string' ? value.trim() : '';
  if (!target) {
    try {
      uni.removeStorageSync(SELECTED_SOFTWARE_KEY);
    } catch (error) {
      console.warn('清除软件位缓存失败', error);
    }
    clearSelectedSoftwareDisplayName();
    return;
  }
  try {
    uni.setStorageSync(SELECTED_SOFTWARE_KEY, target);
  } catch (error) {
    console.warn('保存软件位失败', error);
  }
}

export function getSelectedSoftwareName(): string | null {
  try {
    const value = uni.getStorageSync(SELECTED_SOFTWARE_KEY);
    if (typeof value === 'string') {
      const normalized = value.trim();
      if (normalized) {
        return normalized;
      }
    }
    return null;
  } catch (error) {
    console.warn('读取软件位失败', error);
    return null;
  }
}

export function clearSelectedSoftwareName() {
  try {
    uni.removeStorageSync(SELECTED_SOFTWARE_KEY);
  } catch (error) {
    console.warn('清除软件位失败', error);
  }
}

export function setSelectedSoftwareDisplayName(value?: string | null) {
  const target = typeof value === 'string' ? value.trim() : '';
  try {
    if (!target) {
      uni.removeStorageSync(SELECTED_SOFTWARE_DISPLAY_KEY);
    } else {
      uni.setStorageSync(SELECTED_SOFTWARE_DISPLAY_KEY, target);
    }
  } catch (error) {
    console.warn('保存展示软件位失败', error);
  }
}

export function getSelectedSoftwareDisplayName(): string | null {
  try {
    const value = uni.getStorageSync(SELECTED_SOFTWARE_DISPLAY_KEY);
    if (typeof value === 'string') {
      const normalized = value.trim();
      if (normalized) {
        return normalized;
      }
    }
    return null;
  } catch (error) {
    console.warn('读取展示软件位失败', error);
    return null;
  }
}

export function clearSelectedSoftwareDisplayName() {
  try {
    uni.removeStorageSync(SELECTED_SOFTWARE_DISPLAY_KEY);
  } catch (error) {
    console.warn('清除展示软件位失败', error);
  }
}

export function setThemePreference(value: ThemePreference) {
  try {
    uni.setStorageSync(THEME_KEY, value);
  } catch (error) {
    console.warn('保存主题偏好失败', error);
  }
}

export function getThemePreference(): ThemePreference {
  try {
    const value = uni.getStorageSync(THEME_KEY);
    if (typeof value === 'string') {
      const normalized = value.trim().toLowerCase();
      if (['dark', 'light', 'cartoon'].includes(normalized)) {
        return normalized as ThemePreference;
      }
    }
  } catch (error) {
    console.warn('读取主题偏好失败', error);
  }
  return 'dark';
}

function readWeChatSubscriptionMap(): Record<string, Record<string, boolean>> {
  try {
    const raw = uni.getStorageSync(WECHAT_SUBSCRIPTIONS_KEY);
    if (typeof raw === 'string' && raw.trim()) {
      const parsed = JSON.parse(raw);
      if (parsed && typeof parsed === 'object') {
        return parsed as Record<string, Record<string, boolean>>;
      }
    }
    if (raw && typeof raw === 'object') {
      return raw as Record<string, Record<string, boolean>>;
    }
  } catch (error) {
    console.warn('读取微信订阅偏好失败', error);
  }
  return {};
}

function writeWeChatSubscriptionMap(value: Record<string, Record<string, boolean>>) {
  try {
    uni.setStorageSync(WECHAT_SUBSCRIPTIONS_KEY, JSON.stringify(value));
  } catch (error) {
    console.warn('保存微信订阅偏好失败', error);
  }
}

export function getWeChatSubscriptions(openId: string): Record<string, boolean> {
  if (!openId) {
    return {};
  }
  const map = readWeChatSubscriptionMap();
  const preferences = map[openId];
  if (preferences && typeof preferences === 'object') {
    return { ...preferences };
  }
  return {};
}

export function setWeChatSubscriptions(openId: string, preferences: Record<string, boolean>) {
  if (!openId) {
    return;
  }
  const map = readWeChatSubscriptionMap();
  map[openId] = { ...preferences };
  writeWeChatSubscriptionMap(map);
}

export function clearWeChatSubscriptions(openId: string) {
  if (!openId) {
    return;
  }
  const map = readWeChatSubscriptionMap();
  if (map[openId]) {
    delete map[openId];
    writeWeChatSubscriptionMap(map);
  }
}
