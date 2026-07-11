# 前台清理 Phase 3：特殊展览页缓存化（2026-07-11）

## 范围

继续优化仍保留特殊视觉结构的三个页面：

- `Publication/chunyu3`
- `Publication/tonggou2`
- `Publication/tonggou2024`

三页继续保留独立 HTML 结构和视觉差异，不迁成普通画廊模板。

## 完成内容

- 把三页所有内联 `<style>` 外提并合并为 `wwwroot/css/publication-special.css`。
- 背景图、标题间距、交易区间距等 5 个页面差异改为 body class 对应的 CSS 变量，没有统一成错误的固定值。
- 把 `chunyu3`、`tonggou2` 的内联脚本合并为 `wwwroot/js/publication-special.js`；轮播初始页通过 `data-initial-slide` 保留原差异。
- `tonggou2024` 的导航自动隐藏行为独有，继续放在 `publication-tonggou2024.js`，没有扩散到其他页面。
- 所有脚本保持在 body 末尾加载，避免依赖 DOM 的旧代码提前执行。
- 远程 `$webfont` 增加存在性保护；CDN 可用时字形不变，CDN 失败时不再阻断页面其余交互。

## 体积与缓存

- 三个 Razor 页面原始合计约 205KB；外提去重后页面 HTML 合计约 139KB。
- 共享 CSS 约 19KB；共享 JS 约 7KB，后续页面访问可以直接命中缓存。
- Phase 2 Release publish 为 24,080KB；Phase 3 为 24,036KB。包体只小幅下降，因为 CSS/JS 仍属于发布资产；主要收益来自 HTML 传输减少和跨页面缓存。
- 三页此前移除的 Vue、Element Plus、Bootstrap JS 以及图片懒加载继续保留。

## 字体审计

- 本地存在 Noto Sans SC / Noto Serif SC 多字重原文件，可以用同字形子集继续优化。
- `JhengLight/JhengRegular/JhengBold` 实际由 Youziku 加载 Alibaba 普惠体，不是本地 `MicrosoftJhengHei*`。
- 仓库没有 Alibaba 普惠体原文件，因此本轮没有用微软正黑、Noto 或系统字体冒充替换。
- 在取得合法且准确的 Alibaba 普惠体源文件前，保留远程字体；仅增加失败保护。

## 验证

- `.NET build`：0 warning / 0 error。
- tests：94/94。
- npm build：通过。
- `node --check`：两份页面脚本均通过。
- 三个 Razor 页面不再含内联 `<style>` 或无 `src` 的 `<script>`。
- 已移除旧 CSS/JS 文件名、Vue/Element Plus/Bootstrap JS 和已知 404 footer 链接的悬空引用。
