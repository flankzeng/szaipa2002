# 更新 — 2026-08-04：断开 legacy CSS 引用，规则迁移至现代项目

此前公开页面（`_PublicLayout` / `_ArtistLayout`）仍在加载 3 个 legacy 文件：
`szaipa2022/Content/Model/css/bootstrap.css`（8301 行）、`Site.css`（681 行）、
`responsive.css`（487 行），外加艺术家页的 `responsive/Artist.css`（92 行）。
合计 **9469 行**，其中 ~8880 行为未被现代视图引用的冗余样式。

本次将所有实际使用的规则迁移至 `wwwroot/css/` 下，legacy 文件原地保留但现代项目不再引用。

## 1. 新建文件

### `wwwroot/css/public-grid.css`
从 `Site.css` 迁移的 **20 列响应式栅格** + **响应式隐藏类**。

| 内容 | 说明 |
|---|---|
| `.col-xs/sm/md/lg-1~20` | 20 列浮动栅格，断点 767 / 768–991 / 992–1199 / ≥1200 |
| `.xs-hide / .sm-hide / .md-hide / .lg-hide` | 按断点 `display: none !important` |

### `wwwroot/css/font-legacy.css`
从 `Site.css` 迁移的 **19 个 `@font-face` 声明**。字体族包括：微软正黑（`JhengBold/Regular/Light`）、
DIN（`dinLight/Italic`）、得意黑（`smile`）、思源宋体系列（`cjk*`）、思源黑体系列（`sans*`）。
路径由相对 `../../fonts/` 改为绝对 `/Content/fonts/`（继续读取外部 Content 目录的字体文件）。

## 2. 迁移至已有文件的规则

### `public-layout-common.css`
- **CSS 自定义属性**：从 `responsive.css` 迁入全部 `:root` 变量（导航/邮箱/底栏/轮播/章程/新闻/展会/通用），
  含桌面层（≥768）、手机+平板（≤991）、首页专用、其余页面（Constitution/Publication）共 6 组定义块
- **Swiper 分页器覆盖**：≤991 下 `.mySwiper .swiper-pagination-bullet` 等
- **`!important` 移除**：`font-size: clamp(...)` 不再需要 `!important`（之前为对抗 responsive.css 的 `26px !important`）

### `public-layout-artist.css`
- 从 `Artist.css` 迁入艺术家页全部 `:root` 变量（`--touxiang`、`--subnav*`、`--swiperH`、
  `--zuopin*`、`--xijie*`、`--artistNews*` 等）及平板响应式覆盖（768–991）

### `index.css`
- 从 `responsive.css` 迁入首页 ≤991 响应式选择器：`.chapterTXT`、`.chapterAll`、`.allNews`、
  `.allNews a`、`.processing-lunbo`

### `publication-special.css`
- 从 `responsive.css` 迁入 Publication ≤991 响应式选择器：`.imgCon_Pub`（桌面层）、
  `.elementPub`、`.imgCon_PubSon`、`.imgCon_PubMobCon`、`.imgCon_PubMob`、`.mob`、
  `.trading`、`.mySwiper2`、`.type1`、`.type2`、`.btn1`、`.btn2`、`.introCon .msg p`、
  `.trading-copyright`
- **`!important` 移除**：`font-size: clamp(...)` 不再需要

### `newsread.css`
- **`!important` 移除**：`font-size: clamp(...)` 不再需要

## 3. 布局文件变更

### `_PublicLayout.cshtml`
移除 3 个 legacy `<link>`，移除 `UseBootstrapCss` 条件分支，新增 2 个 `<link>`：

| 移除 | 新增 |
|---|---|
| `/Content/Model/css/bootstrap.css` | `~/css/font-legacy.css` |
| `/Content/Model/css/Site.css` | `~/css/public-grid.css` |
| `/Content/Model/css/responsive.css` | — |

最终加载顺序：`baseline → font-legacy → font-subsets-public → grid → common → main`

### `_ArtistLayout.cshtml`
移除 4 个 legacy `<link>`（bootstrap.css / Site.css / Artist.css / responsive.css），
移除 `UseBootstrapCss` 条件分支，新增 2 个 `<link>`（font-legacy / grid）。
`useSwiperCss` 分支保留不变。

## 4. 未迁移的规则（确认丢弃）

| 文件 | 规则 | 原因 |
|---|---|---|
| `bootstrap.css` | 全部 8301 行 | Bootstrap 3 完整版，现代视图无引用 |
| `Site.css` | `.re` `.padding` `.relative` `.notosans` `.notoserif` 字重类 `@font-feature-values` | 0 引用 |
| `responsive.css` | `html` font-size 阶梯（26px/12px）| 已被现代 clamp 方案取代 |
| `responsive.css` | `@keyframes zoomDot1 zoomDot04` | 0 引用 |
| `responsive.css` | `.news h1` `#ended` `.zhushi` `.article*` `.after::after` | 0 引用 |
| `Artist.css` | `.jieshao` `.xijie img` | 0 引用 |

## 5. 验证

- `dotnet build Szaipa.Web.csproj -c Debug` — 0 错误 0 警告
- 首页 HTTP 200，70,178 字节，栅格/banner/hide 类/字体均正常
- 所有新 CSS 文件通过 StaticFileMiddleware 正确送达（HTTP 200）
- 服务器日志无异常或缺失资源请求

## 7. 2026-08-04 同日下午：按 legacy 分层合并（去碎片化）

初次迁移产生了 **19 个 CSS 文件**（含 6 个 <200 行的碎片文件），维护者反馈认知负担过高，
与 legacy 时期 3 文件（`Site.css` + `responsive.css` + `Artist.css`）的简洁结构差距太大。

本次重构对标 legacy 的分层思路，将 19 个文件合并为 **7 个功能文件**：

### 合并规则

| 新文件 | 合并来源 | 定位 |
|---|---|---|
| `public-core.css` | baseline + font-legacy + grid + layout-common 变量部分 + layout-artist 变量部分 | **地基**（对标 `Site.css` + `responsive.css` 变量） |
| `public-layout.css` | layout-main + layout-common 组件部分 + layout-artist 组件部分 | **组件**（对标 `responsive.css` 选择器 + `Artist.css` 组件） |
| `public-pages.css` | about + vip + news + artist-content + exhibition-important + publication-list | **小页面合集**（每个 <300 行的页面 CSS 合并） |

### 保留独立

| 文件 | 原因 |
|---|---|
| `index.css` | 1439 行，首页专用 |
| `newsread.css` | 747 行，文章排版逻辑复杂 |
| `art.css` | ~400 行，艺术家详情 |
| `publication-special.css` | 584 行，特殊展览逻辑复杂 |

### 附加修复

- **NewsRead / chunyu3 / tonggou2 / tonggou2024**（`Layout=null` 独立页）仍直接引用 legacy `responsive.css` + `Site.css`，一并替换为 `public-core.css`
- 两个 email partial 的 stale 注释更新（`public-layout-common.css` → `public-layout.css`）

### 结果

- CSS 文件数：19 → 11（删除 12，新建 3，净减 8）
- 公开页面加载的 `<link>` 数：6 → 4（`_PublicLayout`）、7 → 4/5（`_ArtistLayout`）
- 改 CSS 变量的工作流：打开 `public-core.css` 一个文件即可（不再跨 3 个文件搜索）
- 对标 legacy 的维护体验：`public-core.css`（地基）= `Site.css` + `responsive.css` 变量部分；修改时一目了然
