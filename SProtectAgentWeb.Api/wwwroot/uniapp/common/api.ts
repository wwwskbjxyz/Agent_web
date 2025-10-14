import { isMockMode, resolveApiBaseUrl } from '@/utils/env';
import {
  clearAuthorToken,
  clearPlatformToken,
  clearToken,
  getAuthorToken,
  getPlatformToken,
  getSelectedSoftwareName,
  getTokenValue
} from '@/utils/storage';

interface ApiEnvelope<T> {
  code: number;
  message: string;
  data: T;
}

interface RequestOptions<T> {
  url: string;
  method?: UniApp.RequestOptions['method'];
  data?: Record<string, unknown>;
  mockData?: T;
  headers?: Record<string, string>;
  auth?: boolean;
  disableMock?: boolean;
  authRole?: 'agent' | 'author';
  skipProxy?: boolean;
}

let baseURL = resolveApiBaseUrl();
let mockEnabled = isMockMode(baseURL);

function resolveUrl(rawUrl: string, skipProxy: boolean): { url: string; proxied: boolean } {
  if (!rawUrl.startsWith('/')) {
    return { url: rawUrl, proxied: false };
  }

  if (skipProxy) {
    return { url: rawUrl, proxied: false };
  }

  if (
    rawUrl.startsWith('/api/agents') ||
    rawUrl.startsWith('/api/authors') ||
    rawUrl.startsWith('/api/bindings') ||
    rawUrl.startsWith('/api/proxy') ||
    rawUrl.startsWith('/api/wechat')
  ) {
    return { url: rawUrl, proxied: rawUrl.startsWith('/api/proxy') };
  }

  const software = getSelectedSoftwareName();
  if (!software) {
    throw new Error('未选择软件码，无法发起代理请求');
  }

  const normalized = rawUrl.replace(/^\//, '');
  return { url: `/api/proxy/${encodeURIComponent(software)}/${normalized}`, proxied: true };
}

export async function apiRequest<T>(options: RequestOptions<T>): Promise<T> {
  const allowMock = !options.disableMock && mockEnabled && options.mockData;

  if (allowMock) {
    return Promise.resolve(options.mockData);
  }

  let requestUrl: string;
  let proxied = false;
  try {
    const resolved = resolveUrl(options.url, options.skipProxy ?? false);
    requestUrl = resolved.url;
    proxied = resolved.proxied;
  } catch (error) {
    return Promise.reject(error);
  }

  function isApiEnvelope(payload: any): payload is ApiEnvelope<unknown> {
    return (
      payload &&
      typeof payload === 'object' &&
      'code' in payload &&
      typeof payload.code === 'number' &&
      'message' in payload
    );
  }

  return new Promise<T>((resolve, reject) => {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.headers ?? {})
    };

    const shouldAuth = options.auth ?? proxied;
    if (shouldAuth) {
      const role = options.authRole || (options.url.startsWith('/api/authors') ? 'author' : 'agent');
      const token = role === 'author' ? getAuthorToken() : getPlatformToken();
      if (token) {
        headers.Authorization = `Bearer ${token}`;
      }
    }

    if (proxied) {
      const remoteToken = getTokenValue();
      if (remoteToken) {
        headers['X-SProtect-Remote-Token'] = remoteToken;
      }
    }

    const isAbsoluteUrl = /^[a-z][a-z0-9+.-]*:\/\//i.test(requestUrl);
    const finalUrl = isAbsoluteUrl ? requestUrl : `${baseURL}${requestUrl}`;

    uni.request({
      url: finalUrl,
      method: options.method ?? 'GET',
      data: options.data,
      header: headers,
      success: (res) => {
        if (res.statusCode && res.statusCode >= 200 && res.statusCode < 300) {
          const payload = res.data as unknown;

          if (isApiEnvelope(payload)) {
            if (payload.code === 0) {
              resolve(payload.data as T);
              return;
            }

            if (allowMock) {
              resolve(options.mockData as T);
              return;
            }

            const error = new Error(payload.message || '请求失败');
            (error as any).code = payload.code;
            (error as any).response = payload;
            reject(error);
            return;
          }

          resolve(payload as T);
          return;
        }

        if (res.statusCode === 401) {
          if (options.authRole === 'author' || options.url.startsWith('/api/authors')) {
            clearAuthorToken();
          } else {
            clearPlatformToken();
          }
          clearToken();
          uni.$emit('app:unauthorized', { message: '登录已过期，请重新登录' });
        }

        if (allowMock) {
          resolve(options.mockData as T);
          return;
        }

        reject(res);
      },
      fail: (err) => {
        if (allowMock) {
          resolve(options.mockData as T);
        } else {
          reject(err);
        }
      }
    });
  });
}

export function getBaseURL() {
  return baseURL;
}

export function refreshBaseURL() {
  baseURL = resolveApiBaseUrl();
  mockEnabled = isMockMode(baseURL);
  return baseURL;
}

export function setBaseURL(url: string) {
  baseURL = url;
  mockEnabled = isMockMode(baseURL);
}
