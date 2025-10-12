export function downloadTextFile(filename: string, content: string, mime = 'text/plain;charset=utf-8') {
  const normalizedName = filename && filename.trim().length > 0 ? filename.trim() : `export-${Date.now()}.txt`;

  if (typeof window !== 'undefined' && typeof document !== 'undefined') {
    try {
      const blob = new Blob([content], { type: mime });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = normalizedName;
      anchor.style.display = 'none';
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);
      setTimeout(() => URL.revokeObjectURL(url), 0);
      return true;
    } catch (error) {
      console.warn('downloadTextFile fallback', error);
    }
  }

  if (typeof uni !== 'undefined' && uni.setClipboardData) {
    uni.setClipboardData({
      data: content,
      success: () => {
        uni.showToast({ title: '内容已复制', icon: 'success' });
      },
      fail: () => {
        uni.showToast({ title: '导出失败', icon: 'none' });
      }
    });
    return false;
  }

  return false;
}
