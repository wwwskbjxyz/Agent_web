"use strict";
const common_vendor = require("../common/vendor.js");
const _sfc_main = /* @__PURE__ */ common_vendor.defineComponent({
  __name: "DataTable",
  props: {
    title: {},
    subtitle: { default: "" },
    columns: {},
    rows: {},
    loading: { type: Boolean, default: false },
    layout: { default: "table" },
    primaryColumn: { default: "" },
    operationsColumn: { default: "" },
    collapseDetails: { type: Boolean, default: true }
  },
  setup(__props) {
    const props = __props;
    const columnCount = common_vendor.computed(() => {
      var _a;
      return ((_a = props.columns) == null ? void 0 : _a.length) ? props.columns.length : 1;
    });
    const rowStyle = common_vendor.computed(() => ({
      gridTemplateColumns: `repeat(${columnCount.value}, minmax(200rpx, auto))`
    }));
    const wrapperStyle = common_vendor.computed(() => ({
      "--column-count": columnCount.value,
      minWidth: `${Math.max(columnCount.value * 220, 600)}rpx`
    }));
    const layout = common_vendor.computed(() => props.layout ?? "table");
    const layoutClass = common_vendor.computed(() => layout.value === "stack" ? "table--stack" : "table--grid");
    const primaryKey = common_vendor.computed(() => {
      var _a, _b;
      if (props.primaryColumn) {
        return props.primaryColumn;
      }
      return ((_b = (_a = props.columns) == null ? void 0 : _a[0]) == null ? void 0 : _b.key) ?? "";
    });
    const operationsKey = common_vendor.computed(() => {
      var _a;
      if (props.operationsColumn) {
        return props.operationsColumn;
      }
      const hasOperations = (_a = props.columns) == null ? void 0 : _a.some((column) => column.key === "operations");
      return hasOperations ? "operations" : "";
    });
    const slots = common_vendor.useSlots();
    const hasOperationsSlot = common_vendor.computed(() => {
      const key = operationsKey.value;
      if (!key)
        return false;
      return !!slots[key];
    });
    const detailColumns = common_vendor.computed(
      () => (props.columns || []).filter(
        (column) => column.key !== primaryKey.value && column.key !== operationsKey.value
      )
    );
    const collapseDetails = common_vendor.computed(() => !!props.collapseDetails);
    const expandedRows = common_vendor.ref({});
    common_vendor.watch(
      () => props.rows,
      () => {
        expandedRows.value = {};
      }
    );
    function getRowId(row, index) {
      const key = primaryKey.value;
      if (key && row && row[key] != null) {
        return String(row[key]);
      }
      return `row-${index}`;
    }
    function toggleRow(id) {
      expandedRows.value[id] = !expandedRows.value[id];
    }
    function isExpanded(id) {
      return !!expandedRows.value[id];
    }
    function formatValue(value) {
      if (value === void 0 || value === null || value === "") {
        return "—";
      }
      if (typeof value === "string" || typeof value === "number") {
        return value;
      }
      if (Array.isArray(value)) {
        return value.join(", ");
      }
      if (typeof value === "boolean") {
        return value ? "是" : "否";
      }
      return String(value);
    }
    return (_ctx, _cache) => {
      return common_vendor.e({
        a: common_vendor.t(_ctx.title),
        b: _ctx.subtitle
      }, _ctx.subtitle ? {
        c: common_vendor.t(_ctx.subtitle)
      } : {}, {
        d: layout.value === "table"
      }, layout.value === "table" ? common_vendor.e({
        e: common_vendor.f(_ctx.columns, (column, k0, i0) => {
          return {
            a: common_vendor.t(column.label),
            b: column.key,
            c: common_vendor.s(column.style)
          };
        }),
        f: common_vendor.s(rowStyle.value),
        g: _ctx.loading
      }, _ctx.loading ? {} : !_ctx.rows.length ? {} : {
        i: common_vendor.f(_ctx.rows, (row, rowIndex, i0) => {
          return {
            a: common_vendor.f(_ctx.columns, (column, k1, i1) => {
              return {
                a: common_vendor.t(formatValue(row[column.key])),
                b: common_vendor.d(column.key),
                c: common_vendor.r(common_vendor.d(column.key), {
                  row
                }, i0 + "-" + i1),
                d: column.key,
                e: common_vendor.s(column.style)
              };
            }),
            b: rowIndex
          };
        }),
        j: common_vendor.s(rowStyle.value)
      }, {
        h: !_ctx.rows.length,
        k: common_vendor.s(wrapperStyle.value)
      }) : common_vendor.e({
        l: _ctx.loading
      }, _ctx.loading ? {} : !_ctx.rows.length ? {} : {
        n: common_vendor.f(_ctx.rows, (row, rowIndex, i0) => {
          return common_vendor.e({
            a: common_vendor.t(formatValue(primaryKey.value ? row[primaryKey.value] : "")),
            b: common_vendor.r(common_vendor.d(primaryKey.value), {
              row
            }, i0)
          }, detailColumns.value.length && collapseDetails.value ? {
            c: common_vendor.t(isExpanded(getRowId(row, rowIndex)) ? "收起" : "详情"),
            d: common_vendor.o(($event) => toggleRow(getRowId(row, rowIndex)), getRowId(row, rowIndex))
          } : {}, hasOperationsSlot.value ? common_vendor.e({
            e: operationsKey.value === "operations"
          }, operationsKey.value === "operations" ? {
            f: common_vendor.t(formatValue(row[operationsKey.value])),
            g: "operations-" + i0,
            h: common_vendor.r("operations", {
              row
            }, i0)
          } : {
            i: common_vendor.t(formatValue(row[operationsKey.value])),
            j: common_vendor.d(operationsKey.value),
            k: common_vendor.r(common_vendor.d(operationsKey.value), {
              row
            }, i0)
          }) : {}, {
            l: !collapseDetails.value || isExpanded(getRowId(row, rowIndex))
          }, !collapseDetails.value || isExpanded(getRowId(row, rowIndex)) ? {
            m: common_vendor.f(detailColumns.value, (column, k1, i1) => {
              return {
                a: common_vendor.t(column.label),
                b: common_vendor.t(formatValue(row[column.key])),
                c: common_vendor.d(column.key),
                d: common_vendor.r(common_vendor.d(column.key), {
                  row
                }, i0 + "-" + i1),
                e: column.key
              };
            })
          } : {}, {
            n: getRowId(row, rowIndex)
          });
        }),
        o: common_vendor.d(primaryKey.value),
        p: detailColumns.value.length && collapseDetails.value,
        q: hasOperationsSlot.value
      }, {
        m: !_ctx.rows.length
      }), {
        r: common_vendor.n(layoutClass.value)
      });
    };
  }
});
const Component = /* @__PURE__ */ common_vendor._export_sfc(_sfc_main, [["__scopeId", "data-v-5fa18d52"]]);
wx.createComponent(Component);
//# sourceMappingURL=../../.sourcemap/mp-weixin/components/DataTable.js.map
