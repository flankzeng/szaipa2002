# 2026-06-24 后台 Works（作品）CRUD 模块

## 新增模块：作品管理（Phase 3 收尾完成）
- Works 实现 `IArtistScopedRecord`，照 Fav/Auction/Exhibition 范本走泛型基类：
  - `WorksAdminRepository : ArtistScopedAdminRepository<Works>`（`src/Szaipa.Data/Services/Admin/`）—— `Noun="作品"`，`ApplyEditableFields` 只拷贝 ArtistId/Title/Content/Tags，图片字段(Path)空值不覆盖（cover-preserve-on-empty，同 Fav/Exhibition 模式）。
  - 控制器 `Areas/Admin/Controllers/WorksController.cs`：Index/Create/Edit/Delete + 艺术家下拉（`GetArtistOptionsAsync`）。
  - 视图：`Index.cshtml` 复用共享 `_ArtistScopedList`；`_Form/Create/Edit.cshtml` 自带（图片上传 + 艺术家下拉 + 文本字段）。
  - 导航 `_AdminLayout.cshtml` 加「作品管理」入口；DI 注册 `WorksAdminRepository`。

## legacy 调研发现：「ArtWorks」不是独立模块，是重复/打架的两套旧写入流程
排查 `szaipa2022/Controllers/StaffController.cs` 发现旧后台对同一张 `Works` 表存在**两套互不知情的管理入口**：
- `ArtWorksAdd/ArtWorksEdit/ArtWorksList`：图片目录 `/Content/ArtImg/Artist/works-narrow/`，只写 Title/Content/Tags/Path。
- `WorkAdd/WorkEdit/Works`：图片目录 `/Content/ArtImg/Artist/Works/`（注意大小写不同，是不同目录！），额外通过 `imginf()` 探测图片尺寸写 Width/Height/transverse/long，并维护 `Artist.WorkCount`/`WorksTag` 关联表。

核对公开页 `Views/Home/NewArt.cshtml`（`@w.Path` 渲染路径用的是 `/Content/ArtImg/Artist/works-narrow/`）和读侧投影 `WorkSummaryModel`（只含 Id/ArtistId/Title/Path/Width/Height/Tags/Content，但 Width/Height 实际未在任何视图里被读取/渲染），确认：
- **公开页实际渲染只依赖** `works-narrow` 目录下的图 + Title/Content/Tags。`WorkAdd` 流程写的另一个目录(`Works`)和算出来的 Width/Height/transverse/long 是从未被读取的死数据。
- 新模块统一为**一套**写入路径，目录用 `works-narrow`（匹配公开页实际渲染），不照搬 Width/Height/transverse/long 探测逻辑和 `Artist.WorkCount` 冗余计数器（避免引入又一个容易漂移的缓存字段——真要显示数量应该现查 `COUNT(*)`，不维护影子计数器）。
- `docs/PROJECT_MAP.md`/`docs/HANDOFF.md` 里原列的「Works、ArtWorks」两个待建项，因此合并为一个「Works」模块，HANDOFF 已更新。

## 测试 / 验证
- `ArtistScopedAdminRepositoryTests` 新增 `Works_create_update_delete_and_preserves_image_on_empty`（创建持久化、EditRecord、图片空值不覆盖、列表 join 艺术家名、删除）。
- `~/.dotnet/dotnet build` 0/0；`test` 58 绿（57→58）。`npm run build` 通过。
- 路由冒烟：未登录 `/Admin/Works`、`/Admin/Works/Create` → 302 跳登录；`/healthz` 200。端到端创建/编辑/删除点击验证留给用户本地可写库环境。

## Phase 3 状态
Fav / Auction / Exhibition / Artist / Works 全部完成，**Phase 3 收尾结束**。下一步进入 Phase 4（Company、Tongou CRUD）。
