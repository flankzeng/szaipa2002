# szaipa2002 — 项目地图（PROJECT MAP）

> 给后续（含低级模型）快速导航用：照这份地图直接定位，**不必每次全项目扫描**。
> 配套文档：[HANDOFF.md](HANDOFF.md)（交接 + 剩余工作 + 模型建议）。

## 这是什么
深圳市艺术产业促进会官网，从 **旧 ASP.NET MVC5（.NET Framework）** 迁到 **ASP.NET Core net10.0**。
- 旧站：`szaipa2022/`（MVC5，Layui）—— 只读参考，最终弃用；**这台 Mac 上跑不起来**（mono 崩），改它只能改源码、无法运行验证。
- 新站：`src/`（ASP.NET Core）—— 实际开发目标。

## 解决方案结构
- `src/Szaipa.Data` —— 数据层：EF Core 上下文、实体、读模型、仓储、admin 写服务。
- `src/Szaipa.Web` —— ASP.NET Core MVC：公开站（Views/Home）+ 后台（Areas/Staff）。
- `tests/Szaipa.Data.Tests` —— xUnit + SQLite 内存库（96 测试）。
- `tests/Szaipa.Web.Tests` —— Web 层纯策略测试（当前 14 项；全解决方案合计 110）。
- `Szaipa.Modernization.slnx` —— 解决方案文件。

## 命令（重要：用 ~/.dotnet/dotnet，SDK 10.0.301；PATH 的 dotnet 是旧版 6/7）
```
~/.dotnet/dotnet build Szaipa.Modernization.slnx      # 须 0 警告 0 错误
~/.dotnet/dotnet test  Szaipa.Modernization.slnx      # 须全绿（当前 110）
cd src/Szaipa.Web && npm run build                    # Tailwind(admin.css) + esbuild(editor.js)
~/.dotnet/dotnet run --project src/Szaipa.Web/Szaipa.Web.csproj --urls http://127.0.0.1:5057
# 冒烟：/healthz 200；未登录 /Staff/* → 302 跳 /Staff/Account/Login
```

## 双数据库架构（关键）
- **读侧**：`SzaipaLegacyReadContext`（`Contexts/Szaipa/`）—— NoTracking、SaveChanges 硬禁；连**只读生产库**（`ConnectionStrings:Szaipa` / 环境变量 `SZAIPA_READONLY_CONNECTION`）。读仓储一律 `.Select(投影)`（`Services/Home/SzaipaHomeProjections.cs`），所以**给实体加字段对读侧安全**（只 SELECT 投影引用的列）。
- **写侧（后台）**：`SzaipaAdminContext`（`Contexts/SzaipaAdmin/`）—— 跟踪、可写；门控：`AdminWrite:EnableWrites=true` + `ConnectionStrings:SzaipaAdmin`，指向**本地可写副本**。生产库永不写、永不用生产凭据（见记忆 db-safety-constraints）。
- **dev 现状**：读=生产只读，写=本地副本，**是两个库**；admin 建的数据在 dev 不被公开读侧看到（生产同库才打通）。端到端点击验证需用户本地配可写库 + 可写 `LegacyAssets:ContentRoot`。
- Tongou 有独立只读上下文 `Contexts/Tongou/TongouLegacyReadContext` 和独立写上下文 `Contexts/TongouAdmin/TongouAdminContext`（门控：`TongouAdminWrite:EnableWrites=true` + `ConnectionStrings:TongouAdmin`，同样指向本地可写副本，与 Szaipa 是两个物理隔离的数据库）。Tongou 表无 `EditRecord` 列，操作审计仍写 Szaipa 库的 Diary/Staff（两个 SaveChanges，不能跨库共事务）。
- 配置装配：`src/Szaipa.Data/DependencyInjection/SzaipaDataServiceCollectionExtensions.cs`（所有上下文/仓储在此注册）；`src/Szaipa.Web/Program.cs`（认证、DI、area 路由、静态文件、/Content 映射）。

