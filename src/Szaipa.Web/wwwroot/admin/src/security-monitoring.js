// Security-monitoring dashboard for the staff backend (Areas/Staff SecurityMonitoring/Index). Bundled by
// esbuild into wwwroot/admin/security-monitoring.js. Mirrors the Dashboard charts (modular ECharts, brand
// palette) plus table/AJAX rendering for blocked IPs, whitelist, login attempts, action queue and alerts.
import * as echarts from 'echarts/core';
import { BarChart, LineChart } from 'echarts/charts';
import { TooltipComponent, GridComponent, LegendComponent } from 'echarts/components';
import { LabelLayout } from 'echarts/features';
import { CanvasRenderer } from 'echarts/renderers';

echarts.use([
  BarChart, LineChart,
  TooltipComponent, GridComponent, LegendComponent,
  LabelLayout, CanvasRenderer,
]);

var PALETTE = ['#bf272d', '#d97b54', '#3e6b8a', '#8a9b6e', '#c9a45c', '#7a6f8f', '#939393'];
var INK = '#2b2b2b';
var MUTED = '#939393';

function getJson(url) {
  return fetch(url, { headers: { 'X-Requested-With': 'fetch' } }).then(function (r) {
    if (!r.ok) throw new Error('请求失败：' + r.status);
    return r.json();
  });
}

function postJson(url, body) {
  var token = document.querySelector('input[name="__RequestVerificationToken"]');
  var headers = { 'X-Requested-With': 'fetch', 'Content-Type': 'application/json' };
  if (token) headers['RequestVerificationToken'] = token.value;
  return fetch(url, { method: 'POST', headers: headers, body: JSON.stringify(body) }).then(function (r) {
    return r.json();
  });
}

function mount(id) {
  var el = document.getElementById(id);
  if (!el) return null;
  var chart = echarts.init(el, null, { renderer: 'canvas' });
  window.addEventListener('resize', function () { chart.resize(); });
  return chart;
}

function showEmpty(el, text) {
  if (el) el.innerHTML = '<div class="flex h-full items-center justify-center text-sm text-muted">' + (text || '暂无数据') + '</div>';
}

function setText(id, value) {
  var el = document.getElementById(id);
  if (el) el.textContent = value == null ? '—' : String(value);
}

function fmtTime(value) {
  if (!value) return '—';
  var d = new Date(value);
  if (isNaN(d.getTime())) return String(value);
  var p = function (n) { return n < 10 ? '0' + n : String(n); };
  return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()) + ' ' + p(d.getHours()) + ':' + p(d.getMinutes());
}

function fmtRemaining(seconds) {
  if (!seconds || seconds <= 0) return '—';
  var h = Math.floor(seconds / 3600);
  var m = Math.floor((seconds % 3600) / 60);
  return h + ' 小时 ' + m + ' 分';
}

function renderKpis() {
  getJson('/Staff/SecurityMonitoring/Kpis').then(function (d) {
    setText('kpi-failed-logons', d.snapshot && d.snapshot.failedLogons24h != null ? d.snapshot.failedLogons24h : '—');
    setText('kpi-lockouts', d.snapshot && d.snapshot.lockouts24h != null ? d.snapshot.lockouts24h : '—');
    setText('kpi-blocked', d.blockedCount);
    setText('kpi-whitelist', d.whitelistCount);
    setText('kpi-requests', d.auditRequestsLast24h);
    setText('kpi-login-fails', d.auditLoginFailsLast24h);
  }).catch(function () { /* tiles stay '—' */ });
}

