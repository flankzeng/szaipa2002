# 艺术家官网总览与展讯数据化（T4）

## 改动

- 为 `Works` 增加作品集排序 `Sort`，为 `Artist` 增加隐藏默认展讯开关 `HideDefaultExhibitions`，并新增 `ArtistPublication` 艺术家与 `Publication` 的有序关联表；读写上下文都映射新增结构。
- `/Staff/Artist/{id}/Site` 提供作品、资讯、收藏、拍卖、手工展览数量及入口，支持批量上传作品、保存已关联展览与默认活动图开关。新建艺术家完成后直接进入官网总览，会员列表提供「官网」入口。
- Art 展讯读取关联展览封面与链接；EndDate 为空时省略结束日期分隔符。旧库缺 `Works.Sort`、`Artist.HideDefaultExhibitions` 或 `ArtistPublication` 时公开页保留原展示，后台提示升级脚本并禁用相应写功能。
- 兼容旧 `Publication` 为空的部署：后台始终合并 SQL 中的展览行和 14 个固定 ID 兼容展览，并保留同构第二季手写专题作为一个固定兼容项；只有关联ID、没有真实 `Publication` 行时，公开页从既有只读目录补上标题/封面/日期/安全站内链接。已选展讯按关联 `Sort` 顺序渲染。
- 增加 `/Staff/Works` 的艺术家筛选和 ArtistSite 子模块入口/创建预选；旧库缺 `Works.Sort` 时列表展示只读状态，停用新增/编辑/删除入口，详情 API 也返回清晰升级提示。
- 旧库 Artist 列表/编辑查询投影不含 T4 新列；缺列探测同时识别 SQL Server 错误号 207/208（中文错误消息）和 SQLite `no such column/table`。
- 新增 Cropper.js 1.6.2 裁切组件：艺术家头像 1:1 彩色默认，横幅宽幅；OrgMember 头像复用 1:1 并默认勾选黑白。裁切原图单独保存，可按原图重新裁切。
- SQL Server 幂等升级脚本：`docs/sql/2026-10-26-artist-site.sql`。

## 修复的旧问题

艺术家官网作品顺序不可控、展讯硬编码无法维护、艺术家头像和横幅只能直接上传原图；缺少结束日期时展讯会显示悬空的日期分隔符。

## 验证

- 最新 `codex-refactor-dotnet-modernization` 依赖升级已合入 T4 worktree。最终 `~/.dotnet/dotnet build Szaipa.Modernization.slnx --no-restore -m:1`：0 warning / 0 error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx --no-restore -m:1`：全部通过，Data 160 + Web 268 = 428 项；新增缺 Sort/HideDefault 旧库测试、Art 展讯关联顺序/兼容ID测试、原图保存/克隆测试。
- `cd src/Szaipa.Web && npm run build`：成功，生成 Cropper JS/CSS 与其他 bundle；仅提示 Browserslist 本地 caniuse-lite 清单过期。
- 独立本地端口 5064（Development、`LegacyAssets__ContentRoot=/private/tmp/szaipa-t4-assets`、只读调试登录）实测 `/healthz`、`/Home/Art/1000`、`/Staff/Artist`、`/Staff/Artist/Edit/1000`、`/Staff/Works`、`/Staff/Artist/1000/Site` 均为 200。SQL Server 返回中文 207/208 错误时读页成功降级；`/Staff/Works` 响应确认显示禁用写入口的升级提示。测试只读生产源，未执行数据库写操作。
- 未完成：未做镜像库后台写入→前台展示、Cropper 浏览器内实际裁切/批量上传点测、390/1440 视觉截图与溢出检查；交由集成阶段在本地镜像/浏览器完成。

## 用户手动操作

生产发布前由用户自行在读写共用库执行 T4 SQL（不得由 agent 执行）。本地镜像需通过 `mirror-sync` 重建新增实体/列；上传点测时 `LegacyAssets__ContentRoot` 指向临时目录。T4 本轮未操作生产库写入或服务器文件。
