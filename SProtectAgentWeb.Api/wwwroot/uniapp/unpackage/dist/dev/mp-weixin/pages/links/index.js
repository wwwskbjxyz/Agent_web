"use strict";
const common_vendor = require("../../common/vendor.js");
const stores_app = require("../../stores/app.js");
if (!Math) {
  (SoftwarePicker + DataTable)();
}
const DataTable = () => "../../components/DataTable.js";
const SoftwarePicker = () => "../../components/SoftwarePicker.js";
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "index",
  setup(__props) {
    const appStore = stores_app.useAppStore();
    const { linkRecords, selectedSoftware } = common_vendor.storeToRefs(appStore);
    const columns = [
      { key: "id", label: "编号", style: "min-width:140rpx" },
      { key: "url", label: "蓝奏云链接", style: "min-width:260rpx" },
      { key: "extractionCode", label: "提取码", style: "min-width:140rpx" },
      { key: "createdAt", label: "抓取时间", style: "min-width:200rpx" },
      { key: "content", label: "备注信息", style: "min-width:240rpx" }
    ];
    const rows = common_vendor.computed(
      () => linkRecords.value.map((item) => ({
        id: item.id,
        url: item.url,
        extractionCode: item.extractionCode || "—",
        createdAt: item.createdAt,
        content: item.content || ""
      }))
    );
    const loading = common_vendor.computed(() => appStore.loading.links);
    common_vendor.onMounted(async () => {
      await appStore.ensureReady();
      await appStore.loadLinkRecords();
    });
    common_vendor.watch(
      () => selectedSoftware.value,
      (next, prev) => {
        if (!next || next === prev)
          return;
        appStore.loadLinkRecords();
      }
    );
    function createLink() {
      common_vendor.index.showToast({ title: "请接入联动创建接口", icon: "none" });
    }
    function copy(text) {
      common_vendor.index.setClipboardData({ data: text, success: () => common_vendor.index.showToast({ title: "链接已复制", icon: "success" }) });
    }
    return (_ctx, _cache) => {
      return {
        a: common_vendor.o(createLink),
        b: common_vendor.w(({
          row
        }, s0, i0) => {
          return {
            a: common_vendor.t(row.url),
            b: common_vendor.o(($event) => copy(row.url)),
            c: i0,
            d: s0
          };
        }, {
          name: "url",
          path: "b",
          vueId: "82002cb0-1"
        }),
        c: common_vendor.w(({
          row
        }, s0, i0) => {
          return {
            a: common_vendor.t(row.content || "—"),
            b: i0,
            c: s0
          };
        }, {
          name: "content",
          path: "c",
          vueId: "82002cb0-1"
        }),
        d: common_vendor.p({
          title: "蓝奏云记录",
          subtitle: "包含链接、提取码及创建时间",
          columns,
          rows: rows.value,
          loading: loading.value
        })
      };
    };
  }
});
const MiniProgramPage = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-82002cb0"]]);
wx.createPage(MiniProgramPage);
//# sourceMappingURL=../../../.sourcemap/mp-weixin/pages/links/index.js.map
