(function initRuntimeConfig(global) {
  const existing = global.__SPROTECT_RUNTIME_CONFIG__ || {};
  global.__SPROTECT_RUNTIME_CONFIG__ = Object.assign(
    {
           apiBaseUrl: 'https://localhost:52623'
    },
    existing
  );
})(typeof window !== 'undefined' ? window : globalThis);