## 后台（Areas/Staff）—— 加新 CRUD 模块照这个抄
认证：cookie（`Program.cs` 配 `AddCookie`，登录 `/Staff/Account/Login`），策略 `AdminAuthorization.StaffPolicy`，控制器加 `[Authorize(Policy=...)]`。密码 MD5 兼容老账号（`Services/Admin/StaffPasswordHasher`）。
- **命名约定（2026-06-24）**：URL/区是 `Staff`（`Areas/Staff`、`/Staff/...`），但**内部命名仍是 Admin**——数据/Web 服务层 `Services/Admin/`、`AdminWriteOptions`/`AdminWrite` 配置段、`AdminAuthorization` 策略类、静态资源 `wwwroot/admin/` 都不改（非 URL）。⚠️ 区改名后 `Szaipa.Web.Areas.Staff` 命名空间与 `Staff` 实体同名，区内引用 `Staff` 实体类型须全限定 `Szaipa.Data.Contexts.Szaipa.Staff`（见 `AccountController`）。
- **本地登录**：`AdminWrite:UseReadOnlyConnectionForDebug=true` 时(1) admin 上下文复用只读 `szaipa_ro` 连接、(2) 启用固定调试账号（默认 `debug`/`debug`，可用 `DebugUserName`/`DebugPassword` 改）——不碰 Staff 表直接签发为「调试管理员（只读）」，即使生产 Staff 表连不上也能登录看后台。仅 gitignore 的 Local.json 启用，生产永不开。详见 `docs/updates/2026-06-24-staff-area-rename-branding-devlogin.md`。

**一个 admin 模块 = 4 处**（以 News 为范本）：
1. 写仓储：`src/Szaipa.Data/Services/Admin/NewsAdminRepository.cs`（+ `I…`）—— CRUD + 用 `IOperationRecorder` 记操作日志（写 Diary + Staff，同事务）。`AdminActor`(StaffId,StaffName)、`PagedResult<T>`。
2. 控制器：`src/Szaipa.Web/Areas/Staff/Controllers/NewsController.cs` —— Index/Create/Edit/Delete，`CurrentActor()` 从 claims 取。
3. 视图：`Areas/Staff/Views/News/{Index,_Form,Create,Edit}.cshtml`。
4. 导航：`Areas/Staff/Views/Shared/_StaffLayout.cshtml`（「内容管理」下拉里加链接 + `contentControllers` 数组）。
- DI 注册：`SzaipaDataServiceCollectionExtensions.cs`。

**艺术家子模块**（Fav/Auction/Exhibition/Works）用泛型基类 `Services/Admin/ArtistScopedAdminRepository<T>`（实体实现 `IArtistScopedRecord`）——子类只给 DbSet/名词/字段拷贝。列表共用 `Views/Shared/_ArtistScopedList.cshtml`。

**已建模块**：Account(登录/改密码) / Dashboard(访问分析仪表盘) / News / ArtNews / Artist(会员) / Works(作品) / Company(会员企业) / Publication(展览) / Fav / Auction / Exhibition / Upload / TongouAtrist(同构艺术家) / TongouWorks(同构作品)。Artist/Company 是主表（非 `IArtistScopedRecord`），照 News 范本（独立仓储）。Tongou 两个模块走独立的 `TongouAdminContext`（与 Szaipa 物理隔离的另一个数据库，门控同 `AdminWrite`，详见 `docs/updates/2026-06-24-tongou-admin-module.md`）。
**仪表盘**：`DashboardController` + `IDashboardAnalyticsRepository`（只读聚合 `SzaipaAdminContext`：每日访问/内容访问/省市旭日/操作记录/KPI）+ ECharts（npm + esbuild `wwwroot/admin/dashboard.js`，源 `wwwroot/admin/src/dashboard.js`）。仓储懒解析 + `AdminWriteOptions.IsConfigured` 守卫，未配库时降级不 500。详见 `docs/updates/2026-06-24-dashboard-analytics-phase5.md`。
**待建（2026-07-09 全部完成）**：展览参展作品目录（`ExhibitionWork`）/ slug 页退役（14 个迁成数据驱动，3 个特大页+1 个翻页书迷你站保留）/ Phase 6 加固审查（未发现遗漏，补测试 82→94）。见 HANDOFF「剩余工作」、`docs/updates/2026-07-09-*`。
**注（2026-06-24）**：旧后台「ArtWorks」≠ 独立实体，只是 legacy `StaffController` 里管理同一张 `Works` 表的另一套重复 action（`ArtWorksAdd/Edit`，图片目录 `works-narrow`），与 `WorkAdd/WorkEdit`（图片目录 `Works`，额外算 Width/Height/transverse/long）功能重叠、互相打架。新 Works 模块只实现公开页 `NewArt.cshtml` 实际渲染引用的字段/路径（`works-narrow` 目录 + Title/Content/Tags），未照搬已死的 Width/Height/transverse/long 计算逻辑——`HANDOFF.md` 旧待建列表里的「ArtWorks」已并入 Works，不再是独立模块。

