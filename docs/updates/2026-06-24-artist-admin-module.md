# 2026-06-24 后台 Artist（会员）CRUD 模块

## 新增模块：会员管理（Phase 3 收尾第一项）
- Artist 是主表（非 `IArtistScopedRecord`），Fav/Works/Auction/Exhibition/ArtNews 都通过 `ArtistId` 挂在其下；照 News 范本（独立仓储，非泛型艺术家子类）实现：
  - `IArtistAdminRepository` / `ArtistAdminRepository`（`src/Szaipa.Data/Services/Admin/`）：分页、按 Id 取、增/改/删，操作均走 `IOperationRecorder` 写 Diary+Staff 同事务。
  - 控制器 `Areas/Admin/Controllers/ArtistController.cs`：Index/Create/Edit/Delete，`[Authorize(Policy=StaffPolicy)]`。
  - 视图 `Areas/Admin/Views/Artist/{Index,_Form,Create,Edit}.cshtml`；导航 `_AdminLayout.cshtml` 加「会员管理」入口。
  - DI 注册：`SzaipaDataServiceCollectionExtensions.cs`。

## 表单字段取舍（依据公开页 `NewArt.cshtml` 实际渲染 + 数据完整性）
- 编辑字段：中文名/英文名（必填）、性别、国家、城市、级别(Title)、社会职务(Position)、主题色 1/2（`type=color`）、头像(Path)、背景图 1/2(Path1/Path2)、简介(Introduction，纯文本——公开页未用 `Html.Raw`，故不接 TipTap)、荣誉(Honor)、艺术家年表(DeedsThings，**TipTap 富文本**——公开页用 `@Html.Raw(artist.DeedsThings)` 渲染)。
- 头像上传目录 `ArtImg/Artist`，背景图目录 `ArtImg/Artist/Banner`（与 `NewArt.cshtml` 的 `--artist-banner` CSS 变量路径一致）。
- 系统字段（不进表单，仓储自动管理）：`WorkCount`/`VisitCount` 初始 0，`AddDate` 仅创建时写入，`EndDate` 每次编辑刷新（照旧后台 `ArtEdit` 行为），`EditRecord` 追加审计文本。
- 有意跳过：`UserContent`/`Deeds`/`DeedsYears`/`Activity`/`FlieInf`/`Sex` 的旧 `FormCollection` 残留字段——当前读侧投影（`SzaipaHomeProjections.ArtistDetail/ArtistSummary`）完全不引用，属遗留死字段，不照搬扩大表单复杂度。

## 测试 / 验证
- 新增 `ArtistAdminRepositoryTests`（7 个）：创建持久化、操作日志写 Diary+Staff、编辑保留未传图片(Path/Path1/Path2)、编辑追加 EditRecord、删除不存在返回 false、分页倒序。
- `~/.dotnet/dotnet build` 0/0；`test` 57 绿（50 → 57）。`npm run build` 前端构建通过。
- 路由冒烟：未登录 `/Admin/Artist`、`/Admin/Artist/Create` → 302 跳 `/Admin/Account/Login`；`/healthz` 200。端到端创建/编辑/删除点击验证留给用户本地可写库环境。

## 剩余（Phase 3 收尾，下一步）
- Works、ArtWorks（依赖本次 Artist 主表落地，结构上是 `IArtistScopedRecord` 子表，可照 Fav/Auction 范本 + 图片/尺寸/标签字段扩展）。
