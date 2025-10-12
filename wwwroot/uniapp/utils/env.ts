// utils/env.ts
const STORAGE_KEY = 'SProtect:apiBaseUrl';
const DEFAULT_BASE_URL = 'http://192.168.1.2:8080';

/**
 * 规范化 URL
 */
function normalizeBaseUrl(value?: string | null): string | undefined {
  if (!value) return undefined;

  const trimmed = value.trim();
  if (!trimmed) return undefined;

  const lower = trimmed.toLowerCase();
  if (lower === 'mock' || lower.startsWith('mock://')) {
    return trimmed;
  }

  // 如果是完整 URL
  if (/^[a-z][a-z0-9+.-]*:\/\//i.test(trimmed)) {
    return trimmed;
  }

  return `http://${trimmed}`;
}

/**
 * 安全读取环境变量，兼容 H5 / App / 小程序 / Node
 */
function readEnv(key: string): string | undefined {
  try {
    // ✅ 1. Vite (H5) 环境
    const metaObj = (globalThis as any)?.import?.meta;
    if (metaObj && metaObj.env && metaObj.env[key]) {
      return metaObj.env[key] as string;
    }
  } catch (_) {
    // App 端无 import.meta，直接跳过
  }

  try {
    // ✅ 2. Node / process 环境
    if (typeof process !== 'undefined' && process.env) {
      const value = (process.env as Record<string, string | undefined>)[key];
      if (value) return value;
    }
  } catch (_) {}

  try {
    // ✅ 3. App / 小程序环境
    if (typeof uni !== 'undefined' && typeof uni.getStorageSync === 'function') {
      const val = uni.getStorageSync(key);
      if (val) return val;
    }
  } catch (_) {}

  return undefined;
}

/**
 * 获取当前接口地址
 */
export function resolveApiBaseUrl(): string {
  try {
    const stored = normalizeBaseUrl(uni.getStorageSync(STORAGE_KEY));
    if (stored) return stored;
  } catch (error) {
    console.warn('读取自定义 API 地址失败', error);
  }

  const runtimeConfig = (globalThis as any)?.__SPROTECT_RUNTIME_CONFIG__;
  const globalValue =
    normalizeBaseUrl(runtimeConfig?.apiBaseUrl) ||
    normalizeBaseUrl((globalThis as any)?.__SPROTECT_API_BASE_URL__);

  if (globalValue) {
    return globalValue;
  }

  const envValue =
    normalizeBaseUrl(readEnv('VITE_API_BASE_URL')) ||
    normalizeBaseUrl(readEnv('API_BASE_URL'));

  if (envValue) {
    return envValue;
  }

  return DEFAULT_BASE_URL;
}

/**
 * 更新接口地址
 */
export function updateApiBaseUrl(url?: string) {
  const normalized = normalizeBaseUrl(url);

  try {
    if (!normalized) {
      uni.removeStorageSync(STORAGE_KEY);
      return;
    }
    uni.setStorageSync(STORAGE_KEY, normalized);
  } catch (err) {
    console.warn('更新接口地址失败', err);
  }
}

/**
 * 判断是否为 mock 模式
 */
export function isMockMode(baseUrl: string): boolean {
  if (!baseUrl) return true;
  const normalized = baseUrl.toLowerCase();
  return normalized === 'mock' || normalized.startsWith('mock://');
}

/**
 * 获取存储键名
 */
export function getApiBaseStorageKey() {
  return STORAGE_KEY;
}