## 富文本 / 图片 / 画廊（前端组件）
- TipTap 编辑器：源 `wwwroot/admin/src/editor.js` → esbuild 打包 `wwwroot/admin/editor.js`；复用 partial `Areas/Staff/Views/Shared/_RichTextEditor.cshtml`（`RichTextEditorModel`）。内容存 HTML。
- 通用图片上传：`Areas/Staff/Controllers/UploadController` + `Services/Admin/AdminAssetStorage`（唯一 GUID 命名，写 `{ContentRoot}/{subfolder}`，规避旧版「子串匹配删图」bug）。封面用插件 `wwwroot/admin/upload-field.js`。
- 展览多图画廊：`wwwroot/admin/gallery-manager.js`（多图上传/↑↓排序/删除）+ `Services/Admin/ExhibitionGalleryFolder`（自动编号 10001+、两段式防碰撞重排、保留封面 10000，**有单测**）+ Web 包装 `ExhibitionGalleryStorage`。
- Tailwind：源 `wwwroot/admin/admin.input.css` → `admin.css`；设计令牌在 `tailwind.config.js`（brand #bf272d / canvas #f7f7f7 / muted #939393 / Noto 字体）。

## 公开站（Views/Home）
- 布局：`Views/Shared/_newLayout.cshtml`（多数页）、`_Artist.cshtml`（NewArt）。两者都加了可选 `@RenderSectionAsync("Styles"/"Scripts")`——页面级 CSS/JS 用 `@section` 挂。
- 已把巨量内联 CSS/JS **外提**到 `wwwroot/css/*.css`、`wwwroot/js/*.js`（newindex/newnews/newnewsread/newart + publication-gallery + exhibition-important）。NewArt 用「CSS 变量 + JS 桥接」保留动态值（`@artist.Color1/Path1`）。
- 前台清理 Phase 1：公共库改按页加载、新闻详情去 Vue/Element Plus、图片懒加载、响应压缩/缓存、同字体子集、手机 viewport/navbar 修复。字体子集通过 `scripts/build-font-subsets.py` 重建；Noto 批处理还需 `scripts/font-db-codepoints.txt`，全量源从远端 `legacy/archive-before-frontend-prune-20260710` 临时 worktree 读取。详情见 `docs/updates/2026-07-10-frontend-cleanup-phase1.md`、`2026-07-13-noto-font-localization.md`。
- ProjectTongou 公开浏览已退役：`Controllers/ProjectTongouController.cs` 仅保留无数据库访问的 410 兼容端点，视图为 `Views/ProjectTongou/Gone.cshtml`；Tongou 后台/数据层不受影响。
- Legacy 资源不能按目录直接删除；第一轮 A/B/C/D 分级和两套 Content 差异见 `docs/updates/2026-07-10-legacy-resource-inventory.md`。
- zengfeng 已退役：现代/旧 MVC 路由返回 410，约 140MB 专属资源从当前分支移除；基线保存在远端 `legacy/archive-before-frontend-prune-20260710`。
- 正式前台列表：`Home/NewAbout.cshtml`、`Home/NewVip.cshtml`、`Home/PublicationList.cshtml`；对应缓存 CSS 为 `newabout.css`、`newvip.css`、`publication-list.css`。DB 未启用时会员/展会仍使用各自 Skeleton。
- 特殊展览页共享资源：`wwwroot/css/publication-special.css`；chunyu3/tonggou2 使用 `wwwroot/js/publication-special.js`，tonggou2024 保留 `publication-tonggou2024.js` 的独有滚动行为。详情见 `docs/updates/2026-07-11-frontend-cleanup-phase3.md`。
- Legacy Content 审计：`scripts/audit-legacy-content.py`；动态保护根、可选 DB 路径/HTTP 日志输入和最新结果见 `docs/updates/2026-07-11-legacy-content-audit.md`、`2026-07-11-frontend-cleanup-phase4.md`。
- 生产 IIS 日志审计：`scripts/audit-iis-content.ps1` 支持共享读取正在写入的 W3C 日志，并把访问量与物理文件盘点合并输出。2026-07-13 的 90 天结果确认 `_preview`/`TempFile`/`js`/`Filme` 正在使用，`Award` 与 `layui` 仅为审计候选。服务器只供只读参考，发布版不隔离、不删除、不部署、不改 IIS；任何必须的服务器改动先停下征得用户确认。见 `docs/updates/2026-07-13-production-iis-content-audit.md`。
- 静态缓存：`Infrastructure/StaticAssetCachePolicy.cs` 统一选择响应头；自有带 `?v=` 的资源为 1 年 `immutable`，外接 `/Content` 图片/字体 30 天、CSS/JS 7 天、未知类型 1 天，Development 始终 `no-cache`。规则由 `tests/Szaipa.Web.Tests` 覆盖；见 `docs/updates/2026-07-13-static-asset-cache-policy.md`。
- 首页轮播的首张实际可见封面由 `NewIndex.cshtml` 动态选择为唯一 eager/high 图片；若数据库轮播为空则落到首张硬编码展览，其余封面继续 lazy。见 `docs/updates/2026-07-13-home-carousel-lcp-priority.md`。
- 当前分支不再携带旧 MVC5 的两套 `packages` 及已归档静态目录；需要完整旧站环境时使用远端 `legacy/archive-before-frontend-prune-20260710`，现代解决方案不受影响。
- 公共页不再请求有字体、Google Fonts 或 loli 字体域；Alibaba 普惠体与 Noto Sans/Serif SC 均使用原字形的本地子集。Noto 使用唯一 `Szaipa Noto ...` family 隔离 legacy `Site.css`，当前源码/数据库字符进 core，GB2312 余字按 `unicode-range` 分片按需加载。
- 旧发布版只读参考位于 `~/Project/GitClone/web24.05`，其 `Content` 约 1.2GB；在生产切换到现代站且取得 IIS 日志/数据库路径前，不按现代源码候选直接删除旧发布资源。
- 展览页：数据驱动 `Views/Home/Publication.cshtml` 按 `Publication.Type` 分支 → 共享 `Views/Shared/_ExhibitionGallery.cshtml`（普通）或 `_ExhibitionImportant.cshtml`（重要：banner+序+画廊，皮肤 `wwwroot/css/exhibition-important.css`）。旧 `Views/Publication/*.cshtml`（slug 硬编码页）待迁数据后退役。
- 路由：`Controllers/HomeController.cs`（newIndex/newnews/newnewsread/newvip/newArt/Publication/PublicationList）。