function sourcesChart(chart, el) {
  getJson('/Staff/SecurityMonitoring/Snapshot').then(function (d) {
    var items = (d && d.bySourceIp) || [];
    items = items.slice().sort(function (a, b) { return (b.failedLogons || 0) - (a.failedLogons || 0); }).slice(0, 10);
    if (items.length === 0) { showEmpty(el, '暂无攻击源数据'); return; }
    chart.setOption({
      color: [PALETTE[0]],
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      grid: { left: 8, right: 16, bottom: 8, top: 8, containLabel: true },
      xAxis: { type: 'value', splitLine: { lineStyle: { color: '#f0f0f0' } }, axisLabel: { color: MUTED } },
      yAxis: { type: 'category', data: items.map(function (i) { return i.ip; }).reverse(), axisLine: { lineStyle: { color: '#e5e5e5' } }, axisLabel: { color: INK } },
      series: [{ type: 'bar', barWidth: '55%', data: items.map(function (i) { return i.failedLogons; }).reverse() }],
    });
  }).catch(function () { showEmpty(el); });
}

function trafficChart(chart, el) {
  getJson('/Staff/SecurityMonitoring/WebTraffic?days=14').then(function (d) {
    if (!d.categories || d.categories.length === 0) { showEmpty(el); return; }
    chart.setOption({
      color: [PALETTE[0], '#3e6b8a'],
      tooltip: { trigger: 'axis' },
      legend: { bottom: 0, textStyle: { color: INK } },
      grid: { left: 8, right: 16, bottom: 28, top: 16, containLabel: true },
      xAxis: { type: 'category', data: d.categories, axisLine: { lineStyle: { color: '#e5e5e5' } }, axisLabel: { color: MUTED } },
      yAxis: { type: 'value', splitLine: { lineStyle: { color: '#f0f0f0' } }, axisLabel: { color: MUTED } },
      series: [
        { name: '请求量', type: 'line', smooth: true, showSymbol: false, data: d.requests, areaStyle: { color: 'rgba(191,39,45,0.08)' }, lineStyle: { width: 2 } },
        { name: '4xx/5xx', type: 'line', smooth: true, showSymbol: false, data: d.errors, lineStyle: { width: 2 } },
      ],
    });
  }).catch(function () { showEmpty(el); });
}

function renderBlocked() {
  var tbody = document.getElementById('blocked-rows');
  if (!tbody) return;
  getJson('/Staff/SecurityMonitoring/BlockedIps').then(function (d) {
    var items = d.items || [];
    if (items.length === 0) {
      tbody.innerHTML = '<tr><td colspan="4" class="py-6 text-center text-muted">暂无封禁 IP</td></tr>';
      return;
    }
    tbody.innerHTML = items.map(function (b) {
      return '<tr class="border-b border-black/5">' +
        '<td class="py-2 pr-4 font-mono text-ink">' + escapeHtml(b.ip) + '</td>' +
        '<td class="py-2 pr-4 text-ink/70">' + fmtTime(b.blockedAtUtc) + '</td>' +
        '<td class="py-2 pr-4 text-ink/70">' + fmtRemaining(b.remainingSeconds) + '</td>' +
        '<td class="py-2"><button type="button" data-unblock="' + escapeHtml(b.ip) + '" class="rounded-md border border-brand/30 px-2 py-1 text-xs text-brand hover:bg-brand/5">解封</button></td>' +
        '</tr>';
    }).join('');
    tbody.querySelectorAll('button[data-unblock]').forEach(function (btn) {
      btn.addEventListener('click', function () { unblockIp(btn.getAttribute('data-unblock')); });
    });
  }).catch(function () {
    tbody.innerHTML = '<tr><td colspan="4" class="py-6 text-center text-muted">加载失败</td></tr>';
  });
}

