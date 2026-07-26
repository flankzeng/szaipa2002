# Bootstrap CSS 分页退役试点（2026-07-22）

本轮继续缩减现代前台的首轮 CSS，只修改本地现代项目。服务器、旧稳定发布版、外接 `Content`、数据库和图片均未改动。

## 范围与兼容基线

`_newLayout.cshtml` 与 `_Artist.cshtml` 仍默认加载外接 Bootstrap 3 CSS；只有页面显式设置 `ViewData["UseBootstrapCss"] = false` 时，才在相同位置改载带内容版本的 `public-bootstrap-baseline.css`。当前只放行七个没有 Bootstrap 组件或标准 12 栏 class 的页面：

- `/Home/NewIndex`
- `/Home/NewVip`
- `/Home/PublicationList`
- `/Home/NewNews`
- `/Home/NewAbout`
- `/Home/NewArt/{id}`
- `/Home/Publication/{id}`

轻量基线保留这些页面和公共 navbar/footer 实际依赖的 Bootstrap/normalize 行为：`border-box`、body 字体/行高/颜色/背景、HTML5 block 元素、链接状态、图片/figure、标题字重/行高/margin、段落和列表 margin、button/input 表单归一化、NewIndex 的 `.clearfix` 与 36px `h1`、NewArt 导航 table 归一化，以及这些页面相关的打印规则。文件保留 Bootstrap 3.3.7 与 normalize.css 的 MIT 归属说明。

其他页面继续走默认 Bootstrap，不受试点影响。三个 `Layout=null` 的特殊展览不经过公共布局；未经逐页验证的其他页面仍不能直接全站删除 Bootstrap。

## 体积

旧发布版的 `bootstrap.css` 为 `143,947B`；轻量基线为 `3,105B`，每个试点页冷加载原始体积减少 `140,842B`。本机同参数对照：

- gzip -9：`20,871B → 1,260B`，减少 `19,611B`。
- Brotli quality 11：`16,989B → 962B`，减少 `16,027B`。

两者都是一条 CSS 请求，因此请求数不增加；新基线带内容版本，可进入现有一年 immutable 缓存。外接 Bootstrap 文件没有删除，仍供未迁页面使用。

## 验证

