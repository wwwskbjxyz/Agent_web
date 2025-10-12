import { isMockMode, resolveApiBaseUrl } from '@/utils/env';
import { clearToken, getTokenValue } from '@/utils/storage';

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
}

let baseURL = resolveApiBaseUrl();
let mockEnabled = isMockMode(baseURL);

export async function apiRequest<T>(options: RequestOptions<T>): Promise<T> {
  const allowMock = !options.disableMock && mockEnabled && options.mockData;

  if (allowMock) {
    return Promise.resolve(options.mockData);
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

    if (options.auth) {
      const token = getTokenValue();
      if (token) {
        headers.Authorization = `Bearer ${token}`;
      }
    }

    uni.request({
      url: `${baseURL}${options.url}`,
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
