"use strict";
Object.defineProperty(exports, Symbol.toStringTag, { value: "Module" });
const common_vendor = require("./common/vendor.js");
const stores_app = require("./stores/app.js");
if (!Math) {
  "./pages/login/index.js";
  "./pages/login/agent.js";
  "./pages/login/author.js";
  "./pages/author/dashboard.js";
  "./pages/home.js";
  "./pages/agent/bind.js";
  "./pages/agent/settlement.js";
  "./pages/index/index.js";
  "./pages/cards/index.js";
  "./pages/agents/index.js";
  "./pages/links/index.js";
  "./pages/chat/index.js";
  "./pages/verify/index.js";
  "./pages/verify/share.js";
  "./pages/blacklist/logs/index.js";
  "./pages/blacklist/machines/index.js";
}
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "App",
  setup(__props) {
    const themePresets = {
      dark: {
        pageBackground: "radial-gradient(circle at top, rgba(56, 189, 248, 0.18), transparent 65%), radial-gradient(circle at bottom, rgba(99, 102, 241, 0.15), transparent 60%), #05070f",
        textColor: "rgba(226, 232, 240, 0.78)",
        backgroundColor: "#05070f",
        navigationBar: {
          background: "#05070f",
          frontColor: "#ffffff"
        },
        variables: {
          "--app-background": "radial-gradient(circle at top, rgba(56, 189, 248, 0.18), transparent 65%), radial-gradient(circle at bottom, rgba(99, 102, 241, 0.15), transparent 60%), #05070f",
          "--card-background": "rgba(15, 23, 42, 0.72)",
          "--card-border": "rgba(108, 92, 231, 0.25)",
          "--surface-light": "rgba(12, 19, 33, 0.55)",
          "--text-primary": "#f8fafc",
          "--text-secondary": "rgba(226, 232, 240, 0.78)",
          "--text-muted": "rgba(148, 163, 184, 0.75)",
          "--outline-color": "rgba(148, 163, 184, 0.24)",
          "--danger-color": "#f87171",
          "--accent-gradient": "linear-gradient(135deg, rgba(56, 189, 248, 0.85), rgba(99, 102, 241, 0.85))",
          "--color-primary": "#6c5ce7",
          "--color-secondary": "#00f0ff",
          "--color-dark": "#0b1220",
          "--color-darker": "#0a0f1a"
        }
      },
      light: {
        pageBackground: "linear-gradient(180deg, #f7f9fc, #e0ecff)",
        textColor: "#4b5563",
        backgroundColor: "#f7f9fc",
        navigationBar: {
          background: "#f7f9fc",
          frontColor: "#000000"
        },
        variables: {
          "--app-background": "linear-gradient(180deg, #f7f9fc, #e0ecff)",
          "--card-background": "rgba(255, 255, 255, 0.86)",
          "--card-border": "rgba(148, 163, 184, 0.35)",
          "--surface-light": "rgba(255, 255, 255, 0.9)",
          "--text-primary": "#1f2937",
          "--text-secondary": "#4b5563",
          "--text-muted": "#6b7280",
          "--outline-color": "rgba(148, 163, 184, 0.45)",
          "--danger-color": "#dc2626",
          "--accent-gradient": "linear-gradient(135deg, rgba(59, 130, 246, 0.9), rgba(14, 165, 233, 0.9))",
          "--color-primary": "#6c5ce7",
          "--color-secondary": "#0ea5e9",
          "--color-dark": "#0b1220",
          "--color-darker": "#0a0f1a"
        }
      },
      cartoon: {
        pageBackground: "radial-gradient(circle at top left, #ffe08a, transparent 55%), radial-gradient(circle at bottom right, #8ec5fc, transparent 55%), #ffe6f7",
        textColor: "#5b4b8a",
        backgroundColor: "#ffe6f7",
        navigationBar: {
          background: "#ffe6f7",
          frontColor: "#000000"
        },
        variables: {
          "--app-background": "radial-gradient(circle at top left, #ffe08a, transparent 55%), radial-gradient(circle at bottom right, #8ec5fc, transparent 55%), #ffe6f7",
          "--card-background": "rgba(255, 255, 255, 0.92)",
          "--card-border": "rgba(255, 182, 193, 0.6)",
          "--surface-light": "rgba(255, 255, 255, 0.95)",
          "--text-primary": "#1f1f3d",
          "--text-secondary": "#5b4b8a",
          "--text-muted": "#746292",
          "--outline-color": "rgba(255, 182, 193, 0.6)",
          "--danger-color": "#f97316",
          "--accent-gradient": "linear-gradient(135deg, #ff7eb3, #65c7f7)",
          "--color-primary": "#6c5ce7",
          "--color-secondary": "#ff7eb3",
          "--color-dark": "#0b1220",
          "--color-darker": "#0a0f1a"
        }
      }
    };
    const appStore = stores_app.useAppStore();
    const { theme } = common_vendor.storeToRefs(appStore);
    const themeClass = common_vendor.computed(() => `theme-${theme.value}`);
    const activePreset = common_vendor.computed(() => themePresets[theme.value] ?? themePresets.dark);
    const platform = typeof common_vendor.index !== "undefined" && typeof common_vendor.index.getSystemInfoSync === "function" ? common_vendor.index.getSystemInfoSync().uniPlatform : "";
    const isMpWeixin = platform === "mp-weixin";
    const themeVariables = common_vendor.computed(() => {
      const style = {};
      Object.entries(activePreset.value.variables).forEach(([key, value]) => {
        style[key] = value;
      });
      style.background = activePreset.value.pageBackground;
      style["background-color"] = activePreset.value.backgroundColor;
      style.color = activePreset.value.textColor;
      return style;
    });
    const pageStyle = common_vendor.computed(() => {
      const declarations = [
        ...Object.entries(activePreset.value.variables).map(([key, value]) => `${key}:${value}`),
        `background:${activePreset.value.pageBackground}`,
        `background-color:${activePreset.value.backgroundColor}`,
        `color:${activePreset.value.textColor}`,
        "font-family:'Poppins','Noto Sans SC',sans-serif"
      ];
      if (isMpWeixin) {
        declarations.push(`--app-background:${activePreset.value.backgroundColor}`);
      }
      return declarations.join(";");
    });
    function applyThemePreset(preset) {
      var _a, _b;
      if (!preset) {
        return;
      }
      const doc = typeof document !== "undefined" ? document : null;
      if (doc == null ? void 0 : doc.documentElement) {
        const rootStyle = doc.documentElement.style;
        Object.entries(preset.variables).forEach(([key, value]) => {
          rootStyle.setProperty(key, value);
        });
        rootStyle.setProperty("--app-background", preset.variables["--app-background"] ?? preset.pageBackground);
        rootStyle.setProperty("--text-primary", preset.variables["--text-primary"] ?? preset.textColor);
        if (doc.body) {
          doc.body.style.background = preset.pageBackground;
          doc.body.style.color = preset.textColor;
        }
      }
      if (typeof common_vendor.index !== "undefined" && ((_a = common_vendor.index) == null ? void 0 : _a.setBackgroundColor)) {
        try {
          common_vendor.index.setBackgroundColor({
            backgroundColor: preset.backgroundColor,
            backgroundColorTop: preset.backgroundColor,
            backgroundColorBottom: preset.backgroundColor
          });
        } catch (error) {
          common_vendor.index.__f__("warn", "at App.vue:169", "设置背景颜色失败", error);
        }
      }
      if (typeof common_vendor.index !== "undefined" && ((_b = common_vendor.index) == null ? void 0 : _b.setNavigationBarColor)) {
        try {
          common_vendor.index.setNavigationBarColor({
            backgroundColor: preset.navigationBar.background,
            frontColor: preset.navigationBar.frontColor
          });
        } catch (error) {
          common_vendor.index.__f__("warn", "at App.vue:180", "设置标题栏颜色失败", error);
        }
      }
    }
    common_vendor.watch(
      activePreset,
      (preset) => {
        applyThemePreset(preset);
      },
      { immediate: true }
    );
    function handleUnauthorized(event) {
      appStore.handleUnauthorized(event == null ? void 0 : event.message);
    }
    common_vendor.onLaunch(() => {
      common_vendor.index.$on("app:unauthorized", handleUnauthorized);
      appStore.bootstrap().catch((error) => {
        common_vendor.index.__f__("warn", "at App.vue:200", "初始化失败", error);
      });
    });
    common_vendor.onUnmounted(() => {
      common_vendor.index.$off("app:unauthorized", handleUnauthorized);
    });
    return (_ctx, _cache) => {
      return {
        a: pageStyle.value,
        b: common_vendor.n(themeClass.value),
        c: common_vendor.s(themeVariables.value)
      };
    };
  }
});
function createApp() {
  const app = common_vendor.createSSRApp(_sfc_main);
  const pinia = common_vendor.createPinia();
  app.use(pinia);
  return {
    app,
    pinia
  };
}
createApp().app.mount("#app");
exports.createApp = createApp;
//# sourceMappingURL=../.sourcemap/mp-weixin/app.js.map
