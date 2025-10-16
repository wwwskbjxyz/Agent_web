"use strict";
const common_vendor = require("../common/vendor.js");
function downloadTextFile(filename, content, mime = "text/plain;charset=utf-8") {
  filename && filename.trim().length > 0 ? filename.trim() : `export-${Date.now()}.txt`;
  if (typeof common_vendor.index !== "undefined" && common_vendor.index.setClipboardData) {
    common_vendor.index.setClipboardData({
      data: content,
      success: () => {
        common_vendor.index.showToast({ title: "内容已复制", icon: "success" });
      },
      fail: () => {
        common_vendor.index.showToast({ title: "导出失败", icon: "none" });
      }
    });
    return false;
  }
  return false;
}
exports.downloadTextFile = downloadTextFile;
//# sourceMappingURL=../../.sourcemap/mp-weixin/utils/download.js.map
