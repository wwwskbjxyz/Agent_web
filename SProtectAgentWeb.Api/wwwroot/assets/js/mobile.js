(() => {
  const processedTables = new WeakMap();

  function toInt(value) {
    const n = parseInt(value, 10);
    return Number.isFinite(n) ? n : null;
  }

  function parseIndices(value, fallback) {
    if (typeof value !== 'string' || !value.trim()) {
      return Array.isArray(fallback) ? fallback.slice() : [];
    }
    return value
      .split(',')
      .map(item => toInt(item.trim()))
      .filter(idx => idx != null && idx >= 0);
  }

  function defaultPrimaryIndices(count) {
    if (!Number.isFinite(count) || count <= 0) {
      return [0];
    }
    const length = Math.min(count, 2);
    return Array.from({ length }, (_, idx) => idx);
  }

  function getHeaders(table) {
    if (!table || !table.tHead) return [];
    const rows = Array.from(table.tHead.rows || []);
    if (!rows.length) return [];
    const headerRow = rows[rows.length - 1];
    return Array.from(headerRow.cells || []).map(cell => (cell.textContent || '').trim());
  }

  let detailModal = null;
  let detailTitleEl = null;
  let detailBodyEl = null;
  let detailActionsEl = null;
  let lastFocusedElement = null;

  function ensureDetailModal() {
    if (detailModal) return;
    detailModal = document.createElement('div');
    detailModal.id = 'mobile-detail-modal';
    detailModal.className = 'modal-backdrop mobile-detail-backdrop';
    detailModal.setAttribute('role', 'dialog');
    detailModal.setAttribute('aria-modal', 'true');
    detailModal.setAttribute('aria-hidden', 'true');
    detailModal.innerHTML = `
      <div class="modal glass rounded-2xl p-5 mobile-detail-modal-content" role="document">
        <div class="flex items-center justify-between gap-3 mb-4">
          <div class="text-lg font-semibold text-white" data-mobile-detail-title id="mobile-detail-modal-title">详细信息</div>
          <button type="button" class="text-gray-400 hover:text-white" data-mobile-detail-close aria-label="关闭详情">
            <i class="fa fa-times"></i>
          </button>
        </div>
        <div class="mobile-detail-content" data-mobile-detail-body></div>
        <div class="mobile-detail-actions" data-mobile-detail-actions></div>
      </div>
    `;
    document.body.appendChild(detailModal);
    detailTitleEl = detailModal.querySelector('[data-mobile-detail-title]');
    detailBodyEl = detailModal.querySelector('[data-mobile-detail-body]');
    detailActionsEl = detailModal.querySelector('[data-mobile-detail-actions]');

    detailModal.addEventListener('click', (event) => {
      if (event.target === detailModal) {
        closeDetail();
      }
    });
    const closeBtn = detailModal.querySelector('[data-mobile-detail-close]');
    if (closeBtn) {
      closeBtn.addEventListener('click', () => closeDetail());
    }
    document.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && detailModal?.classList.contains('active')) {
        closeDetail();
      }
    });
  }

  function closeDetail() {
    if (!detailModal) return;
    if (!detailModal.classList.contains('active')) {
      detailModal.style.display = 'none';
      detailModal.setAttribute('aria-hidden', 'true');
      return;
    }
    detailModal.classList.remove('active');
    detailModal.setAttribute('aria-hidden', 'true');
    setTimeout(() => {
      if (detailModal && !detailModal.classList.contains('active')) {
        detailModal.style.display = 'none';
        if (lastFocusedElement && typeof lastFocusedElement.focus === 'function') {
          try {
            lastFocusedElement.focus({ preventScroll: true });
          } catch (err) {
            /* ignore focus errors */
          }
        }
        lastFocusedElement = null;
      }
    }, 180);
  }

  function isPromise(value) {
    return value && typeof value.then === 'function';
  }

  function createActionButton(action, options = {}) {
    if (!action) return null;
    const elementName = action.element || 'button';
    const element = document.createElement(elementName);
    if (elementName !== 'button') {
      if (elementName === 'a') {
        if (action.href) element.href = action.href;
        if (action.target) element.target = action.target;
        if (action.rel) element.rel = action.rel;
      }
    } else {
      element.type = 'button';
    }
    element.className = action.className || 'glass rounded-lg px-3 py-2 text-sm';
    if (action.html) {
      element.innerHTML = action.html;
    } else {
      element.textContent = action.label || action.text || '操作';
    }
    if (action.attributes && typeof action.attributes === 'object') {
      Object.entries(action.attributes).forEach(([key, value]) => {
        if (value != null) {
          element.setAttribute(key, value);
        }
      });
    }
    if (typeof action.onClick === 'function') {
      element.addEventListener('click', (event) => {
        try {
          const result = action.onClick(event);
          if (options.closeDetail && action.closeOnClick !== false) {
            if (isPromise(result)) {
              result.finally(() => closeDetail());
            } else {
              closeDetail();
            }
          }
        } catch (err) {
          console.error('mobile action handler error:', err);
          if (options.closeDetail && action.closeOnClick !== false) {
            closeDetail();
          }
        }
      });
    }
    return element;
  }

  function renderActions(container, actions, options = {}) {
    if (!container || !Array.isArray(actions)) return;
    if (!container.dataset.mobileDisplay) {
      container.dataset.mobileDisplay = window.getComputedStyle(container).display || 'flex';
    }
    container.innerHTML = '';
    if (!actions.length) {
      container.style.display = 'none';
      return;
    }
    container.style.display = container.dataset.mobileDisplay;
    actions.forEach(action => {
      const button = createActionButton(action, options);
      if (button) {
        container.appendChild(button);
      }
    });
  }

  function showDetail(config) {
    ensureDetailModal();
    const title = (config && config.title) || '详细信息';
    const items = Array.isArray(config?.items) ? config.items : [];
    const actions = Array.isArray(config?.actions) ? config.actions : [];

    detailTitleEl.textContent = title;
    const modalContent = detailModal.querySelector('.mobile-detail-modal-content');
    if (modalContent) {
      modalContent.setAttribute('aria-labelledby', detailTitleEl.id || 'mobile-detail-modal-title');
    }
    detailBodyEl.innerHTML = '';

    if (!items.length) {
      const empty = document.createElement('div');
      empty.className = 'text-sm text-gray-400 text-center py-4';
      empty.textContent = '暂无更多信息';
      detailBodyEl.appendChild(empty);
    } else {
      items.forEach(item => {
        if (!item) return;
        const wrap = document.createElement('div');
        wrap.className = 'mobile-detail-item';
        const label = document.createElement('div');
        label.className = 'mobile-detail-label';
        label.textContent = item.label || '';
        const value = document.createElement('div');
        value.className = 'mobile-detail-value';
        value.innerHTML = item.content || '';
        wrap.appendChild(label);
        wrap.appendChild(value);
        detailBodyEl.appendChild(wrap);
      });
    }

    renderActions(detailActionsEl, actions, { closeDetail: true });

    lastFocusedElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    detailModal.style.display = 'flex';
    requestAnimationFrame(() => {
      detailModal.classList.add('active');
      detailModal.setAttribute('aria-hidden', 'false');
      const closeBtn = detailModal.querySelector('[data-mobile-detail-close]');
      if (closeBtn && typeof closeBtn.focus === 'function') {
        try {
          closeBtn.focus({ preventScroll: true });
        } catch (err) {
          /* ignore focus errors */
        }
      }
    });
  }

  function prepareTable(table) {
    if (!table || processedTables.has(table)) {
      const existing = processedTables.get(table);
      return existing ? existing.api : undefined;
    }

    const state = {
      table,
      headers: getHeaders(table),
      primary: [],
      actionColumns: [],
      titleIndex: 0,
      detailLabel: table.dataset.mobileDetailLabel || '详细',
      breakpoint: toInt(table.dataset.mobileBreakpoint) || 900,
      isMobile: false
    };

    const primary = parseIndices(table.dataset.mobilePrimary, defaultPrimaryIndices(state.headers.length));
    state.primary = primary.length ? primary : defaultPrimaryIndices(state.headers.length);
    state.actionColumns = parseIndices(table.dataset.mobileActions, []);
    const explicitTitle = toInt(table.dataset.mobileTitleCol);
    state.titleIndex = explicitTitle != null ? explicitTitle : (state.primary[0] ?? 0);

    function updateHeaders(isMobile) {
      const headRows = table.tHead ? Array.from(table.tHead.rows || []) : [];
      headRows.forEach(row => {
        Array.from(row.cells || []).forEach((cell, idx) => {
          const shouldHide = isMobile && !state.primary.includes(idx) && !state.actionColumns.includes(idx);
          cell.classList.toggle('mobile-hidden-cell', shouldHide);
        });
      });
    }

    function resetRow(row) {
      if (!row) return;
      const detailCell = row.querySelector('td.mobile-detail-cell');
      if (detailCell) {
        detailCell.remove();
      }
      Array.from(row.cells || []).forEach(cell => {
        cell.classList.remove('mobile-hidden-cell', 'mobile-primary-cell');
      });
      row.classList.remove('mobile-row-ready');
    }

    function buildDetailItems(row, cells) {
      if (Array.isArray(row._mobileDetailItems) && row._mobileDetailItems.length) {
        cells.forEach((cell, idx) => {
          if (state.primary.includes(idx) || state.actionColumns.includes(idx)) {
            return;
          }
          cell.classList.add('mobile-hidden-cell');
        });
        return row._mobileDetailItems.map(item => ({
          label: item?.label || '',
          content: item?.content || ''
        }));
      }
      const items = [];
      cells.forEach((cell, idx) => {
        if (state.primary.includes(idx) || state.actionColumns.includes(idx)) {
          return;
        }
        cell.classList.add('mobile-hidden-cell');
        const label = state.headers[idx] || cell.getAttribute('data-title') || `字段${idx + 1}`;
        items.push({ label, content: cell.innerHTML || '' });
      });
      return items;
    }

    function gatherActions(row, cells) {
      const defined = Array.isArray(row._mobileActions) ? row._mobileActions.slice() : [];
      if (defined.length) {
        return defined;
      }
      if (!state.actionColumns.length) {
        return [];
      }
      const actions = [];
      state.actionColumns.forEach(idx => {
        const cell = cells[idx];
        if (!cell) return;
        cell.classList.add('mobile-hidden-cell');
        const html = cell.innerHTML;
        if (html && html.trim()) {
          actions.push({ html });
        }
      });
      return actions;
    }

    function attachDetailCell(row, detailItems, actions) {
      if (!detailItems.length && !actions.length) {
        return;
      }
      if (row._mobileInlineDetail) {
        row.classList.add('mobile-row-ready');
        return;
      }
      let detailCell = document.createElement('td');
      detailCell.className = 'mobile-detail-cell px-4 py-3';
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'mobile-detail-btn glass rounded-lg px-3 py-2 text-sm';
      button.textContent = state.detailLabel;
      button.addEventListener('click', (event) => {
        event.stopPropagation();
        const titleCell = row.cells[state.titleIndex];
        const fallbackTitle = (titleCell ? titleCell.textContent : '') || '';
        const title = (row.dataset.mobileTitle || fallbackTitle || '详细信息').trim();
        showDetail({ title, items: detailItems, actions });
      }, { passive: true });
      detailCell.appendChild(button);
      row.appendChild(detailCell);
      row.classList.add('mobile-row-ready');
    }

    function prepareRow(row) {
      if (!row || !row.cells || row.cells.length === 0) return;
      const firstCell = row.cells[0];
      if (firstCell && firstCell.colSpan && firstCell.colSpan > 1) {
        return;
      }
      resetRow(row);
      const cells = Array.from(row.cells || []);
      cells.forEach((cell, idx) => {
        if (state.primary.includes(idx)) {
          cell.classList.add('mobile-primary-cell');
        }
      });
      const detailItems = buildDetailItems(row, cells);
      const actions = gatherActions(row, cells);
      attachDetailCell(row, detailItems, actions);
    }

    let mutationDepth = 0;
    let refreshQueued = false;

    function withMutationLock(fn) {
      mutationDepth += 1;
      try {
        return fn();
      } finally {
        mutationDepth -= 1;
      }
    }

    function prepareAllRows() {
      withMutationLock(() => {
        const bodies = table.tBodies ? Array.from(table.tBodies || []) : [];
        bodies.forEach(body => {
          Array.from(body.rows || []).forEach(row => {
            prepareRow(row);
          });
        });
      });
    }

    function resetAllRows() {
      withMutationLock(() => {
        const bodies = table.tBodies ? Array.from(table.tBodies || []) : [];
        bodies.forEach(body => {
          Array.from(body.rows || []).forEach(row => resetRow(row));
        });
      });
    }

    function queuePrepareAllRows() {
      if (!state.isMobile || refreshQueued) return;
      refreshQueued = true;
      requestAnimationFrame(() => {
        refreshQueued = false;
        if (state.isMobile) {
          prepareAllRows();
        }
      });
    }

    function activateMobile() {
      if (state.isMobile) return;
      state.isMobile = true;
      table.classList.add('mobile-table-active');
      updateHeaders(true);
      prepareAllRows();
    }

    function deactivateMobile() {
      if (!state.isMobile) return;
      state.isMobile = false;
      table.classList.remove('mobile-table-active');
      updateHeaders(false);
      resetAllRows();
    }

    function refreshHeaders() {
      state.headers = getHeaders(table);
    }

    const observer = new MutationObserver(() => {
      if (!state.isMobile || mutationDepth > 0) return;
      refreshHeaders();
      queuePrepareAllRows();
    });
    observer.observe(table, { childList: true, subtree: true });

    const mediaQuery = window.matchMedia(`(max-width: ${state.breakpoint}px)`);
    const onChange = () => {
      refreshHeaders();
      if (mediaQuery.matches) {
        activateMobile();
      } else {
        deactivateMobile();
      }
    };
    mediaQuery.addEventListener('change', onChange);
    onChange();

    const api = {
      refresh() {
        refreshHeaders();
        if (state.isMobile) {
          prepareAllRows();
        }
      },
      destroy() {
        observer.disconnect();
        mediaQuery.removeEventListener('change', onChange);
        processedTables.delete(table);
      }
    };

    processedTables.set(table, { state, api });
    return api;
  }

  document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('table[data-mobile-table]').forEach(table => prepareTable(table));
  });

  window.MobileFriendly = {
    registerTable: prepareTable,
    refreshTable(table) {
      const record = processedTables.get(table);
      if (record) {
        record.api.refresh();
      }
    },
    renderActions,
    createActionButton,
    showDetail,
    closeDetail
  };
})();