- 先在原 Bootstrap 版本记录 NewVip/PublicationList 的 390、768、1440 几何与计算样式快照，再在退出后逐字段对照；除样式表 URL 按设计变化外，body、navbar、标题、Grid/空状态、卡片、图片和两套 footer 的快照完全一致。
- 稳定字体加载后的 390px NewVip 与 1440px PublicationList 截图保持原视觉；PublicationList 当前只读库为空列表，真实空状态一并验证。
- NewVip 验证 320/390/767/768/769/991/992/1279/1280/1440，PublicationList 验证 320/390/768/991/992/1279/1280/1440：18/18 均为 `scrollWidth == clientWidth`，navbar 四项始终单行横排，991/992 footer 切换正确，页面只加载轻量基线而不加载 Bootstrap。
- NewNews 另在 320/390/768/991/992/1279/1280/1440 做退出前后对照：8/8 的 body、navbar、73 行新闻、标题/日期/图片、footer 几何和计算样式逐字段一致，均无横向溢出；1440 截图逐字节一致，390 稳定截图视觉一致。
- NewAbout 另在 320/390/767/768/991/992/1199/1200/1279/1280/1440 做退出前后对照：11/11 的 body、navbar、双端正文/章程图、文档按钮、合作伙伴卡片和 footer 几何及计算样式逐字段一致。按钮在 390px 保持 `54.765625px` 高、`38.376px` 行高、pointer 光标及原生 button 外观；所有断点均无横向溢出，navbar 始终单行横排。
- NewArt 选真实只读样本 1000/1012/1052（44/56/0 件作品），分别在 320/390/767/768/991/992/1199/1200/1279/1280/1440 做 33 组退出前后对照。作品/展览数量、两组 Swiper 初始化、艺术家导航 table、移动订阅 input/button 与双端 footer 均保持一致；33/33 无横向溢出，五项导航始终同一行、横向书写。样本 1000 的一个首屏外 lazy 作品图在两轮采集时分别处于已缓存 250×208 与尚未触发加载 0×0 状态；隔离这三个运行时时序矩形后，33/33 的其余几何和计算样式逐字段一致。390/1440 首屏截图复核正常，jQuery/Magnify 插件仍未在初始加载中出现。
- NewIndex 在 320/390/767/768/991/992/1199/1200/1279/1280/1440 做 11 组退出前后对照：除样式表 URL 外，页面高度、各 section、navbar/footer、订阅 input/button、5 处 `.clearfix` 伪元素及全部代表元素的几何和计算样式逐字段完全一致；在售标题保持 Bootstrap 原值 36px。11/11 无横向溢出，navbar 四项始终单行横排；真实只读数据保持 17 个主轮播、10 位核心成员、2 位顾问、6 件在售、6 条新闻、4 个展会，Swiper 在 320–767 为 2 个、768 起为 3 个。390/1440 首屏截图复核正常，初始加载仍无 jQuery/Magnify 插件。
- 六页累计 81 个响应式检查通过；首页退出后，默认 Bootstrap 回退仍只服务未经逐页验证的页面。
- 新增 Web 契约测试，锁定 opt-out 页面、带版本 baseline 链接、Bootstrap 默认回退及页面 Styles 的加载顺序。
- `~/.dotnet/dotnet build Szaipa.Modernization.slnx --no-restore --disable-build-servers -m:1`：0 warning / 0 error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx --no-build --no-restore --disable-build-servers -m:1`：Data 96/96、Web 123/123，合计 219/219。

## Publication 扩展（2026-07-26）

- 数据展览详情已加入同一 opt-out 契约。普通与重要模板都只使用站点自有 20 栏和 `exh-*` 组件，不依赖 Bootstrap 的标准 12 栏、组件或工具 class。
- 以 131 图的 `/Home/Publication/92012` 做退出前后严格 A/B：390×844 与 1440×900 下，navbar、标题/日期、两组图库、箭头和 footer 的矩形与关键 computed style 逐字段完全一致；破图 0，`scrollWidth == clientWidth`。
- 991/992 两侧另做断点检查：页面均无横向溢出，navbar 高度均为 64px，四项链接均为 `white-space: nowrap` 且保持单行横排。390px 首屏截图复核正常。
- 页面样式表由外接 `143,947B` Bootstrap 改为带版本的 `3,105B` 基线，原始体积少 `140,842B`；以 `gzip -9 -n` 对照为 `20,857B → 1,230B`，冷加载少 `19,627B`，请求数不变。
- 外接 Bootstrap、旧发布版、服务器、数据库、图片与 Content 均未修改；默认回退继续服务其余未验证页面。

## 三个特殊展览扩展（2026-07-26）

- `chunyu3`、`tonggou2`、`tonggou2024` 是不经过公共布局的独立页面，现也在原加载位置直接使用同一带版本轻量基线。
- 三页各自以 390×844、1440×900 做退出前后对照，共 6 组。首轮发现 Bootstrap `.nav` 组件隐式提供 `margin-bottom: 0`，轻量基线会令导航上移 10px；该真实依赖已收回 `publication-special.css` 的 `.nav { margin: 0; }`。复测后 banner、标题、按钮、导航、各内容 section、图库、订阅区和 footer 的全部记录矩形与关键 computed style 均逐字段一致，6/6 无破图或横向溢出。
- 另在三页各验证 320/991/992，共 9 组：`scrollWidth == clientWidth`，所有导航项均保持 `nowrap`、`horizontal-tb`；chunyu3/tonggou2 的移动订阅标题在 320px 为 22.5px、991px 为 32px，保持既有响应式上限。
- 三个页面各自减少与数据展览相同的 `140,842B` 原始 CSS / `19,627B` gzip 冷加载；请求数不变。契约测试锁定三页只能引用轻量基线，不得回退整份 Bootstrap。
