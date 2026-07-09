# 2026-07-09 展览「参展作品目录」

## 概述
为「重要」类型展览（`Publication.Type == 1`）新增可选的「参展作品目录」子模块：展示本次展览收录的作品列表（分类/标题/艺术家/尺寸/材质/图片），支持排序。依附于 Publication，不做独立主表 CRUD 路由，管理方式与既有「展览图片画廊」并列，复用同一套 UI 语言（后台内嵌管理器组件 + ↑↓排序 + 逐条删除）。

## 数据模型
- 新实体 `Szaipa.Data.Contexts.Szaipa.ExhibitionWork`：`Id`, `PublicationId`(FK), `Category`, `Title`, `Artist`, `Size`, `Medium`, `ImagePath`, `SortOrder`。
- 两个上下文都注册了该实体：`SzaipaLegacyReadContext`（只读，投影）+ `SzaipaAdminContext`（可写，`SortOrder` 给 `HasDefaultValue(0)`，与 `Publication.Type` 同一坑位）。
- SQL 迁移脚本：`docs/sql/2026-07-09-exhibition-works-catalog.sql`（新建 `dbo.ExhibitionWork` 表 + FK + 索引，`OBJECT_ID` 守卫可重复执行）。**需要用户在本地可写副本（+ 如需公开页联调也要在读库）上手动执行**，agent 未对任何库做写操作。

## 写侧（后台）
- `IExhibitionWorkAdminRepository` / `ExhibitionWorkAdminRepository`（`Services/Admin/`）：`GetByPublicationAsync` + `ReplaceAsync`（整表替换语义——删除该展览下全部旧行、按提交顺序以全新连续 `SortOrder` 重新插入，与画廊的 `ApplyOrder` 思路一致，避免部分更新的增量 diff 复杂度），走 `IOperationRecorder` 记操作日志。DI 注册在 `SzaipaDataServiceCollectionExtensions`。
- `PublicationController` 新增 `GET Works/{id}`（给编辑页管理器拉取现有作品列表）；`Create`/`Edit` POST 在 `SyncGalleryAsync` 之后新增 `SyncWorksAsync`，解析 `PublicationFormViewModel.WorksJson`（JSON 数组，隐藏字段，由前端组件在提交时写入）并调用 `ReplaceAsync`。`WorksJson` 为 `null`（组件未接触过）时保留原有目录不动；显式提交 `"[]"` 则清空。
- 单张作品图片**不需要**画廊那种 gap-free 连续编号（每条作品独立、互不引用同一张图），直接复用通用单图上传 `/Staff/Upload/Image`（`AdminAssetStorage`，唯一 GUID 命名），子目录固定为 `images/{FolderName}/works`，与画廊图片同层级目录结构。
- 前端组件 `wwwroot/admin/works-manager.js`（纯 JS，无打包，仿 `gallery-manager.js` 的结构）：每行 = 缩略图（点击上传）+ 分类/标题/艺术家/尺寸/材质 五个文本框 + ↑↓/删除按钮；提交时把整个数组序列化进隐藏字段。编辑页加载时按 `data-publication-id` 拉取既有列表；新建页从空列表开始。
- 表单 `Areas/Staff/Views/Publication/_Form.cshtml` 在画廊区块下方新增「参展作品目录」区（提示仅重要类型公开页展示，不做 Type 联动隐藏，与「序」区块的既有处理方式一致）；`Create.cshtml`/`Edit.cshtml` 加载 `works-manager.js`。
- Tailwind 新增 `.works-list`/`.works-row` 系列工具类（`admin.input.css`，中文注释）。

## 读侧（公开页）
- `PublicationDetailModel` 新增 `Works`（`IReadOnlyList<ExhibitionWorkModel>`，默认空数组）；`SzaipaHomeProjections.ExhibitionWorkSummary` 只 `SELECT` 展示需要的列。
- `PublicationReadRepository.GetPublicationDetailSnapshotAsync` 在拿到展览详情后按 `PublicationId` 查 `ExhibitionWork`（`OrderBy SortOrder, ThenBy Id`），非空时重建 `detail`（因该类型是 `init`-only、非 record，手工拷贝全部字段）附加 `Works`。
- Web 层 `ExhibitionGalleryModel` 新增同名 `Works` 属性；`Views/Home/Publication.cshtml` 组装 gallery model 时带上 `publication.Works`。
- `Views/Shared/_ExhibitionImportant.cshtml` 在「序」之后、「现场照片」之前新增「参展作品目录」区块（`Model.Works.Count == 0` 时整块不渲染），卡片网格：图 + 分类标签 + 标题 + 艺术家 + 尺寸/材质。样式新增于 `wwwroot/css/exhibition-important.css`（`.exh-works*`，中文注释），响应式（`auto-fill minmax` 网格，移动端更窄的最小列宽）。

## 测试 / 验证
- 新增 `ExhibitionWorkAdminRepositoryTests`（5 个）：插入排序/连续 `SortOrder`、二次保存重排+丢弃、清空目录、按展览隔离、操作记录写入。
- 扩展 `PublicationReadRepositoryTests`（+2）：`GetPublicationDetailSnapshotAsync` 按 `SortOrder` 返回作品列表、未配置时返回空数组。
- `~/.dotnet/dotnet build` 0 警告 0 错误；`test` 75 → **82** 全绿。`npm run build`（css + editor + dashboard）通过。
- 路由冒烟（无可写库环境）：`/Staff/Publication` 302、`/Staff/Publication/Works/1` 302（未登录跳登录页，符合预期）。

## 留给用户本地验证
- 执行 `docs/sql/2026-07-09-exhibition-works-catalog.sql`（本地可写副本；如需端到端在公开页看到目录，读库也要执行）。
- 本地配好可写 `AdminWrite` + `LegacyAssets:ContentRoot` 后，登录 `/Staff/Publication`，编辑一个 `Type=1` 的展览，添加/排序/删除作品条目并保存，核对 `/Home/Publication/{id}` 公开页「参展作品目录」区块渲染与图片路径。
