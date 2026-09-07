// Dashboard charts for the staff backend (Areas/Staff Dashboard/Index). Bundled by esbuild into
// wwwroot/admin/dashboard.js. Replaces the legacy ECharts-from-CDN dashboard (StaffController.Index):
// daily-visit line, content-access doughnut, and month/year province→city sunbursts. Modular ECharts
// imports keep the bundle small (only the chart types/components we use are included).
import * as echarts from 'echarts/core';
import { LineChart, PieChart, BarChart } from 'echarts/charts';
import { TooltipComponent, GridComponent, LegendComponent } from 'echarts/components';
import { LabelLayout } from 'echarts/features';
import { CanvasRenderer } from 'echarts/renderers';

echarts.use([
  LineChart, PieChart, BarChart,
  TooltipComponent, GridComponent, LegendComponent,
  LabelLayout, CanvasRenderer,
]);

// Brand-aligned palette (brand red #bf272d first; warm/neutral supports).
var PALETTE = ['#bf272d', '#d97b54', '#3e6b8a', '#8a9b6e', '#c9a45c', '#7a6f8f', '#939393'];
var INK = '#2b2b2b';
var MUTED = '#939393';

function getJson(url) {
  return fetch(url, { headers: { 'X-Requested-With': 'fetch' } }).then(function (r) {
    if (!r.ok) throw new Error('请求失败：' + r.status);
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

function showEmpty(el) {
  if (el) el.innerHTML = '<div class="flex h-full items-center justify-center text-sm text-muted">暂无数据</div>';
}

function dailyVisits(chart, el) {
  getJson('/Staff/Dashboard/DailyVisits?days=30').then(function (d) {
    if (!d.categories || d.categories.length === 0) { showEmpty(el); return; }
    chart.setOption({
      color: [PALETTE[0]],
      tooltip: { trigger: 'axis', axisPointer: { type: 'line' } },
      grid: { left: 8, right: 16, bottom: 8, top: 16, containLabel: true },
      xAxis: { type: 'category', boundaryGap: false, data: d.categories, axisLine: { lineStyle: { color: '#e5e5e5' } }, axisLabel: { color: MUTED } },
      yAxis: { type: 'value', splitLine: { lineStyle: { color: '#f0f0f0' } }, axisLabel: { color: MUTED } },
      series: [{
        name: '当日访问数', type: 'line', smooth: true, showSymbol: false, data: d.data,
        areaStyle: { color: 'rgba(191,39,45,0.08)' }, lineStyle: { width: 2 },
      }],
    });
  }).catch(function () { showEmpty(el); });
}

function contentAccess(chart, el) {
  getJson('/Staff/Dashboard/ContentAccess').then(function (d) {
    var items = d.items || [];
    if (items.length === 0 || items.every(function (i) { return !i.value; })) { showEmpty(el); return; }
    chart.setOption({
      color: PALETTE,
      tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
      legend: { bottom: 0, textStyle: { color: INK } },
      series: [{
        name: '访问构成', type: 'pie', radius: ['42%', '68%'], center: ['50%', '44%'],
        avoidLabelOverlap: true, itemStyle: { borderColor: '#fff', borderWidth: 2 },
        label: { show: false }, labelLine: { show: false },
        data: items.map(function (i) { return { name: i.name, value: i.value }; }),
      }],
    });
  }).catch(function () { showEmpty(el); });
}

// 访客分布：横向柱状图（分类多时标签会挤压，旭日图看不全）。折前 GEO_TOP 名，其余并进「其他」；
// 点击省份钻取到城市分布，返回按钮回到全国视图。
var GEO_TOP = 10;

function foldTop(items) {
  var sorted = items.slice().sort(function (a, b) { return b.value - a.value; });
  var top = sorted.slice(0, GEO_TOP);
  var rest = sorted.slice(GEO_TOP).reduce(function (s, x) { return s + x.value; }, 0);
  if (rest > 0) top.push({ name: '其他', value: rest });
  return top;
}

function geoBarOption(items, title) {
  var opt = {
    color: PALETTE,
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
      formatter: function (ps) {
        if (!ps || !ps.length) return '';
        return ps[0].name + '：' + ps[0].value;
      }
    },
    grid: { left: 8, right: 52, bottom: 8, top: title ? 24 : 8, containLabel: true },
    xAxis: {
      type: 'value', splitLine: { lineStyle: { color: '#f0f0f0' } }, axisLabel: { color: MUTED }
    },
    yAxis: {
      type: 'category', inverse: true,
      data: items.map(function (i) { return i.name; }),
      axisLine: { lineStyle: { color: '#e5e5e5' } },
      axisLabel: { color: INK, fontSize: 11 }
    },
    series: [{
      type: 'bar', barMaxWidth: 20,
      data: items.map(function (i) { return i.value; }),
      itemStyle: { color: PALETTE[0], borderRadius: [0, 3, 3, 0] },
      label: { show: true, position: 'right', color: MUTED, fontSize: 11 }
    }]
  };
  if (title) {
    opt.title = { text: title, left: 0, top: 0, textStyle: { fontSize: 12, color: INK } };
  }
  return opt;
}

function geo(chart, el, range, backBtn) {
  getJson('/Staff/Dashboard/Geo?range=' + range).then(function (d) {
    var data = d.data || [];
    if (data.length === 0) { showEmpty(el); return; }
    showGeoProvinces(chart, data, backBtn);
  }).catch(function () { showEmpty(el); });
}

function showGeoProvinces(chart, provinces, backBtn) {
  if (backBtn) { backBtn.classList.add('hidden'); backBtn.onclick = null; }
  chart.setOption(geoBarOption(foldTop(provinces)), true);
  chart.off('click');
  chart.on('click', function (params) {
    if (!params || !params.name || params.name === '其他') return;
    var p = provinces.find(function (x) { return x.name === params.name; });
    if (p && p.children && p.children.length) {
      showGeoCities(chart, p, provinces, backBtn);
    }
  });
}

function showGeoCities(chart, province, provinces, backBtn) {
  chart.setOption(geoBarOption(foldTop(province.children), province.name), true);
  chart.off('click');
  if (backBtn) {
    backBtn.classList.remove('hidden');
    backBtn.onclick = function () { showGeoProvinces(chart, provinces, backBtn); };
  }
}

function init() {
  var daily = mount('chart-daily');
  if (daily) dailyVisits(daily, document.getElementById('chart-daily'));
  var content = mount('chart-content');
  if (content) contentAccess(content, document.getElementById('chart-content'));
  var month = mount('chart-geo-month');
  if (month) geo(month, document.getElementById('chart-geo-month'), 'month', document.getElementById('geo-month-back'));
  var year = mount('chart-geo-year');
  if (year) geo(year, document.getElementById('chart-geo-year'), 'year', document.getElementById('geo-year-back'));
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', init);
} else {
  init();
}
