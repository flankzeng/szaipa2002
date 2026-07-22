# 前台图片放大器按需加载（2026-07-22）

本轮继续缩减现代前台的首轮资源，不修改服务器、旧稳定发布版、外接 `Content`、数据库或图片文件。首页和艺术家页的缩略图、原图路径、放大倍率与视觉样式均保持不变。

## 资源加载

- `NewIndex`、`NewArt` 不再在页面打开时加载 jQuery、Magnify 插件和 Magnify CSS。
- 新增带内容版本的共享 `wwwroot/js/magnify-loader.js`。它只为 `.zoom[data-magnify-src]` 绑定轻量原生事件；访客首次 pointer/mouse/touch/focus 交互时才并行加载 CSS 与 jQuery，随后加载插件并初始化当前图片。
- 多张图片共用一次依赖加载；同一图片只初始化一次。依赖请求失败时保留缩略图和原链接，后续交互可重试，不产生未处理 Promise 错误。
- `newindex.js`、`newart.js` 中放大器以外的 jQuery 行为已改为原生 DOM，包括展会筛选/轮播、返回顶部、艺术家导航、Banner 悬停和年表折叠。没有为其他行为继续保留整套 jQuery。
- 动态插入的 vendor CSS 会位于页面样式之后，因此两个页面既有的高度与白色镜片边框使用更明确的页面选择器保持原层叠结果；契约测试防止这项覆盖被意外撤销。

旧发布版中三项依赖合计 `105,919B`（jQuery `89,664B`、插件 `14,961B`、CSS `1,294B`）；共享 loader 为 `8,642B`。因此首页和 NewArt 在访客不使用放大镜时，冷加载原始体积净减少 `97,277B`（约 95KiB，压缩前）。首次使用放大镜时仍加载原插件，显示效果不变。

## 响应式细节

- NewArt 手机导航链接显式保持单行、横向书写。
- 把窄屏右侧箭头内收 `0.25rem`，修复 320px 视口因旋转箭头多出约 1px 的页面级横向滚动。
- 所有新增尺寸均使用 `rem`，没有引入固定像素字号。

## 验证

- `/Home/NewIndex` 1440×900：首次打开没有 jQuery/Magnify CSS/插件请求；展会“正在展出/往期展览”切换和三个实际 Swiper 正常；首次作品交互后依赖各出现一次，放大镜使用原图，重复交互不重复加载；镜片计算样式仍为 `1px solid white`。
- `/Home/NewIndex` 390×844：`scrollWidth=clientWidth=390`，navbar 全部单行横排，手机轮播正常，未交互时只加载共享 loader。
- `/Home/NewArt/1000` 1440×900：年表前五项、全部展开/折叠、单项切换、Banner 悬停与现有 Swiper 正常；作品首次交互后放大镜使用 `works-narrow` 原图，离开边界后正确隐藏；wrapper 计算高度仍为 `315px`（`35vh`），镜片仍为 `1px solid white`。
- `/Home/NewArt/1000` 320/390px：导航链接同一行且横向书写；最终 `scrollWidth=clientWidth`，无页面级横向溢出。
- `/Home/NewArt/1052` 390×844：无作品/无 Banner 数据时不加载 Magnify 依赖，不产生控制台错误或横向溢出。
- 新增 Web 契约测试，防止公开视图重新直接引用 jQuery/Magnify 依赖，并核对两个入口均使用带版本的共享 loader。
- 三个前台 JS 文件均通过 Node `--check`；`npm run build` 的 Tailwind、editor、dashboard bundle 全部通过。
- `~/.dotnet/dotnet build Szaipa.Modernization.slnx --no-restore --disable-build-servers -m:1`：0 warning / 0 error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx --no-build --no-restore --disable-build-servers -m:1`：Data 96/96、Web 122/122，合计 218/218。
