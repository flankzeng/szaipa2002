# Bootstrap CSS 分页退役试点（2026-07-22）

本轮继续缩减现代前台的首轮 CSS，只修改本地现代项目。服务器、旧稳定发布版、外接 `Content`、数据库和图片均未改动。

## 范围与兼容基线

`_newLayout.cshtml` 仍默认加载外接 Bootstrap 3 CSS；只有页面显式设置 `ViewData["UseBootstrapCss"] = false` 时，才在相同位置改载带内容版本的 `public-bootstrap-baseline.css`。当前只放行三个没有 Bootstrap 组件或标准 12 栏 class 的页面：

- `/Home/NewVip`
- `/Home/PublicationList`
- `/Home/NewNews`

轻量基线保留这些页面和公共 navbar/footer 实际依赖的 Bootstrap/normalize 行为：`border-box`、body 字体/行高/颜色/背景、HTML5 block 元素、链接状态、图片/figure、标题字重/行高/margin、段落和列表 margin，以及这些页面相关的打印规则。文件保留 Bootstrap 3.3.7 与 normalize.css 的 MIT 归属说明。

其他页面继续走默认 Bootstrap，不受试点影响。特别是 NewIndex 仍有 `.clearfix` 依赖，不能直接全站删除；NewAbout、数据展览和特殊展览也继续保留原加载边界。

## 体积

旧发布版的 `bootstrap.css` 为 `143,947B`；轻量基线为 `2,335B`，每个试点页冷加载原始体积减少 `141,612B`。本机同参数对照：

- gzip -9：`20,871B → 1,011B`，减少 `19,860B`。
- Brotli quality 11：`16,989B → 765B`，减少 `16,224B`。

两者都是一条 CSS 请求，因此请求数不增加；新基线带内容版本，可进入现有一年 immutable 缓存。外接 Bootstrap 文件没有删除，仍供未迁页面使用。

## 验证

- 先在原 Bootstrap 版本记录 NewVip/PublicationList 的 390、768、1440 几何与计算样式快照，再在退出后逐字段对照；除样式表 URL 按设计变化外，body、navbar、标题、Grid/空状态、卡片、图片和两套 footer 的快照完全一致。
- 稳定字体加载后的 390px NewVip 与 1440px PublicationList 截图保持原视觉；PublicationList 当前只读库为空列表，真实空状态一并验证。
- NewVip 验证 320/390/767/768/769/991/992/1279/1280/1440，PublicationList 验证 320/390/768/991/992/1279/1280/1440：18/18 均为 `scrollWidth == clientWidth`，navbar 四项始终单行横排，991/992 footer 切换正确，页面只加载轻量基线而不加载 Bootstrap。
- NewNews 另在 320/390/768/991/992/1279/1280/1440 做退出前后对照：8/8 的 body、navbar、73 行新闻、标题/日期/图片、footer 几何和计算样式逐字段一致，均无横向溢出；1440 截图逐字节一致，390 稳定截图视觉一致。
- NewIndex 在 390/1440 的对照验证仍加载 Bootstrap、不加载轻量基线，navbar、footer 和横向宽度正常。
- 新增 Web 契约测试，锁定 opt-out 页面、带版本 baseline 链接、Bootstrap 默认回退及页面 Styles 的加载顺序。
- `~/.dotnet/dotnet build Szaipa.Modernization.slnx --no-restore --disable-build-servers -m:1`：0 warning / 0 error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx --no-build --no-restore --disable-build-servers -m:1`：Data 96/96、Web 123/123，合计 219/219。
