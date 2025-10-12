(function initRuntimeConfig(global) {
  const existing = global.__SPROTECT_RUNTIME_CONFIG__ || {};
  global.__SPROTECT_RUNTIME_CONFIG__ = Object.assign(
    {
      /**
       * 在这里设置 API 地址，例如：
       * apiBaseUrl: 'https://example.com/api'
       * 留空则继续使用内置默认值或打包时的环境变量
       */
      apiBaseUrl: ''
    },
    existing
  );
})(typeof window !== 'undefined' ? window : globalThis);
