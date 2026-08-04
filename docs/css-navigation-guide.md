# CSS 导航说明书

快速理解两套区域（公开页面 / Staff 后台）各自加载哪些 CSS、顺序如何、每个文件管什么。

---

## 一、公开页面

公开页面有两个共享布局（`_PublicLayout` / `_ArtistLayout`），加载链几乎相同。
三个独立页面（`NewsRead` / `chunyu3` / `tonggou2` / `tonggou2024`）使用 `Layout = null`，
自己声明 `<head>`，但也加载相同的地基层。

### 1.1 加载链

```
① public-core.css          ← 地基层：reset / @font-face / 20 列栅格 / 全站 CSS 变量 / 根字号
② font-subsets-public.css  ← 现代 Noto 子集字体（Szaipa Noto Sans/Serif SC）
③ public-layout.css        ← 组件层：导航栏 / 邮件订阅 / 底栏 / Swiper 分页器 / 艺术家子导航
④ (Swiper 按需)            ← public/vendor/swiper-public.css
⑤ @RenderSection("Styles") ← 页面私有 CSS（大页面独立文件，小页面合集 public-pages.css）
```

### 1.2 设计理念

对标 legacy 时期的分层结构（`Site.css` 地基 + `responsive.css` 参数总表 + `Artist.css` 例外），
**按概念分层，不按页面拆分**：

| 层 | 文件 | 对标 legacy | 内容 |
|---|---|---|---|
| 地基 | `public-core.css` | `Site.css` + `responsive.css` 变量部分 | reset · 19 个 @font-face · 20 列栅格 · 全部 :root 变量（~80 个，按页面/断点分段）· 根字号 · a:focus 重置 |
| 组件 | `public-layout.css` | `responsive.css` 选择器部分 + `Artist.css` 组件部分 | 导航栏 · 邮件区 · 底栏 · Swiper 分页器 · 艺术家子导航 |
| 页面 | `public-pages.css` | — | 小页面 CSS 合集（About / Vip / News / ArtistContent / ExhibitionImportant / PublicationList） |

大页面保持独立：`index.css`（1439 行）· `newsread.css`（747 行）· `art.css`（400 行）· `publication-special.css`（584 行）。

---

## 二、Staff 后台

### 2.1 `_StaffLayout.cshtml` — 所有 `/Staff/*` 管理页面

```
① font-subsets-staff.css          ← Staff 专用 Noto 子集
② admin/admin.css                 ← Tailwind CSS v3.4 编译产物（preflight + 组件类）
```

> Staff 区不加载任何公开页面的 CSS。修改 Staff 样式后需运行 `npm --prefix src/Szaipa.Web run build` 重新编译 Tailwind。

---

## 三、CSS 文件速查表

| 文件 | 加载区域 | 内容 |
|---|---|---|
| `public-core.css` | 公开 | 地基层：reset、@font-face、20 列栅格、全站 CSS 变量、根字号 |
| `public-layout.css` | 公开 | 组件层：导航栏、邮件、底栏、Swiper、艺术家子导航 |
| `public-pages.css` | 公开 | 页面合集：About、Vip、News 列表、Artist 内容区、重要展览皮肤、展会列表 |
| `font-subsets-public.css` | 公开 | 本地 Szaipa Noto 子集 @font-face |
| `font-subsets-staff.css` | Staff | Staff 专用 Noto 子集 |
| `index.css` | 公开 | 首页独有样式（轮播/章程/成员/新闻/展会六段，1439 行） |
| `newsread.css` | 公开 | 新闻详情页 + 响应式文章排版（747 行） |
| `art.css` | 公开 | 艺术家详情页 |
| `publication-special.css` | 公开 | 特殊展览页（春语3/同构2/同构2024）+ Publication 响应式 |
| `site.css` | — | 旧版 dashboard 着陆页（`_Layout.cshtml` 专用，非公开/Staff） |
| `admin/admin.input.css` | Staff 源 | Tailwind 源文件（`@tailwind base/components/utilities` + 自定义组件层） |
| `admin/admin.css` | Staff 产 | Tailwind 编译产物（不要直接编辑，改 input 后 `npm run build`） |

### 页面 CSS 加载映射

| 视图 | 加载的页面 CSS |
|---|---|
| `Index.cshtml` | `index.css` |
| `News.cshtml` | `public-pages.css`（§3 News 段） |
| `NewsRead.cshtml` | `newsread.css` |
| `Art.cshtml` | `art.css` |
| `ArtArchive.cshtml` | `public-pages.css`（§4 Artist Content 段） |
| `ArtArticle.cshtml` | `public-pages.css`（§4 Artist Content 段） |
| `About.cshtml` | `public-pages.css`（§1 About 段） |
| `Vip.cshtml` | `public-pages.css`（§2 Vip 段） |
| `Constitution.cshtml` | 无额外 CSS（使用 public-core 变量） |
| `Publication.cshtml` | `publication-special.css` +（Type=Important 时 `public-pages.css` §5） |
| `PublicationList.cshtml` | `public-pages.css`（§6 Publication List 段） |
| `chunyu3 / tonggou2 / tonggou2024` | `publication-special.css` |

---

## 四、CSS 变量（CSS Custom Properties）分布

所有变量集中在 **`public-core.css` §4** 中，按用途分段：

| 段 | 内容 | 来源 |
|---|---|---|
| §4.1 | 全局默认值（导航/邮箱/底栏/轮播/章程/新闻/展会） | responsive.css |
| §4.2 | 桌面层 ≥768 覆盖 | responsive.css |
| §4.3 | 手机+平板 ≤991 覆盖 | responsive.css |
| §4.4 | 首页专用 手机层 ≤991 | responsive.css |
| §4.5 | 其余页面 桌面层 ≥768 | responsive.css |
| §4.6 | 其余页面 手机层 ≤991 | responsive.css |
| §4.7 | 艺术家页 全局默认 | Artist.css |
| §4.8 | 艺术家页 平板 768–991 | Artist.css |

> 修改变量值时**打开 `public-core.css` 一个文件即可**，所有 var() 消费处自动生效。

---

## 五、修改 CSS 后的操作

| 场景 | 操作 |
|---|---|
| 改 `wwwroot/css/*.css` | 无需额外步骤，刷新即生效 |
| 改 `wwwroot/admin/admin.input.css` | 运行 `npm --prefix src/Szaipa.Web run build` 重新编译 Tailwind |
| 改 `.cshtml` 视图 | 需 `dotnet build` 重新编译（视图预编译进 DLL） |
| 改 `tailwind.config.js` | 运行 `npm --prefix src/Szaipa.Web run build` |
