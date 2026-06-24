# 2026-06-24 后台 staff 迁移 + 数据驱动展览 + 前台优化

## 后台 staff（ASP.NET Core，Areas/Admin）
- **基座**：可写 `SzaipaAdminContext`（门控 `AdminWrite:EnableWrites` + 本地副本连接，生产库硬隔离）；cookie 认证替换 `Session["Staff"]`（MD5 兼容老账号）；`Areas/Admin` 外壳 + Tailwind 主题（品牌红 #bf272d，贴近 NewIndex）。
- **编辑器/上传**：TipTap（esbuild 打包 `wwwroot/admin/editor.js`）+ `_RichTextEditor` 组件；`AdminAssetStorage` 唯一 GUID 命名（规避旧版子串匹配删图 bug）；封面 `upload-field.js`。
- **CRUD 模块**：News、ArtNews、Fav/Auction/Exhibition（泛型基类 `ArtistScopedAdminRepository<T>`）、展览(Publication)。统一 `IOperationRecorder` 操作日志、`PagedResult<T>`、`AdminActor`。

## 数据驱动展览（复用 Publication）
- `Publication` 加 `Type/Preface/Signature`（`docs/sql/2026-06-exhibition-template-columns.sql`；两上下文 `Type` 默认 0）。
- 多图画廊上传器：`ExhibitionGalleryFolder`（自动编号 10001+、两段式防碰撞重排、保留封面，含单测）+ `gallery-manager.js`。
- 前台 `Publication.cshtml` 按 `Type` 分支：普通 `_ExhibitionGallery` / 重要 `_ExhibitionImportant`（banner+序+画廊，皮肤 `exhibition-important.css`）。

## 前台公开页优化
- NewIndex/NewNews/NewNewsRead/NewArt 内联 CSS/JS **外提**到 `wwwroot/css|js`（缓存、HTML 瘦身、删死注释）；NewArt 用 CSS 变量 + JS 桥接保留动态值。
- `_newLayout`/`_Artist` 加可选 `Styles`/`Scripts` section。
- 成员头像区 `<table>` + 每人写 2–3 遍 + `xs/md-hide` + opacity 填充 → **扁平 flex 列表，每人一次**；每行人头数 = 一句 CSS（桌面 ~6 / 手机 3）。

## 测试 / 验证
- `~/.dotnet/dotnet build` 0/0；`test` 50 绿（新增 News/ArtNews/艺术家域/画廊/MD5 等 SQLite 实测）。
- 端到端点击验证（建数据/传图→公开页）待本地可写库 + 可写 ContentRoot + 跑 SQL 脚本。

## legacy 旧后台 bug 补丁（szaipa2022）
- `NewsAdd` 补回丢失的正文 + 内联图路径改写；`str2t1` 停止从永久目录 `newsImg` 误删图片。（.NET Framework 栈本机跑不起来，需在可跑该栈的机器重新生成确认编译。）

## 文档
- 新增 `docs/PROJECT_MAP.md`（项目地图）、`docs/HANDOFF.md`（交接 + 模型建议）。
