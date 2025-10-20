(function initRuntimeConfig(global) {
  const existing = global.__SPROTECT_RUNTIME_CONFIG__ || {};
  global.__SPROTECT_RUNTIME_CONFIG__ = Object.assign(
    {
          apiBaseUrl: 'https://localhost:52623'
          //apiBaseUrl: 'http://localhost:52624'
          //apiBaseUrl: 'https://localhost:5001'
          //apiBaseUrl: 'http://localhost:5000'
          //将apiBaseUrl换成你使用生产环境地址
    },
    existing
  );
})(typeof window !== 'undefined' ? window : globalThis);