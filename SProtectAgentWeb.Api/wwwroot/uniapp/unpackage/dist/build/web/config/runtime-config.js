(function initRuntimeConfig(global) {
  const existing = global.__SPROTECT_RUNTIME_CONFIG__ || {};
  global.__SPROTECT_RUNTIME_CONFIG__ = Object.assign(
    {
           apiBaseUrl: 'https://chen.roccloudiot.cn:5001'
    },
    existing
  );
})(typeof window !== 'undefined' ? window : globalThis);