/* ============================================================
   GhostCleaner UI 0.1 — 可点击原型
   Hash 路由 / 主题切换 / 结果列表 / 详情面板 / 对话框 / Toast
   ============================================================ */
(function () {
  'use strict';

  /* ---------------- 工具 ---------------- */
  const $ = (s, r) => (r || document).querySelector(s);
  const $$ = (s, r) => Array.from((r || document).querySelectorAll(s));
  const icon = (id, cls) => `<svg class="gc-icon ${cls || ''}" viewBox="0 0 24 24"><use href="#${id}"/></svg>`;
  const esc = (s) => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  const fmtInt = (n) => n.toLocaleString('en-US');

  /* ---------------- 主题 ---------------- */
  const THEME_KEY = 'gc-theme';
  const media = window.matchMedia('(prefers-color-scheme: dark)');

  function resolveTheme(mode) {
    if (mode === 'system') return media.matches ? 'dark' : 'light';
    return mode;
  }
  function applyTheme(mode) {
    localStorage.setItem(THEME_KEY, mode);
    document.documentElement.setAttribute('data-theme', resolveTheme(mode));
    $('#themeIconUse').setAttribute('href', '#' + (resolveTheme(mode) === 'dark' ? 'gc-sun' : 'gc-moon'));
    $$('#themeOptions .theme-opt').forEach(b => b.setAttribute('aria-pressed', String(b.dataset.themeSet === mode)));
  }
  function currentMode() { return localStorage.getItem(THEME_KEY) || 'system'; }
  applyTheme(currentMode());
  media.addEventListener('change', () => { if (currentMode() === 'system') applyTheme('system'); });

  $('#themeBtn').addEventListener('click', () => {
    const order = ['system', 'light', 'dark'];
    const next = order[(order.indexOf(currentMode()) + 1) % order.length];
    applyTheme(next);
    const label = { system: '跟随系统', light: '浅色', dark: '深色' }[next];
    toast('info', '主题已切换为「' + label + '」');
  });
  $$('#themeOptions .theme-opt').forEach(b => {
    b.addEventListener('click', () => { applyTheme(b.dataset.themeSet); toast('info', '主题已应用'); });
  });

  /* ---------------- 路由 ---------------- */
  const PAGES = ['dashboard', 'scan', 'scanning', 'complete', 'results', 'quarantine', 'history', 'settings', 'about'];
  function navigate(route) {
    if (!PAGES.includes(route)) route = 'dashboard';
    location.hash = '#/' + route;
  }
  function renderRoute() {
    const route = (location.hash || '').replace('#/', '') || 'dashboard';
    $$('.page').forEach(p => p.classList.remove('active'));
    const page = $('#page-' + route);
    if (page) page.classList.add('active');
    $$('#nav .nav-item').forEach(b => b.classList.toggle('active', b.dataset.route === route));
    $('#main').scrollTop = 0;
  }
  window.addEventListener('hashchange', renderRoute);
  $$('[data-nav]').forEach(el => el.addEventListener('click', () => navigate(el.dataset.nav)));
  $$('#nav .nav-item').forEach(b => b.addEventListener('click', () => navigate(b.dataset.route)));
  renderRoute();

  /* ---------------- Toast ---------------- */
  function toast(type, msg, actionText, actionFn) {
    const el = document.createElement('div');
    el.className = 'toast ' + type;
    const icoId = { success: 'gc-circle-check', warning: 'gc-alert-triangle', danger: 'gc-alert-circle', info: 'gc-info' }[type];
    el.innerHTML = icon(icoId, 'i16') + '<span class="toast-msg">' + esc(msg) + '</span>';
    if (actionText) {
      const a = document.createElement('button');
      a.className = 'btn btn-subtle sm toast-action';
      a.textContent = actionText;
      a.addEventListener('click', () => { if (actionFn) actionFn(); dismiss(); });
      el.appendChild(a);
    }
    $('#toastLayer').appendChild(el);
    const timer = setTimeout(dismiss, actionText ? 6000 : 4000);
    function dismiss() {
      clearTimeout(timer);
      el.classList.add('out');
      setTimeout(() => el.remove(), 220);
    }
    while ($('#toastLayer').children.length > 3) $('#toastLayer').firstChild.remove();
  }

  /* ---------------- 对话框 ---------------- */
  let dialogEscHandler = null;
  function showDialog(opt) {
    const layer = $('#dialogLayer');
    const scrim = $('#scrim');
    const d = document.createElement('div');
    d.className = 'dialog ' + (opt.size || '');
    d.innerHTML =
      '<div class="dialog-head ' + (opt.tone || 'accent') + '">' + icon(opt.icon || 'gc-info') +
      '<div class="dialog-title">' + esc(opt.title) + '</div></div>' +
      '<div class="dialog-body">' + opt.body + '</div>' +
      (opt.requireText
        ? '<div class="confirm-input"><span class="label">请输入 <code>' + esc(opt.requireText) + '</code> 以继续</span>' +
          '<input class="input" id="confirmInput" placeholder="' + esc(opt.requireText) + '" autocomplete="off"/></div>'
        : '') +
      '<div class="dialog-foot">' +
      '<button class="btn btn-secondary" data-act="cancel">' + esc(opt.cancelText || '取消') + '</button>' +
      '<button class="btn ' + (opt.danger ? 'btn-danger' : 'btn-primary') + '" data-act="ok"' + (opt.requireText ? ' disabled' : '') + '>' +
      esc(opt.confirmText || '确认') + '</button></div>';
    layer.innerHTML = '';
    layer.appendChild(d);
    layer.classList.add('show');
    scrim.classList.add('show');

    const okBtn = $('[data-act="ok"]', d);
    if (opt.requireText) {
      const input = $('#confirmInput', d);
      input.addEventListener('input', () => { okBtn.disabled = input.value.trim() !== opt.requireText; });
      setTimeout(() => input.focus(), 50);
    } else {
      setTimeout(() => okBtn.focus(), 50);
    }
    $('[data-act="cancel"]', d).addEventListener('click', closeDialog);
    okBtn.addEventListener('click', () => { closeDialog(); if (opt.onConfirm) opt.onConfirm(); });
    scrim.onclick = opt.danger ? null : closeDialog;
    dialogEscHandler = opt.danger ? null : closeDialog;
  }
  function closeDialog() {
    $('#dialogLayer').classList.remove('show');
    $('#dialogLayer').innerHTML = '';
    $('#scrim').classList.remove('show');
    dialogEscHandler = null;
  }
  document.addEventListener('keydown', e => { if (e.key === 'Escape' && dialogEscHandler) dialogEscHandler(); });

  /* ============================================================
     扫描结果数据（与 Core 模型字段一一对应）
     ============================================================ */
  const ITEMS = [
    {
      id: 'i1', name: 'ExampleApp', path: 'D:\\Program Files\\ExampleApp', size: '1.42 GB', sizeBytes: 1.42e9,
      conf: 92, risk: 'Low', protected: false, excluded: false, category: null,
      lastModified: '2024-10-02 09:14', fileCount: 1284, dirCount: 96, version: '3.4.1', publisher: 'Example Inc.',
      rec: 'Quarantine', recText: '建议人工确认后隔离',
      pos: [
        ['未发现卸载项', 'HKLM / HKCU（含 32 位视图）卸载项中查不到该目录', 25],
        ['未发现 App Paths', 'App Paths 中没有指向该目录的条目', 15],
        ['未发现快捷方式', '开始菜单、桌面、启动目录均无指向该目录的快捷方式', 12],
        ['未发现相关服务', 'Windows 服务 ImagePath 中无命中', 10],
        ['未发现计划任务', '计划任务 XML 中无命中', 10],
        ['当前没有相关进程', '运行进程的可执行文件路径无命中', 8],
        ['长时间未修改', '已 731 天未修改', 12]
      ],
      neg: [], warnings: []
    },
    {
      id: 'i2', name: 'OldToolSuite', path: 'E:\\Software\\OldToolSuite', size: '3.21 GB', sizeBytes: 3.21e9,
      conf: 88, risk: 'Medium', protected: false, excluded: false, category: null,
      lastModified: '2024-06-18 17:52', fileCount: 4120, dirCount: 214, version: '2.0.7', publisher: 'OldTool',
      rec: 'Review', recText: '建议人工确认',
      pos: [
        ['未发现卸载项', 'HKLM / HKCU 卸载项中查不到该目录', 25],
        ['未发现快捷方式', '开始菜单与桌面均无指向该目录的快捷方式', 12],
        ['未发现计划任务', '计划任务 XML 中无命中', 10],
        ['长时间未修改', '已 476 天未修改', 12],
        ['存在卸载程序', '发现 unins000.exe', 25]
      ],
      neg: [
        ['发现用户配置文件', 'config\\settings.json 疑似保存了用户配置', -30],
        ['含可能重要的用户数据', '检测到 userdata\\ 目录', -25]
      ], warnings: []
    },
    {
      id: 'i3', name: 'MediaConverter', path: 'E:\\Software\\MediaConverter', size: '742 MB', sizeBytes: 7.42e8,
      conf: 54, risk: 'Medium', protected: false, excluded: false, category: null,
      lastModified: '2025-03-11 20:33', fileCount: 856, dirCount: 42, version: null, publisher: null,
      rec: 'Review', recText: '建议人工确认',
      pos: [
        ['未发现卸载项', 'HKLM / HKCU 卸载项中查不到该目录', 25],
        ['长时间未修改', '已 575 天未修改', 12]
      ],
      neg: [
        ['媒体文件占比过高', '媒体文件占比 78%，更像媒体库而非软件目录', -30],
        ['可能是便携式软件', '无需安装即可运行，天然没有系统关联', -20]
      ], warnings: []
    },
    {
      id: 'i4', name: 'DevToolkit', path: 'E:\\Projects\\DevToolkit', size: '2.10 GB', sizeBytes: 2.1e9,
      conf: 71, risk: 'High', protected: true, excluded: false, category: 'DevelopmentProject',
      protectionReason: '疑似开发项目：发现 .git / package.json / node_modules',
      lastModified: '2026-10-06 22:41', fileCount: 28410, dirCount: 3120, version: null, publisher: null,
      rec: 'HighRiskReview', recText: '高风险，必须人工复核',
      pos: [
        ['未发现卸载项', 'HKLM / HKCU 卸载项中查不到该目录', 25],
        ['未发现快捷方式', '开始菜单与桌面均无命中', 12]
      ],
      neg: [
        ['疑似开发项目', '发现 .git / package.json / node_modules', -45],
        ['检测到最近修改', '昨天 22:41 仍有写入', -25],
        ['发现用户数据', '检测到 src\\ 与用户配置', -30]
      ],
      warnings: ['该目录含有活跃开发痕迹，强烈建议保留']
    },
    {
      id: 'i5', name: 'SomeGame', path: 'D:\\Games\\SteamLibrary\\SomeGame', size: '12.4 GB', sizeBytes: 1.24e10,
      conf: 46, risk: 'High', protected: true, excluded: true, category: 'GameDirectory',
      protectionReason: '命中保留关键词「SteamLibrary」',
      lastModified: '2026-09-30 19:02', fileCount: 1840, dirCount: 288, version: null, publisher: null,
      rec: 'Keep', recText: '建议保留',
      pos: [['长时间未修改', '已 7 天未修改', 4]],
      neg: [
        ['疑似游戏目录', '命中保留关键词 SteamLibrary', -60],
        ['受白名单保护', '保留关键词命中，永不参与删除', -100]
      ],
      warnings: []
    },
    {
      id: 'i6', name: 'BrowserHelper', path: 'D:\\Apps\\BrowserHelper', size: '1.27 GB', sizeBytes: 1.27e9,
      conf: 95, risk: 'Low', protected: false, excluded: false, category: null,
      lastModified: '2024-01-08 11:20', fileCount: 642, dirCount: 38, version: '1.9.2', publisher: 'HelperSoft',
      rec: 'Quarantine', recText: '建议人工确认后隔离',
      pos: [
        ['未发现卸载项', 'HKLM / HKCU 卸载项中查不到该目录', 25],
        ['未发现 App Paths', 'App Paths 中没有指向该目录的条目', 15],
        ['未发现快捷方式', '开始菜单、桌面、启动目录均无命中', 12],
        ['未发现相关服务', 'Windows 服务 ImagePath 中无命中', 10],
        ['未发现计划任务', '计划任务 XML 中无命中', 10],
        ['未发现启动项', 'Run / RunOnce 中无命中', 10],
        ['当前没有相关进程', '运行进程路径无命中', 8],
        ['未发现文件关联', 'Classes\\*\\shell\\command 与 CLSID 中无命中', 8],
        ['长时间未修改', '已 1003 天未修改', 12],
        ['存在卸载程序', '发现 uninstall.exe', 25]
      ],
      neg: [], warnings: []
    },
    {
      id: 'i7', name: 'PortableNotes', path: 'F:\\PortableApps\\PortableNotes', size: '48 MB', sizeBytes: 4.8e7,
      conf: 62, risk: 'Low', protected: false, excluded: false, category: 'PortableSoftware',
      lastModified: '2025-08-14 08:55', fileCount: 34, dirCount: 6, version: '4.2', publisher: null,
      rec: 'Review', recText: '建议人工确认',
      pos: [
        ['未发现卸载项', 'HKLM / HKCU 卸载项中查不到该目录', 25],
        ['长时间未修改', '已 420 天未修改', 12]
      ],
      neg: [
        ['可能是便携式软件', '位于 PortableApps，无需安装即可运行', -25],
        ['发现用户数据', 'notes.db 疑似保存了用户笔记', -30]
      ],
      warnings: []
    },
    {
      id: 'i8', name: 'PdfToolPro', path: 'D:\\Program Files (x86)\\PdfToolPro', size: '386 MB', sizeBytes: 3.86e8,
      conf: 91, risk: 'Medium', protected: false, excluded: false, category: null,
      lastModified: '2026-09-27 15:08', fileCount: 512, dirCount: 44, version: '7.1.0', publisher: 'PdfTool',
      rec: 'Review', recText: '建议人工确认',
      pos: [
        ['未发现卸载项', 'HKLM / HKCU 卸载项中查不到该目录', 25],
        ['未发现快捷方式', '开始菜单与桌面均无命中', 12],
        ['未发现相关服务', 'Windows 服务 ImagePath 中无命中', 10],
        ['未发现计划任务', '计划任务 XML 中无命中', 10]
      ],
      neg: [
        ['检测到最近修改', '10 天前仍有写入，可能仍在使用', -25]
      ],
      warnings: []
    },
    {
      id: 'i9', name: 'LegacyDriverPack', path: 'D:\\Drivers\\LegacyDriverPack', size: '214 MB', sizeBytes: 2.14e8,
      conf: 83, risk: 'High', protected: false, excluded: false, category: null,
      lastModified: '2024-11-30 13:26', fileCount: 1180, dirCount: 74, version: null, publisher: null,
      rec: 'HighRiskReview', recText: '高风险，必须人工复核',
      pos: [
        ['未发现卸载项', 'HKLM / HKCU 卸载项中查不到该目录', 25],
        ['长时间未修改', '已 676 天未修改', 12]
      ],
      neg: [],
      warnings: [
        '关联来源「Windows 服务」「计划任务」未完成检查，判定可靠性下降',
        '未以管理员身份运行：判定偏保守，风险已自动升级为「高」'
      ]
    }
  ];

  const state = { filter: 'all', search: '', sort: 'conf', selectedId: null, checked: new Set() };

  function confClass(c) { return c >= 90 ? 'conf-vhp' : c >= 70 ? 'conf-hp' : c >= 40 ? 'conf-sus' : 'conf-norm'; }
  function confLabel(c) { return c >= 90 ? '非常高概率残留' : c >= 70 ? '高概率残留' : c >= 40 ? '可疑' : '普通'; }
  const RISK = {
    Low: { cls: 'badge-success', ico: 'gc-shield-check', text: '低' },
    Medium: { cls: 'badge-warning', ico: 'gc-shield-alert', text: '中' },
    High: { cls: 'badge-danger', ico: 'gc-alert-circle', text: '高' }
  };

  function visibleItems() {
    let list = ITEMS.slice();
    if (state.filter === 'high') list = list.filter(i => i.conf >= 70);
    else if (state.filter === 'review') list = list.filter(i => i.rec === 'Review' || i.rec === 'HighRiskReview');
    else if (state.filter === 'protected') list = list.filter(i => i.protected || i.excluded);
    if (state.search) {
      const q = state.search.toLowerCase();
      list = list.filter(i => i.name.toLowerCase().includes(q) || i.path.toLowerCase().includes(q));
    }
    const cmp = {
      conf: (a, b) => b.conf - a.conf,
      size: (a, b) => b.sizeBytes - a.sizeBytes,
      path: (a, b) => a.path.localeCompare(b.path),
      risk: (a, b) => ({ High: 0, Medium: 1, Low: 2 })[a.risk] - ({ High: 0, Medium: 1, Low: 2 })[b.risk]
    }[state.sort];
    return list.sort(cmp);
  }

  function renderCounts() {
    $('#cntAll').textContent = '(' + ITEMS.length + ')';
    $('#cntHigh').textContent = '(' + ITEMS.filter(i => i.conf >= 70).length + ')';
    $('#cntReview').textContent = '(' + ITEMS.filter(i => i.rec === 'Review' || i.rec === 'HighRiskReview').length + ')';
    $('#cntProtected').textContent = '(' + ITEMS.filter(i => i.protected || i.excluded).length + ')';
  }

  function renderSummary() {
    const totalBytes = ITEMS.filter(i => !i.excluded).reduce((s, i) => s + i.sizeBytes, 0);
    const gb = (totalBytes / 1e9).toFixed(1);
    const high = ITEMS.filter(i => i.conf >= 70 && !i.excluded).length;
    const review = ITEMS.filter(i => (i.rec === 'Review' || i.rec === 'HighRiskReview') && !i.excluded).length;
    const prot = ITEMS.filter(i => i.protected || i.excluded).length;
    $('#resultsSummary').textContent =
      `共 ${ITEMS.length} 项，${gb} GB。其中 ${high} 项为高概率残留，${review} 项需要人工确认，${prot} 项受保护或已排除。默认未勾选任何条目。`;
  }

  function renderList() {
    const list = visibleItems();
    const host = $('#resultList');
    host.innerHTML = '';
    $('#resultEmpty').innerHTML = list.length ? '' :
      '<div class="empty">' + icon('gc-filter', 'i48') +
      '<div class="empty-title">当前筛选条件下没有条目</div>' +
      '<div class="empty-desc">试试切换筛选条件，或清空搜索框。</div></div>';

    list.forEach(it => {
      const r = RISK[it.risk];
      const locked = it.protected || it.excluded;
      const el = document.createElement('div');
      el.className = 'result-item' + (state.selectedId === it.id ? ' selected' : '') + (locked ? ' protected' : '');
      const evText = it.pos.slice(0, 3).map(e => e[0]).join(' · ') + (it.pos.length > 3 ? ` +${it.pos.length - 3}` : '');
      el.innerHTML =
        '<label class="check' + (locked ? ' disabled' : '') + '" onclick="event.stopPropagation()">' +
          '<input type="checkbox" data-check="' + it.id + '"' +
            (state.checked.has(it.id) ? ' checked' : '') + (locked ? ' disabled' : '') + '/>' +
          '<span class="check-box"><svg viewBox="0 0 24 24"><use href="#gc-check"/></svg></span>' +
        '</label>' +
        '<div class="ri-main">' +
          '<div class="ri-name">' + esc(it.name) + '</div>' +
          '<div class="ri-path">' + esc(it.path) + '</div>' +
          '<div class="ri-meta">' +
            '<span class="confidence ' + confClass(it.conf) + '" title="疑似残留度：我们有多大把握认为它是残留">' +
              '<span class="confidence-track"><span class="confidence-fill" style="width:' + it.conf + '%"></span></span>' +
              '<span class="confidence-value">' + it.conf + '%</span></span>' +
            '<span class="ri-evidence">' + (evText || '—') + '</span>' +
          '</div>' +
        '</div>' +
        '<div class="ri-right">' +
          '<span class="ri-size">' + it.size + '</span>' +
          '<div class="ri-badges">' +
            '<span class="badge ' + r.cls + '">' + icon(r.ico) + r.text + '</span>' +
            (it.excluded ? '<span class="badge badge-neutral">' + icon('gc-eye-off') + '已排除</span>' : '') +
            (it.protected && !it.excluded ? '<span class="badge badge-success">' + icon('gc-shield-check') + '已保护</span>' : '') +
            (it.warnings.length ? '<span class="badge badge-warning">' + icon('gc-alert-triangle') + '需注意</span>' : '') +
          '</div>' +
        '</div>';
      el.addEventListener('click', () => { state.selectedId = it.id; renderList(); renderDetail(); });
      host.appendChild(el);
    });

    $$('[data-check]').forEach(cb => cb.addEventListener('change', e => {
      const id = e.target.dataset.check;
      if (e.target.checked) state.checked.add(id); else state.checked.delete(id);
      renderActionBar();
    }));
  }

  function renderActionBar() {
    const bar = $('#actionBar');
    const ids = Array.from(state.checked);
    if (!ids.length) { bar.style.display = 'none'; return; }
    bar.style.display = 'flex';
    const bytes = ITEMS.filter(i => ids.includes(i.id)).reduce((s, i) => s + i.sizeBytes, 0);
    const gb = bytes >= 1e9 ? (bytes / 1e9).toFixed(2) + ' GB' : Math.round(bytes / 1e6) + ' MB';
    $('#abSummary').textContent = `已选 ${ids.length} 项 · 共 ${gb}`;
  }

  function renderDetail() {
    const it = ITEMS.find(i => i.id === state.selectedId);
    const empty = $('#detailEmpty'), content = $('#detailContent'), panel = $('#detailPanel');
    if (!it) { empty.style.display = ''; content.style.display = 'none'; panel.classList.remove('open'); return; }
    empty.style.display = 'none';
    content.style.display = '';
    panel.classList.add('open');
    const r = RISK[it.risk];
    const locked = it.protected || it.excluded;

    const evHtml = (arr, cls) => arr.length
      ? '<div class="evidence-list">' + arr.map(e =>
          '<div class="evidence-item ' + cls + '">' + icon(cls === 'pos' ? 'gc-check' : 'gc-alert-triangle') +
          '<div><div class="ev-title">' + esc(e[0]) + '</div><div class="ev-desc">' + esc(e[1]) + '</div></div>' +
          '<div class="ev-score">' + (e[2] > 0 ? '+' + e[2] : e[2]) + '</div></div>').join('') + '</div>'
      : '<div class="t-sm c-tertiary" style="padding:var(--sp-2) 0">无</div>';

    content.innerHTML =
      '<div class="detail-head">' +
        '<div class="row g8"><span class="t-h2" style="font-weight:600">' + esc(it.name) + '</span>' +
          '<span class="badge ' + r.cls + '">' + icon(r.ico) + '风险 ' + r.text + '</span>' +
          (it.excluded ? '<span class="badge badge-neutral">' + icon('gc-eye-off') + '已排除</span>' : '') +
          (it.protected && !it.excluded ? '<span class="badge badge-success">' + icon('gc-shield-check') + '已保护</span>' : '') +
        '</div>' +
        '<div class="path-line" style="margin-top:var(--sp-2)">' +
          '<span class="path-text" title="' + esc(it.path) + '">' + esc(it.path) + '</span>' +
          '<button class="icon-btn sm" data-act="copy" title="复制路径">' + icon('gc-copy', 'i16') + '</button>' +
          '<button class="icon-btn sm" data-act="open" title="打开文件夹">' + icon('gc-folder-open', 'i16') + '</button>' +
        '</div>' +
      '</div>' +

      '<div class="detail-body">' +
        '<div class="t-overline c-tertiary">判定结论</div>' +
        '<div class="verdict-grid">' +
          '<div class="verdict-cell"><div class="vl">疑似残留度</div>' +
            '<div class="confidence ' + confClass(it.conf) + ' lg" style="margin-top:var(--sp-2)">' +
              '<span class="confidence-track"><span class="confidence-fill" style="width:' + it.conf + '%"></span></span>' +
              '<span class="confidence-value">' + it.conf + '%</span></div>' +
            '<div class="t-caption c-tertiary" style="margin-top:6px">' + confLabel(it.conf) + '</div></div>' +
          '<div class="verdict-cell"><div class="vl">清理风险</div>' +
            '<div class="row g8" style="margin-top:var(--sp-2)"><span class="badge ' + r.cls + '">' + icon(r.ico) + r.text + '</span></div>' +
            '<div class="t-caption c-tertiary" style="margin-top:6px">' + esc(it.protectionReason || '未发现显著保护信号') + '</div></div>' +
        '</div>' +

        '<div class="banner ' + (it.rec === 'Quarantine' ? 'banner-info' : it.rec === 'Keep' ? 'banner-warning' : 'banner-warning') + '" style="margin-top:var(--sp-4)">' +
          icon(it.rec === 'Quarantine' ? 'gc-shield-check' : 'gc-shield-alert') +
          '<div><div class="banner-title">推荐操作：' + esc(it.recText) + '</div>' +
          '<div class="banner-body">GhostCleaner 不会自动执行任何删除。是否处理由你决定。</div></div></div>' +

        (it.warnings.length
          ? '<div class="banner banner-warning" style="margin-top:var(--sp-2)">' + icon('gc-alert-triangle') +
            '<div><div class="banner-title">警告</div><div class="banner-body">' + it.warnings.map(esc).join('<br/>') + '</div></div></div>'
          : '') +

        '<div class="evidence-group" style="margin-top:var(--sp-5)">' +
          '<div class="t-overline c-tertiary">检测依据 · 支持「是残留」（' + it.pos.length + '）</div>' + evHtml(it.pos, 'pos') + '</div>' +
        '<div class="evidence-group">' +
          '<div class="t-overline c-tertiary">保护信号 · 支持「保留」（' + it.neg.length + '）</div>' + evHtml(it.neg, 'neg') + '</div>' +

        '<div class="section" style="margin-top:var(--sp-5)">' +
          '<div class="t-overline c-tertiary">文件信息</div>' +
          '<dl class="kv" style="margin-top:var(--sp-2)">' +
            '<dt>大小</dt><dd class="num">' + it.size + '</dd>' +
            '<dt>文件数</dt><dd class="num">' + fmtInt(it.fileCount) + '</dd>' +
            '<dt>目录数</dt><dd class="num">' + fmtInt(it.dirCount) + '</dd>' +
            '<dt>最后修改</dt><dd class="num">' + it.lastModified + '</dd>' +
            '<dt>版本</dt><dd>' + (it.version || '—') + '</dd>' +
            '<dt>厂商</dt><dd>' + (it.publisher || '—') + '</dd>' +
            '<dt>目录分类</dt><dd>' + (it.category || '—') + '</dd>' +
          '</dl></div>' +

        '<div class="section">' +
          '<div class="t-overline c-tertiary">系统关联</div>' +
          '<div class="t-sm c-secondary" style="margin-top:var(--sp-2)">' +
            it.pos.filter(e => e[0].startsWith('未发现')).map(e => esc(e[0])).join(' · ') || '—' +
          '</div></div>' +
      '</div>' +

      '<div class="detail-foot">' +
        '<button class="btn btn-primary sm" data-act="quarantine"' + (locked ? ' disabled' : '') + '>' + icon('gc-quarantine', 'i16') + '移入隔离区</button>' +
        '<button class="btn btn-secondary sm" data-act="open">打开文件夹</button>' +
        '<button class="btn btn-secondary sm" data-act="uninstall"' + (it.name === 'OldToolSuite' || it.name === 'BrowserHelper' ? '' : ' disabled') + '>运行卸载程序</button>' +
        '<button class="btn btn-subtle sm" data-act="whitelist"' + (it.excluded ? ' disabled' : '') + '>' + icon('gc-bookmark', 'i16') + '加入白名单</button>' +
      '</div>';

    $('[data-act="copy"]', content).addEventListener('click', () => toast('success', '路径已复制到剪贴板'));
    $$('[data-act="open"]', content).forEach(b => b.addEventListener('click', () => toast('info', '已在文件资源管理器中打开该目录')));
    $('[data-act="uninstall"]', content).addEventListener('click', () => toast('info', '已启动卸载程序'));
    $('[data-act="whitelist"]', content).addEventListener('click', () => {
      showDialog({
        tone: 'accent', icon: 'gc-bookmark', title: '加入排除路径白名单',
        body: '<p>该目录及其子目录将不再参与任何删除操作。</p>' +
              '<div class="dialog-summary"><span style="font-family:var(--font-mono);font-size:12px">' + esc(it.path) + '</span></div>' +
              '<p>之后可在「设置 → 保护与白名单」中移除。</p>',
        confirmText: '加入白名单',
        onConfirm: () => { it.excluded = true; it.protectionReason = '已加入排除路径白名单'; state.checked.delete(it.id); renderList(); renderDetail(); renderActionBar(); toast('success', '已加入白名单，该目录不会再参与删除'); }
      });
    });
    $('[data-act="quarantine"]', content).addEventListener('click', () => {
      state.checked.add(it.id); renderList(); renderActionBar(); confirmQuarantine();
    });
  }

  /* ---------------- 结果页交互 ---------------- */
  $$('#filterTabs button').forEach(b => b.addEventListener('click', () => {
    state.filter = b.dataset.filter;
    $$('#filterTabs button').forEach(x => x.setAttribute('aria-selected', String(x === b)));
    renderList();
  }));
  $('#searchInput').addEventListener('input', e => { state.search = e.target.value; renderList(); });
  $('#sortSelect').addEventListener('change', e => { state.sort = e.target.value; renderList(); });
  $('#selectNoneBtn').addEventListener('click', () => { state.checked.clear(); renderList(); renderActionBar(); });
  $('#selectLowRiskBtn').addEventListener('click', () => {
    state.checked.clear();
    ITEMS.filter(i => i.risk === 'Low' && !i.excluded && !i.protected).forEach(i => state.checked.add(i.id));
    renderList(); renderActionBar();
    toast('info', '已选中 ' + state.checked.size + ' 个低风险条目');
  });
  $('#exportBtn').addEventListener('click', () => toast('success', '扫描报告已导出为 JSON'));
  $('#exportBtn2').addEventListener('click', () => toast('success', '扫描报告已导出为 JSON'));

  function confirmQuarantine() {
    const ids = Array.from(state.checked);
    if (!ids.length) { toast('warning', '请先勾选要处理的条目。默认不会自动勾选任何条目。'); return; }
    const picked = ITEMS.filter(i => ids.includes(i.id));
    const bytes = picked.reduce((s, i) => s + i.sizeBytes, 0);
    const gb = bytes >= 1e9 ? (bytes / 1e9).toFixed(2) + ' GB' : Math.round(bytes / 1e6) + ' MB';
    const hi = picked.filter(i => i.risk === 'High').length;
    showDialog({
      tone: 'accent', icon: 'gc-quarantine', title: '确认移入隔离区',
      body: '<p>即将把 <strong>' + picked.length + '</strong> 个目录移动到隔离区，总大小 <strong>' + gb + '</strong>。</p>' +
            '<div class="dialog-summary">' + picked.map(i => '<div style="font-family:var(--font-mono);font-size:12px">' + esc(i.path) + '</div>').join('') + '</div>' +
            '<p>目录会被<strong>移动</strong>到隔离区而不是删除，可随时恢复到原始路径。清理前会尝试创建系统还原点。</p>' +
            (hi ? '<p style="color:var(--warning-fg)">所选条目中有 ' + hi + ' 个高风险条目，需要你确认后才会处理。</p>' : ''),
      confirmText: '移入隔离区',
      onConfirm: () => {
        picked.forEach(i => { ITEMS.splice(ITEMS.indexOf(i), 1); });
        state.checked.clear(); state.selectedId = null;
        renderList(); renderDetail(); renderActionBar(); renderCounts(); renderSummary(); renderDash();
        toast('success', picked.length + ' 项已移入隔离区 · 可在隔离区恢复', '查看隔离区', () => navigate('quarantine'));
      }
    });
  }
  function confirmPermDelete() {
    const ids = Array.from(state.checked);
    if (!ids.length) { toast('warning', '请先勾选要处理的条目。'); return; }
    const picked = ITEMS.filter(i => ids.includes(i.id));
    showDialog({
      tone: 'danger', icon: 'gc-alert-triangle', title: '永久删除',
      body: '<p>即将<strong>永久删除</strong> ' + picked.length + ' 个目录。<strong>此操作不可恢复。</strong></p>' +
            '<div class="dialog-summary">' + picked.map(i => '<div style="font-family:var(--font-mono);font-size:12px">' + esc(i.path) + '</div>').join('') + '</div>' +
            '<p>如果只是想腾出空间又保留退路，请改用「移入隔离区」。</p>',
      requireText: '永久删除',
      danger: true, confirmText: '永久删除',
      onConfirm: () => {
        picked.forEach(i => ITEMS.splice(ITEMS.indexOf(i), 1));
        state.checked.clear(); state.selectedId = null;
        renderList(); renderDetail(); renderActionBar(); renderCounts(); renderSummary(); renderDash();
        toast('danger', picked.length + ' 项已永久删除 · 不可恢复');
      }
    });
  }
  $('#quarantineBtn').addEventListener('click', confirmQuarantine);
  $('#permDeleteBtn').addEventListener('click', confirmPermDelete);

  /* ---------------- 隔离区交互 ---------------- */
  $('#restoreBtn').addEventListener('click', () => {
    showDialog({
      tone: 'accent', icon: 'gc-restore', title: '恢复所选条目',
      body: '<p>条目会被移回<strong>原始路径</strong>。如果原路径已存在同名目录，恢复会失败。</p>' +
            '<div class="dialog-summary"><div style="font-family:var(--font-mono);font-size:12px">D:\\Program Files\\ExampleApp</div>' +
            '<div style="font-family:var(--font-mono);font-size:12px">E:\\Software\\OldToolSuite</div></div>',
      confirmText: '恢复',
      onConfirm: () => toast('success', '2 项已恢复到原始路径')
    });
  });
  $('#purgeBtn').addEventListener('click', () => {
    showDialog({
      tone: 'danger', icon: 'gc-trash', title: '永久删除隔离区条目',
      body: '<p>此操作<strong>不可恢复</strong>，被隔离的目录将被彻底删除。</p>',
      requireText: '永久删除', danger: true, confirmText: '永久删除',
      onConfirm: () => toast('danger', '条目已永久删除')
    });
  });

  /* ---------------- 扫描流程模拟 ---------------- */
  const SCAN_PATHS = [
    'D:\\Program Files', 'D:\\Program Files\\ExampleApp', 'D:\\Program Files\\ExampleApp\\resources',
    'D:\\Program Files (x86)\\PdfToolPro', 'D:\\Apps\\BrowserHelper', 'D:\\Drivers\\LegacyDriverPack',
    'E:\\Software\\OldToolSuite', 'E:\\Software\\MediaConverter', 'E:\\PortableApps', 'F:\\PortableApps\\PortableNotes'
  ];
  let scanTimer = null, scanPct = 0, scanSec = 0, paused = false;
  const CIRC = 251.3;

  function setRing(p) {
    $('#ringFg').setAttribute('stroke-dashoffset', String(CIRC * (1 - p / 100)));
    $('#ringText').textContent = Math.round(p) + '%';
  }
  function stopScan() { if (scanTimer) { clearInterval(scanTimer); scanTimer = null; } }
  function startScan() {
    stopScan();
    scanPct = 0; scanSec = 0; paused = false;
    navigate('scanning');
    $('#pauseBtn').innerHTML = icon('gc-pause', 'i16') + '暂停';
    scanTimer = setInterval(() => {
      if (paused) return;
      scanPct = Math.min(100, scanPct + 1.1);
      scanSec += 0.25;
      setRing(scanPct);
      const mm = String(Math.floor(scanSec / 60)).padStart(2, '0');
      const ss = String(Math.floor(scanSec % 60)).padStart(2, '0');
      $('#scanElapsed').textContent = mm + ':' + ss;
      $('#statScanned').textContent = fmtInt(Math.round(18492 * scanPct / 100));
      $('#statCandidate').textContent = String(Math.round(264 * scanPct / 100));
      $('#statOrphan').textContent = String(Math.round(12 * scanPct / 100));
      $('#scanPath').textContent = SCAN_PATHS[Math.min(SCAN_PATHS.length - 1, Math.floor(scanPct / 10))];
      const steps = $$('#page-scanning .step');
      if (scanPct >= 100) {
        steps.forEach(s => { s.className = 'step done'; });
        $('#scanPhase').textContent = '扫描完成';
        stopScan();
        setTimeout(() => navigate('complete'), 500);
      } else {
        const idx = scanPct < 22 ? 0 : scanPct < 45 ? 1 : scanPct < 78 ? 2 : 3;
        const names = ['正在建立系统关联索引', '正在枚举候选目录', '正在逐个度量目录', '正在分析判定'];
        $('#scanPhase').textContent = names[idx];
        steps.forEach((s, i) => { s.className = 'step' + (i < idx ? ' done' : i === idx ? ' active' : ''); });
      }
    }, 250);
  }
  $('#startScanBtn').addEventListener('click', startScan);
  $('#pauseBtn').addEventListener('click', () => {
    paused = !paused;
    $('#pauseBtn').innerHTML = icon(paused ? 'gc-play' : 'gc-pause', 'i16') + (paused ? '继续' : '暂停');
    $('#scanPhase').textContent = paused ? '已暂停' : '正在扫描';
  });
  $('#cancelBtn').addEventListener('click', () => {
    stopScan();
    showDialog({
      tone: 'danger', icon: 'gc-stop', title: '取消本次扫描',
      body: '<p>已扫描到的候选会被丢弃，不会写入扫描记录。</p>',
      confirmText: '取消扫描', danger: true,
      onConfirm: () => { navigate('scan'); toast('warning', '扫描已取消'); }
    });
  });

  /* ---------------- 设置 / 记录 分页 ---------------- */
  $$('#settingsNav .nav-item').forEach(b => b.addEventListener('click', () => {
    $$('#settingsNav .nav-item').forEach(x => x.classList.toggle('active', x === b));
    $$('.settings-pane').forEach(p => p.classList.toggle('active', p.dataset.pane === b.dataset.pane));
  }));
  $$('#histTabs button').forEach(b => b.addEventListener('click', () => {
    $$('#histTabs button').forEach(x => x.setAttribute('aria-selected', String(x === b)));
    $('#histScans').style.display = b.dataset.hist === 'scans' ? '' : 'none';
    $('#histLogs').style.display = b.dataset.hist === 'logs' ? '' : 'none';
  }));
  $('#scanOptionsHead').addEventListener('click', () => $('#scanOptions').classList.toggle('open'));

  /* ---------------- 概览「需要你看一眼」 ---------------- */
  function renderDash() {
    const picks = ITEMS.filter(i => i.conf >= 70 && (i.neg.length || i.warnings.length) && !i.excluded).slice(0, 3);
    const host = $('#dashReview');
    if (!picks.length) { host.innerHTML = '<div class="t-sm c-tertiary" style="padding:var(--sp-3) 0">当前没有需要特别复核的条目。</div>'; return; }
    host.innerHTML = picks.map(i => {
      const r = RISK[i.risk];
      return '<div class="review-item">' +
        '<div class="ri-t"><div class="nm">' + esc(i.name) + '</div><div class="pt">' + esc(i.path) + '</div></div>' +
        '<div class="rr">' +
          '<span class="confidence ' + confClass(i.conf) + '">' +
            '<span class="confidence-track"><span class="confidence-fill" style="width:' + i.conf + '%"></span></span>' +
            '<span class="confidence-value">' + i.conf + '%</span></span>' +
          '<span class="badge ' + r.cls + '">' + icon(r.ico) + r.text + '</span>' +
          '<button class="btn btn-subtle sm" data-jump="' + i.id + '">查看</button>' +
        '</div></div>';
    }).join('');
    $$('[data-jump]', host).forEach(b => b.addEventListener('click', () => {
      navigate('results');
      state.selectedId = b.dataset.jump;
      setTimeout(() => { renderList(); renderDetail(); }, 30);
    }));
  }

  /* ---------------- 初始化 ---------------- */
  renderCounts(); renderSummary(); renderList(); renderDash(); setRing(63);
})();