function renderWhitelist() {
  var box = document.getElementById('whitelist-items');
  if (!box) return;
  getJson('/Staff/SecurityMonitoring/Whitelist').then(function (d) {
    var items = d.items || [];
    if (items.length === 0) {
      box.innerHTML = '<span class="text-sm text-muted">白名单为空</span>';
      return;
    }
    box.innerHTML = items.map(function (ip) {
      return '<span class="inline-flex items-center gap-1.5 rounded-full border border-black/10 bg-white px-3 py-1 text-sm text-ink">' +
        '<span class="font-mono">' + escapeHtml(ip) + '</span>' +
        '<button type="button" data-wl-remove="' + escapeHtml(ip) + '" class="text-xs text-muted hover:text-brand">✕</button>' +
        '</span>';
    }).join('');
    box.querySelectorAll('button[data-wl-remove]').forEach(function (btn) {
      btn.addEventListener('click', function () { whitelistEdit(btn.getAttribute('data-wl-remove'), false); });
    });
  }).catch(function () {
    box.innerHTML = '<span class="text-sm text-muted">加载失败</span>';
  });
}

function renderLogins() {
  var tbody = document.getElementById('login-rows');
  if (!tbody) return;
  getJson('/Staff/SecurityMonitoring/StaffLogins?limit=20').then(function (d) {
    var items = d.items || [];
    if (items.length === 0) {
      tbody.innerHTML = '<tr><td colspan="4" class="py-6 text-center text-muted">暂无登录记录（审计未开启或尚无尝试）</td></tr>';
      return;
    }
    tbody.innerHTML = items.map(function (l) {
      var badge = l.success ? '<span class="rounded-full bg-green-500/10 px-2 py-0.5 text-xs text-green-600">成功</span>'
        : '<span class="rounded-full bg-red-500/10 px-2 py-0.5 text-xs text-red-600">失败</span>';
      return '<tr class="border-b border-black/5">' +
        '<td class="py-2 pr-4 text-ink/70">' + fmtTime(l.timestamp) + '</td>' +
        '<td class="py-2 pr-4 text-ink">' + escapeHtml(l.username || '') + '</td>' +
        '<td class="py-2 pr-4 font-mono text-ink/70">' + escapeHtml(l.ip || '') + '</td>' +
        '<td class="py-2">' + badge + '</td></tr>';
    }).join('');
  }).catch(function () {
    tbody.innerHTML = '<tr><td colspan="4" class="py-6 text-center text-muted">加载失败</td></tr>';
  });
}

function renderActions() {
  var tbody = document.getElementById('action-rows');
  if (!tbody) return;
  getJson('/Staff/SecurityMonitoring/Actions').then(function (d) {
    var items = d.items || [];
    if (items.length === 0) {
      tbody.innerHTML = '<tr><td colspan="4" class="py-6 text-center text-muted">暂无操作请求</td></tr>';
      return;
    }
    tbody.innerHTML = items.map(function (a) {
      var kind = a.kind === 'unblock' ? '解封' : a.kind === 'whitelist-add' ? '白名单添加' : a.kind === 'whitelist-remove' ? '白名单移除' : a.kind;
      var status = a.status === 'processed'
        ? '<span class="rounded-full bg-green-500/10 px-2 py-0.5 text-xs text-green-600">已处理</span>'
        : '<span class="rounded-full bg-amber-500/10 px-2 py-0.5 text-xs text-amber-600">待处理</span>';
      return '<tr class="border-b border-black/5">' +
        '<td class="py-2 pr-4 text-ink">' + escapeHtml(kind) + '</td>' +
        '<td class="py-2 pr-4 font-mono text-ink/70">' + escapeHtml(a.targetIp || '') + '</td>' +
        '<td class="py-2 pr-4">' + status + '</td>' +
        '<td class="py-2 text-xs text-ink/60">' + escapeHtml(a.result || '') + '</td></tr>';
    }).join('');
  }).catch(function () {
    tbody.innerHTML = '<tr><td colspan="4" class="py-6 text-center text-muted">加载失败</td></tr>';
  });
}

function renderAlerts() {
  var box = document.getElementById('alert-log');
  if (!box) return;
  getJson('/Staff/SecurityMonitoring/Alerts').then(function (d) {
    var items = d.items || [];
    if (items.length === 0) {
      box.innerHTML = '<p class="text-muted">暂无告警日志</p>';
      return;
    }
    box.innerHTML = items.map(function (a) {
      return '<div class="mb-1 break-all">' + escapeHtml(a.line) + '</div>';
    }).join('');
  }).catch(function () {
    box.innerHTML = '<p class="text-muted">加载失败</p>';
  });
}

