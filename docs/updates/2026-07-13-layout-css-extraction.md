# 公共布局 CSS 外提（2026-07-13）

## 范围与边界

- 只修改本地 ASP.NET Core modernization 项目；未连接、修改或发布生产服务器。
- 检查了 `_newLayout.cshtml` 与 `_Artist.cshtml` 的 `<style>`：没有依赖运行时数据的 Razor CSS 表达式。
  - `_newLayout.cshtml` 只有 Razor 为输出 `@media` 使用的 `@@media`，外提后恢复为标准 `@media`。
  - `_Artist.cshtml` 只有一段已注释的 `background-color` Razor 注释，不影响生成 CSS；外提时未启用该属性。
- 页面自己的 `Styles` section 仍在布局 CSS 之后，保留原有覆盖顺序。

## 实施

- 两个布局在原 `<style>` 的级联位置改为带 `asp-append-version="true"` 的样式链接。
- `public-layout-common.css` 保存两个布局完全共享的邮箱、页脚和版权规则。
- `public-layout-main.css` 保存主站 navbar、主站差异和响应式规则。
- `public-layout-artist.css` 保存艺术家子导航、艺术家页差异和 `#ArtistNav` 规则。
- 加载顺序固定为“公共规则 → 布局差异 → 页面 Styles section”，因此页面覆盖能力不变。
- 实际渲染的三个链接均带内容哈希 `?v=...`；现有 `StaticAssetCachePolicy` 会把有版本的 webroot 资源按版本化静态资源缓存。

## HTML 与传输体积

本地开发服务器的未压缩响应（`curl | wc -c`）：

| 页面 | 修改前 HTML | 修改后 HTML | 单次 HTML 减少 |
|---|---:|---:|---:|
| `/Home/newIndex` | 74,951 B | 65,326 B | 9,625 B（12.84%） |
| `/Home/newArt/1000` | 81,523 B | 74,322 B | 7,201 B（8.83%） |

新 CSS 的未压缩大小：公共 3,968 B、主站 3,206 B、艺术家 1,034 B，三者均返回 HTTP 200。即使按首次访问还未缓存计算：

- 首页 HTML + 公共/主站 CSS 合计仍减少 2,451 B（3.27%，不计请求头和压缩）。
- NewArt HTML + 公共/艺术家 CSS 合计仍减少 2,199 B（2.70%，不计请求头和压缩）。
- 后续访问可复用浏览器缓存，页面 HTML 不再重复携带布局 CSS；文件内容变化时版本查询参数会自动更新。

## 浏览器回归

在改动前后分别检查 `/Home/newIndex` 与 `/Home/newArt/1000`，视口为 390×844 和 1280×720。比较时排除预期变化的 HTML 字符数和内联样式字符数，其余采集的关键 computed style、元素尺寸和位置完全一致（四组均为 `true`）。

| 页面 / 视口 | 横向溢出 | 导航结果 |
|---|---|---|
| 首页 390×844 | `scrollWidth = clientWidth = 390` | 4 个 navbar 链接均 `white-space: nowrap`、`writing-mode: horizontal-tb` |
| 首页 1280×720 | `scrollWidth = clientWidth = 1280` | 4 个 navbar 链接保持单行横排 |
| NewArt 390×844 | `scrollWidth = clientWidth = 390` | `#ArtistNav` 的尺寸、位置、字号、行高与改动前一致 |
| NewArt 1280×720 | `scrollWidth = clientWidth = 1280` | `#ArtistNav` 的尺寸、位置、字号、行高与改动前一致 |

首页布局内联样式由 9,817 个字符降为 0；NewArt 仍有 192 个字符的页面专属 `Styles` section，布局本身的静态 CSS 已全部外提。

## 自动验证

- `dotnet build Szaipa.Modernization.slnx --disable-build-servers -m:1`：0 warning / 0 error。
- `dotnet test Szaipa.Modernization.slnx --no-build --disable-build-servers -m:1`：110/110 通过（Data 96、Web 14）。
- `npm --prefix src/Szaipa.Web run build`：CSS、editor JS、dashboard JS 全部构建通过。
- `git diff --check`：通过。