## 测试范式
- SQLite 内存库 + `EnsureCreated()`（建全 schema，含新列）。读上下文测试 `TestDb.cs` 用原始 SQL 播种（因读上下文禁 SaveChanges）。
- admin/写测试直接用 `SzaipaAdminContext`（可写）：见 `NewsAdminRepositoryTests` / `ArtistScopedAdminRepositoryTests` / `ExhibitionGalleryFolderTests` / `StaffPasswordHasherTests`。
- 加实体 NOT NULL 字段时记得在两上下文 `OnModelCreating` 给默认值（如 `Publication.Type` `HasDefaultValue(0)`），否则原始 SQL 播种会撞 NOT NULL。

## SQL 迁移
- `docs/sql/` 放 ALTER 脚本（如 `2026-06-exhibition-template-columns.sql` 给 Publication 加 Type/Preface/Signature）。用户在本地副本（+ 必要时读库）上跑。

## 约定 / 注意
- **最终总结用中文**（记忆 summaries-in-chinese）。设计师用户，CSS 大改写中文注释、保持干净（css-design-conventions）。
- **边做边验证**：每个功能真的跑一下；遇到 legacy bug **顺手修**而不是照搬（已修：旧 NewsAdd 丢正文 + str2t1 误删 newsImg；旧 ArtNewsEdit 字段赋值给自己的 no-op；NewIndex 王玉波头像错链）。见记忆 verify-and-fix-legacy-bugs。
- 记忆目录有：parity-baseline / db-safety-constraints / migration-state / update-docs-workflow / css-design-conventions / summaries-in-chinese / staff-backend-migration / verify-and-fix-legacy-bugs / exhibition-pages-design / project-map。