var trafficPage = 1;
var trafficHasMore = false;
var trafficLoadId = 0;

function trafficCategoryLabel(category) {
  if (category === 'bot') return 'Bot';
  if (category === 'suspectedBot') return '疑似 Bot';
  return '未判定';
}

function trafficCell(row, value, className) {
  var cell = document.createElement('td');
  cell.className = className || 'border-b border-black/5 px-3 py-2 align-top text-ink/80';
  cell.textContent = value == null || value === '' ? '—' : String(value);
  row.appendChild(cell);
  return cell;
}

function trafficEmptyRow(tbody, colspan, message) {
  var row = document.createElement('tr');
  var cell = document.createElement('td');
  cell.colSpan = colspan;
  cell.className = 'px-3 py-5 text-center text-muted';
  cell.textContent = message;
  row.appendChild(cell);
  tbody.replaceChildren(row);
}

function renderTrafficInsights() {
  var days = document.getElementById('traffic-insights-days');
  var category = document.getElementById('traffic-insights-category');
  var path = document.getElementById('traffic-insights-path');
  var status = document.getElementById('traffic-insights-status');
  var rows = document.getElementById('traffic-request-rows');
  var pages = document.getElementById('traffic-top-pages');
  if (!rows || !pages) return;

  var loadId = ++trafficLoadId;
  var query = new URLSearchParams({ days: days ? days.value : '7', page: String(trafficPage), limit: '50' });
  if (category && category.value) query.set('category', category.value);
  if (path && path.value.trim()) query.set('path', path.value.trim());
  status.textContent = '正在加载访问数据…';
  getJson('/Staff/SecurityMonitoring/TrafficInsights?' + query.toString()).then(function (d) {
    if (loadId !== trafficLoadId) return;
    var counts = d.counts || {};
    setText('traffic-count-total', counts.total);
    setText('traffic-count-bot', counts.bot);
    setText('traffic-count-suspected', counts.suspectedBot);
    setText('traffic-count-unknown', counts.unknown);

    var topPages = Array.isArray(d.topPages) ? d.topPages : [];
    if (topPages.length === 0) trafficEmptyRow(pages, 3, '此筛选条件下暂无页面数据');
    else {
      var pageFragment = document.createDocumentFragment();
      topPages.forEach(function (p) {
        var row = document.createElement('tr');
        row.className = 'border-b border-black/5';
        trafficCell(row, p.path);
        trafficCell(row, p.requests);
        trafficCell(row, (p.bot || 0) + ' / ' + (p.suspectedBot || 0) + ' / ' + (p.unknown || 0));
        pageFragment.appendChild(row);
      });
      pages.replaceChildren(pageFragment);
    }

    var requestData = d.requests || {};
    var items = Array.isArray(requestData.items) ? requestData.items : [];
    if (items.length === 0) trafficEmptyRow(rows, 5, '此筛选条件下暂无访问明细');
    else {
      var requestFragment = document.createDocumentFragment();
      items.forEach(function (item) {
        var row = document.createElement('tr');
        row.className = 'border-b border-black/5';
        trafficCell(row, item.path, 'max-w-[24rem] break-all border-b border-black/5 px-3 py-2 align-top font-mono text-xs text-ink/80');
        trafficCell(row, fmtTime(item.timestamp));
        trafficCell(row, item.status);
        var label = trafficCategoryLabel(item.category);
        trafficCell(row, label, 'border-b border-black/5 px-3 py-2 align-top text-ink/80');
        var reasons = Array.isArray(item.reasons) ? item.reasons.join('；') : item.reasons;
        trafficCell(row, reasons);
        requestFragment.appendChild(row);
      });
      rows.replaceChildren(requestFragment);
    }

    var currentPage = Number(requestData.page || trafficPage);
    var limit = Number(requestData.limit || 50);
    var total = Number(requestData.total || 0);
    trafficPage = currentPage;
    trafficHasMore = !!requestData.hasMore;
    var pageInfo = document.getElementById('traffic-page-info');
    if (pageInfo) pageInfo.textContent = total + ' 条记录 · 第 ' + currentPage + ' 页';
    var prev = document.getElementById('traffic-prev');
    var next = document.getElementById('traffic-next');
    if (prev) prev.disabled = currentPage <= 1;
    if (next) next.disabled = !trafficHasMore || items.length === 0;

    var notes = [];
    if (requestData.truncated) notes.push('明细已截断，当前只展示可读取的部分数据');
    if (requestData.sourceTruncated || d.sourceTruncated) notes.push(d.truncationNote || '来源日志存在截断，统计可能不完整');
    if (items.length === 0 && topPages.length === 0) notes.push('当前范围内没有可展示的数据');
    var classificationNote = d.classificationNote || '分类依据请求特征作自动判定；未判定不代表真人。';
    status.textContent = classificationNote + ' ' + (notes.length ? notes.join('；') : '数据已更新 · 最近 ' + (d.days || days.value) + ' 天');
  }).catch(function () {
    if (loadId !== trafficLoadId) return;
    setText('traffic-count-total', '—');
    setText('traffic-count-bot', '—');
    setText('traffic-count-suspected', '—');
    setText('traffic-count-unknown', '—');
    trafficEmptyRow(pages, 3, '访问数据加载失败，请稍后重试');
    trafficEmptyRow(rows, 5, '访问数据加载失败，请稍后重试');
    status.textContent = '访问分析加载失败，请检查权限或稍后重试。';
    var pageInfo = document.getElementById('traffic-page-info');
    if (pageInfo) pageInfo.textContent = '';
    var prev = document.getElementById('traffic-prev');
    var next = document.getElementById('traffic-next');
    if (prev) prev.disabled = true;
    if (next) next.disabled = true;
  });
}

