# 2026-06-24 Phase 5：访问分析仪表盘 + 修改密码

## 概述
完成后台综合首页（`/Admin` / `/Admin/Dashboard`）的访问数据可视化，迁移 legacy `StaffController.Index` 的 4 个 ECharts + 操作记录 feed，并补上账户安全的「修改密码」。

## 数据层（Szaipa.Data，只读聚合）
- `IDashboardAnalyticsRepository` / `DashboardAnalyticsRepository`（`Services/Admin/`）+ DTO `DashboardAnalyticsModels.cs`。
- **数据源决策**：走 `SzaipaAdminContext` + `AsNoTracking()`。这些分析表（AccessData / Diary / Works·Artist·Company·News 的访问计数）都在 Szaipa 库，且 admin 上下文在生产即指向生产库；分析是纯只读聚合，复用 admin 上下文避免跨上下文复杂度，且与所有 admin 模块同源。
- 五个聚合，对应 legacy 端点：
  - `GetDailyVisitsAsync(days)` ← legacy `DayVisityCount`：近 N 天每日访问（`Diary.VistiTotal`），**缺失日补 0**，oldest→newest。
  - `GetContentAccessAsync()` ← legacy `DataCount`：Works/Artist/Company 的 VisitCount 之和 + News.ReadCount 之和（`(long?)…  ?? 0`，空表返回 0）。
  - `GetGeoDistributionAsync(since)` ← legacy `visitcityM`/`visitcityY`：`AccessData` 按 (Porvince, Ctiy) **在 SQL 里 GroupBy 聚合**（不再像 legacy 那样把全表拉进内存逐行 foreach），再在内存组装省→市树；过滤空/`None` 市（保留 legacy 语义）。
  - `GetRecentOperationsAsync(days)` ← legacy `DieryTodayRecord`/`DayOr`：`Diary.OperationRecord` 按 `/` 拆分，newest day first。
  - `GetKpiAsync(recentDays)`：今日访问 / 本月访问 / 内容总访问 / 近 7 天操作数。

## Web 层
- `DashboardController` 重写：KPI + 操作记录服务端渲染（`DashboardViewModel`），4 个图表走 AJAX JSON 端点（`DailyVisits` / `ContentAccess` / `Geo?range=month|year`）。
- **健壮性**：仓储经 `HttpContext.RequestServices` **懒解析**（不走构造注入），且先查 `AdminWriteOptions.IsConfigured`——未配置可写库（dev 常态）时页面渲染「访问统计暂不可用」提示而非 500；配置了但连不上库时 catch `DbException` 同样降级。这样首页在任何配置下都能打开。
- 路由：`[Route("Admin/Dashboard")]` + `[HttpGet("")]`/`[HttpGet("Index")]`/`[HttpGet("/Admin")]`，三个入口都命中（替代原本的约定路由）。

## 前端图表（ECharts）
- 选型沿用 legacy 的 **Apache ECharts**（原生支持折线/饼/旭日，契合既有视觉），但改为 **npm 依赖 + esbuild 按需打包**（`echarts/core` + LineChart/PieChart/SunburstChart + 必要组件），而非 legacy 的 CDN 全量引入。产物 `wwwroot/admin/dashboard.js`（~552KB，tree-shaken），随 `npm run build`（新增 `build:dashboard`）生成。自包含、离线可用、可被 `asp-append-version` 缓存。
- 源 `wwwroot/admin/src/dashboard.js`：折线（近30天趋势）+ 环形饼（内容访问构成）+ 双旭日（本月/本年省市）；品牌红 #bf272d 主色调色板；空数据显示「暂无数据」；窗口 resize 自适应。
- 视图 `Dashboard/Index.cshtml`：KPI 卡 ×4 + 折线 + 饼 + 双旭日 + 操作记录时间线。

## 修改密码（账户安全）
- `AccountController` 加 `PasswordChange` GET/POST（复用已有的懒解析 + MD5 兼容）：校验原密码（`StaffPasswordHasher.Verify`）→ 写新密码（仍存 lowercase-MD5，保持与老账号一致）→ 经 `IOperationRecorder` 记操作日志 → 保存。`PasswordChangeViewModel` 带原/新/确认三段校验（新密码 ≥6 位、两次一致）。
- 视图 `Account/PasswordChange.cshtml`；`_AdminLayout` 顶栏在「退出」旁加「修改密码」入口。
- 修复 legacy `PasswordChange` 的明显 bug：旧代码 `string repassword = password = Request.Form["RePassword"]` 把变量赋值串错、且即使两次不一致也继续往下改密码（`AddModelError` 后没 return）——新实现用标准 ModelState 校验 + `[Compare]`，不一致直接退回表单。

## 测试 / 验证
- 新增 `DashboardAnalyticsRepositoryTests`（6 个）：每日补零/排序、内容求和、空表归零、省市树（含 None/空/越界过滤 + 降序）、操作记录拆分排序、KPI 聚合。
- `~/.dotnet/dotnet build` 0/0；`test` 69→75 绿。`npm run build` 通过（css + editor + dashboard）。
- 路由冒烟：`/Admin`、`/Admin/Dashboard`、`/Admin/Dashboard/{Index,DailyVisits,ContentAccess,Geo}`、`/Admin/Account/PasswordChange`（GET+POST）未登录全部 302；`/admin/dashboard.js` 200。首页/登录页正常。
- 端到端（真实图表渲染 + 改密码落库）需用户本地配 `AdminWrite` 可写库并登录后点测（无可写库时仪表盘走「暂不可用」降级路径，已验证不 500）。

## 备注 / 后续
- 一个**独立的全量操作记录页**（分页查看历史，而非仪表盘的近 7 天 feed）属可选增强，未做。
- 端到端真实数据校验依赖用户本地可写库 + AccessData 有数据。
