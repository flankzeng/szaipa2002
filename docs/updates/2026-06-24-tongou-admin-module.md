# 2026-06-24 后台 Tongou（同构）Atrist/Works CRUD 模块，Phase 4 完成

## 新增模块：同构艺术家 + 同构作品管理
Tongou(同构) 是物理隔离的另一个数据库（同一台 SQL Server 实例，不同 DB；只读侧已有独立的 `TongouLegacyReadContext`）。这次先补上写侧基础设施，再建两个 CRUD 模块：

### 新写侧基础设施
- `Configuration/TongouAdminWriteOptions`：照搬 `AdminWriteOptions` 的门控模式（`EnableWrites` + 独立连接字符串，`IsConfigured` 才构造上下文）。
- `Contexts/TongouAdmin/TongouAdminContext`：可写 EF Core 上下文，`DbSet<TongouAtrist>`/`DbSet<TongouWorks>`（小写 `id` 主键，复用读侧实体类）。
- DI 注册（`SzaipaDataServiceCollectionExtensions.cs`）：`ConnectionStrings:TongouAdmin` / 环境变量 `TONGOU_ADMIN_CONNECTION` 解析，未配置时构造即报错（同 SzaipaAdminContext 模式）。`appsettings.Local.example.json` 加了对应示例段。

### 跨库审计：两次 SaveChanges
TongouAtrist/TongouWorks 表本身**没有** `EditRecord` 列（legacy 这两张表从未有审计字段），员工操作日志（Diary/Staff）只存在于 Szaipa 库。因此两个新仓储同时持有 `TongouAdminContext`（业务数据）和 `SzaipaAdminContext`（审计），各自 `SaveChangesAsync` 一次——两个物理数据库不能共享一个事务，这是架构上的硬约束，不是疏漏。

### TongouAtrist（同构艺术家）
- `ITongouAtristAdminRepository`/`TongouAtristAdminRepository`：分页/取/增/改/删 + `NameExistsAsync`（照搬 legacy `AtristAdd` 的重名校验「已存在此艺术家」——这是有意保留的业务规则，不是要修的 bug）。
- 字段：Name(必填)/Title/AboutText/HeardPath(头像)。`WorksCount`/`Aboutid`/`HotCount` 不在表单里——legacy `AtristEdit` 本身也从不改这三个字段；`WorksCount` 是个影子计数器（legacy 在 `WorkAdd` 里 `atr.WorksCount++`），读侧投影里也完全没渲染过，照之前 Works/Company 模块的同一决定，不维护这个易漂移的缓存字段。
- 控制器/视图：`Areas/Admin/Controllers/TongouAtristController.cs` + `Views/TongouAtrist/{Index,_Form,Create,Edit}`。

### TongouWorks（同构作品）
- `ITongouWorksAdminRepository`/`TongouWorksAdminRepository`：分页（join 艺术家名）/取/增/改/删。
- 字段：Atristid(下拉选艺术家)/Title(必填)/Size/Type/CreationDate/ImgPath(作品图)。**改进点**：legacy `WorkAdd` 是按艺术家姓名字符串匹配（`AtristidName` 文本框，找不到就报错），新模块改成下拉选 Id（与 Szaipa 侧 Fav/Auction/Works 模块一致的 UX，不再依赖脆弱的姓名字符串匹配）；`AtristidName` 仍然写入，保持读侧投影 `ArtistName = work.AtristidName` 的兼容。`VisityCount`/`HotCount` 不在表单里，同上理由。
- 控制器/视图：`Areas/Admin/Controllers/TongouWorksController.cs` + `Views/TongouWorks/{Index,_Form,Create,Edit}`。

### 重要发现：Tongou 的图片字段存的是完整 URL，不是文件名
核对 `Views/ProjectTongou/Atrist.cshtml`（`url('@Model.HeardPath')`）和 `Work.cshtml`（`src="@Model.ImgPath"`）——两者都**直接**把字段值当 URL 用，没有像 Szaipa 侧 `Path` 一样在视图里拼 `/Content/.../` 前缀。说明 Tongou 库的 `HeardPath`/`ImgPath` 历史上存的就是完整路径。

为此给共享的 `wwwroot/admin/upload-field.js` 加了一个**向后兼容的可选开关** `data-store="url"`：不加时行为不变（继续存文件名，给 News/Artist/Works/Fav/Auction/Exhibition/Company 用），加了就把上传接口返回的完整 `/Content/...` URL 存进隐藏字段——Tongou 的两个表单用这个开关。

## 测试 / 验证
- 新增 `TongouAdminRepositoryTests`（5 个），每个测试同时起两个独立 SQLite 内存库（模拟两个物理隔离的真实库）：重名校验、审计写入 Diary+Staff、编辑/删除、艺术家名解析与列表 join、编辑保留艺术家与图片(未传不覆盖)。
- `~/.dotnet/dotnet build` 0/0；`test` 64→69 绿。`npm run build` 通过。
- 路由冒烟：未登录 `/Admin/TongouAtrist`、`/Admin/TongouAtrist/Create`、`/Admin/TongouWorks`、`/Admin/TongouWorks/Create` → 全部 302 跳登录（验证了 `[Authorize]` 在控制器实例化、也就是在 `TongouAdminContext` 被请求之前就拦截，所以本地没配置 Tongou 可写库也能正常跑通这层冒烟）；`/healthz` 200。端到端创建/编辑/删除点击验证留给用户配 `ConnectionStrings:TongouAdmin` 本地可写副本后验证。

## Phase 4 状态
Company + Tongou(Atrist/Works) 全部完成，**Phase 4 收尾结束**。下一步 Phase 5（访问分析仪表盘）建议升级到 Opus（数据可视化设计/图表库选型）。