var interactionLoadId = 0;

function renderInteractionInsights() {
  var days = document.getElementById('traffic-insights-days');
  var status = document.getElementById('interaction-insights-status');
  var pages = document.getElementById('interaction-top-pages');
  var recentRows = document.getElementById('interaction-recent-rows');
  if (!pages || !recentRows) return;

  var range = document.getElementById('interaction-insights-range');
  var selectedDays = days ? days.value : '7';
  var loadId = ++interactionLoadId;
  if (range) range.textContent = '时间范围：近 ' + selectedDays + ' 天';
  status.textContent = '正在加载浏览器交互证据…';
  getJson('/Staff/SecurityMonitoring/Interactions?days=' + encodeURIComponent(selectedDays) + '&limit=50').then(function (d) {
    if (loadId !== interactionLoadId) return;
    setText('interaction-count-views', d.views);
    setText('interaction-count-clicks', d.clicks);
    var topPages = Array.isArray(d.topPages) ? d.topPages : [];
    if (topPages.length === 0) trafficEmptyRow(pages, 3, '此时间范围内暂无页面交互数据');
    else {
      var pageFragment = document.createDocumentFragment();
      topPages.forEach(function (p) {
        var row = document.createElement('tr');
        row.className = 'border-b border-black/5';
        trafficCell(row, p.path, 'max-w-[24rem] break-all border-b border-black/5 px-3 py-2 align-top font-mono text-xs text-ink/80');
        trafficCell(row, p.views);
        trafficCell(row, p.clicks);
        pageFragment.appendChild(row);
      });
      pages.replaceChildren(pageFragment);
    }

    var recent = Array.isArray(d.recent) ? d.recent : [];
    if (recent.length === 0) trafficEmptyRow(recentRows, 4, '此时间范围内暂无浏览或点击记录');
    else {
      var recentFragment = document.createDocumentFragment();
      recent.forEach(function (item) {
        var row = document.createElement('tr');
        row.className = 'border-b border-black/5';
        trafficCell(row, fmtTime(item.timestamp));
        trafficCell(row, item.kind === 'click' ? '链接点击' : item.kind === 'view' ? '页面浏览' : '未知行为');
        trafficCell(row, item.pagePath, 'max-w-[20rem] break-all border-b border-black/5 px-3 py-2 align-top font-mono text-xs text-ink/80');
        trafficCell(row, item.targetPath, 'max-w-[20rem] break-all border-b border-black/5 px-3 py-2 align-top font-mono text-xs text-ink/80');
        recentFragment.appendChild(row);
      });
      recentRows.replaceChildren(recentFragment);
    }

    var notes = ['最近最多显示 50 条交互记录；浏览器脚本可模拟这些行为，不能据此确认真人访问。'];
    if (d.sourceTruncated) notes.unshift('交互日志来源已截断，汇总和明细可能不完整。');
    if (topPages.length === 0 && recent.length === 0) notes.unshift('当前时间范围无交互数据。');
    status.textContent = notes.join(' ');
  }).catch(function () {
    if (loadId !== interactionLoadId) return;
    setText('interaction-count-views', '—');
    setText('interaction-count-clicks', '—');
    trafficEmptyRow(pages, 3, '交互数据加载失败，请稍后重试');
    trafficEmptyRow(recentRows, 4, '交互数据加载失败，请稍后重试');
    status.textContent = '交互证据加载失败，请检查权限或稍后重试。';
  });
}

