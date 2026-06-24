# 2026-06-24 后台 Company（会员企业）CRUD 模块

## 新增模块：会员企业管理（Phase 4 第一项）
Company 是独立主表（无 ArtistId），目前完全不被任何公开页读取（搜索 `src/Szaipa.Data/Services/Home/`、`Views/Home/` 均无引用），照 News 范本走独立仓储：

- `ICompanyAdminRepository`/`CompanyAdminRepository`（`src/Szaipa.Data/Services/Admin/`）：分页/取/增/改/删，操作走 `IOperationRecorder`。
- 控制器 `Areas/Admin/Controllers/CompanyController.cs` + `CompanyFormViewModel` + Index/_Form/Create/Edit 视图；导航加「会员企业」入口；DI 注册。

## 字段取舍
- 编辑字段：企业中文名(必填)/英文名/法人(CEO)/经营范围(Business)/地址(Address)/Logo(ImgPath，上传目录 `ArtImg/Company`，对应旧后台 `/Content/ArtImg/Company`)。
- 系统字段：`VisitCount` 创建时置 0；`FirstDate`/`LastDate` 仅创建时写入一次——**照旧后台行为**：legacy `CompanyEdit` 从不更新这两个日期字段（与 Artist 模块「EndDate 每次编辑刷新」不同，Company 没有这个行为，没有照搬过去）。
- 有意跳过 `FilePath`（旧后台的企业附件上传，落盘到 `/Content/File/`）：当前 `IAdminAssetStorage` 只支持图片（白名单 jpg/png/gif/bmp/webp），没有通用文件上传管线，且该字段同样不被任何读侧引用——不为这一个冷字段新建一套文件上传基础设施。

## 测试 / 验证
- 新增 `CompanyAdminRepositoryTests`（6 个）：创建持久化/操作日志/编辑保留 Logo(未传不覆盖)/编辑不存在返回 false/删除/分页倒序。
- `~/.dotnet/dotnet build` 0/0；`test` 58→64 绿。`npm run build` 通过。
- 路由冒烟：未登录 `/Admin/Company`、`/Admin/Company/Create` → 302 跳登录；`/healthz` 200。

## 下一步（Phase 4 继续）
Tongou(Atrist/Works) CRUD —— 与 Szaipa 主库物理隔离的另一个数据库，目前只有只读上下文 `TongouLegacyReadContext`，需要先建一个对应的可写 `TongouAdminContext`（照搬 `SzaipaAdminContext` 的门控连接字符串模式），再照 Fav/Auction 范本建 Atrist/Works CRUD。