function unblockIp(ip) {
  if (!window.confirm('确认解封 ' + ip + ' ？')) return;
  postJson('/Staff/SecurityMonitoring/Unblock', { ip: ip }).then(function (d) {
    window.alert(d.message || (d.ok ? '已提交' : '操作失败'));
    renderBlocked();
    renderActions();
  }).catch(function () { window.alert('请求失败'); });
}

function whitelistEdit(ip, add) {
  var url = add ? '/Staff/SecurityMonitoring/WhitelistAdd' : '/Staff/SecurityMonitoring/WhitelistRemove';
  postJson(url, { ip: ip }).then(function (d) {
    window.alert(d.message || (d.ok ? '已提交' : '操作失败'));
    renderWhitelist();
    renderActions();
  }).catch(function () { window.alert('请求失败'); });
}

function escapeHtml(value) {
  return String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function init() {
  renderKpis();
  var sources = mount('chart-sources');
  if (sources) sourcesChart(sources, document.getElementById('chart-sources'));
  var traffic = mount('chart-traffic');
  if (traffic) trafficChart(traffic, document.getElementById('chart-traffic'));
  renderBlocked();
  renderWhitelist();
  renderLogins();
  renderActions();
  renderAlerts();

  var trafficFilter = document.getElementById('traffic-insights-filter');
  if (trafficFilter) trafficFilter.addEventListener('submit', function (e) {
    e.preventDefault();
    trafficPage = 1;
    renderTrafficInsights();
    renderInteractionInsights();
  });
  var trafficPrev = document.getElementById('traffic-prev');
  if (trafficPrev) trafficPrev.addEventListener('click', function () {
    if (trafficPage > 1) { trafficPage -= 1; renderTrafficInsights(); }
  });
  var trafficNext = document.getElementById('traffic-next');
  if (trafficNext) trafficNext.addEventListener('click', function () {
    if (trafficHasMore) { trafficPage += 1; renderTrafficInsights(); }
  });
  renderTrafficInsights();
  renderInteractionInsights();

  var addForm = document.getElementById('whitelist-add-form');
  if (addForm) {
    addForm.addEventListener('submit', function (e) {
      e.preventDefault();
      var input = document.getElementById('whitelist-add-input');
      var ip = (input && input.value) ? input.value.trim() : '';
      if (!ip) return;
      whitelistEdit(ip, true);
      if (input) input.value = '';
    });
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', init);
} else {
  init();
}
