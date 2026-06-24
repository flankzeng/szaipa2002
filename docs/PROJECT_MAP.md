# szaipa2002 — 项目地图（PROJECT MAP）

> 给后续（含低级模型）快速导航用：照这份地图直接定位，**不必每次全项目扫描**。
> 配套文档：[HANDOFF.md](HANDOFF.md)（交接 + 剩余工作 + 模型建议）。

## 这是什么
深圳市艺术产业促进会官网，从 **旧 ASP.NET MVC5（.NET Framework）** 迁到 **ASP.NET Core net10.0**。
- 旧站：`szaipa2022/`（MVC5，Layui）—— 只读参考，最终弃用；**这台 Mac 上跑不起来**（mono 崩），改它只能改源码、无法运行验证。
- 新站：`src/`（ASP.NET Core）—— 实际开发目标。

## 解决方案结构
- `src/Szaipa.Data` —— 数据层：EF Core 上下文、实体、读模型、仓储、admin 写服务。
- `src/Szaipa.Web` —— ASP.NET Core MVC：公开站（Views/Home）+ 后台（Areas/Admin）。
- `tests/Szaipa.Data.Tests` —— xUnit + SQLite 内存库（50 测试）。
- `Szaipa.Modernization.slnx` —— 解决方案文件。

## 命令（重要：用 ~/.dotnet/dotnet，SDK 10.0.301；PATH 的 dotnet 是旧版 6/7）
```
~/.dotnet/dotnet build Szaipa.Modernization.slnx      # 须 0 警告 0 错误
~/.dotnet/dotnet test  Szaipa.Modernization.slnx      # 须全绿（当前 50）
cd src/Szaipa.Web && npm run build                    # Tailwind(admin.css) + esbuild(editor.js)
~/.dotnet/dotnet run --project src/Szaipa.Web/Szaipa.Web.csproj --urls http://127.0.0.1:5057
# 冒烟：/healthz 200；未登录 /Admin/* → 302 跳 /Admin/Account/Login
```

## 双数据库架构（关键）
- **读侧**：`SzaipaLegacyReadContext`（`Contexts/Szaipa/`）—— NoTracking、SaveChanges 硬禁；连**只读生产库**（`ConnectionStrings:Szaipa` / 环境变量 `SZAIPA_READONLY_CONNECTION`）。读仓储一律 `.Select(投影)`（`Services/Home/SzaipaHomeProjections.cs`），所以**给实体加字段对读侧安全**（只 SELECT 投影引用的列）。
- **写侧（后台）**：`SzaipaAdminContext`（`Contexts/SzaipaAdmin/`）—— 跟踪、可写；门控：`AdminWrite:EnableWrites=true` + `ConnectionStrings:SzaipaAdmin`，指向**本地可写副本**。生产库永不写、永不用生产凭据（见记忆 db-safety-constraints）。
- **dev 现状**：读=生产只读，写=本地副本，**是两个库**；admin 建的数据在 dev 不被公开读侧看到（生产同库才打通）。端到端点击验证需用户本地配可写库 + 可写 `LegacyAssets:ContentRoot`。
- Tongou 有独立只读上下文 `Contexts/Tongou/TongouLegacyReadContext`。
- 配置装配：`src/Szaipa.Data/DependencyInjection/SzaipaDataServiceCollectionExtensions.cs`（所有上下文/仓储在此注册）；`src/Szaipa.Web/Program.cs`（认证、DI、area 路由、静态文件、/Content 映射）。

## 后台（Areas/Admin）—— 加新 CRUD 模块照这个抄
认证：cookie（`Program.cs` 配 `AddCookie`，登录 `/Admin/Account/Login`），策略 `AdminAuthorization.StaffPolicy`，控制器加 `[Authorize(Policy=...)]`。密码 MD5 兼容老账号（`Services/Admin/StaffPasswordHasher`）。

**一个 admin 模块 = 4 处**（以 News 为范本）：
1. 写仓储：`src/Szaipa.Data/Services/Admin/NewsAdminRepository.cs`（+ `I…`）—— CRUD + 用 `IOperationRecorder` 记操作日志（写 Diary + Staff，同事务）。`AdminActor`(StaffId,StaffName)、`PagedResult<T>`。
2. 控制器：`src/Szaipa.Web/Areas/Admin/Controllers/NewsController.cs` —— Index/Create/Edit/Delete，`CurrentActor()` 从 claims 取。
3. 视图：`Areas/Admin/Views/News/{Index,_Form,Create,Edit}.cshtml`。
4. 导航：`Areas/Admin/Views/Shared/_AdminLayout.cshtml`（「内容管理」下拉里加链接 + `contentControllers` 数组）。
- DI 注册：`SzaipaDataServiceCollectionExtensions.cs`。

**艺术家子模块**（Fav/Auction/Exhibition/将来 Works/ArtWorks）用泛型基类 `Services/Admin/ArtistScopedAdminRepository<T>`（实体实现 `IArtistScopedRecord`）——子类只给 DbSet/名词/字段拷贝。列表共用 `Views/Shared/_ArtistScopedList.cshtml`。

**已建模块**：Account(登录) / Dashboard / News / ArtNews / Publication(展览) / Fav / Auction / Exhibition / Upload。
**待建**：Artist、Works、ArtWorks（Phase 3 收尾）；Company、Tongou（Phase 4）；分析仪表盘（Phase 5）。

## 富文本 / 图片 / 画廊（前端组件）
- TipTap 编辑器：源 `wwwroot/admin/src/editor.js` → esbuild 打包 `wwwroot/admin/editor.js`；复用 partial `Areas/Admin/Views/Shared/_RichTextEditor.cshtml`（`RichTextEditorModel`）。内容存 HTML。
- 通用图片上传：`Areas/Admin/Controllers/UploadController` + `Services/Admin/AdminAssetStorage`（唯一 GUID 命名，写 `{ContentRoot}/{subfolder}`，规避旧版「子串匹配删图」bug）。封面用插件 `wwwroot/admin/upload-field.js`。
- 展览多图画廊：`wwwroot/admin/gallery-manager.js`（多图上传/↑↓排序/删除）+ `Services/Admin/ExhibitionGalleryFolder`（自动编号 10001+、两段式防碰撞重排、保留封面 10000，**有单测**）+ Web 包装 `ExhibitionGalleryStorage`。
- Tailwind：源 `wwwroot/admin/admin.input.css` → `admin.css`；设计令牌在 `tailwind.config.js`（brand #bf272d / canvas #f7f7f7 / muted #939393 / Noto 字体）。

## 公开站（Views/Home）
- 布局：`Views/Shared/_newLayout.cshtml`（多数页）、`_Artist.cshtml`（NewArt）。两者都加了可选 `@RenderSectionAsync("Styles"/"Scripts")`——页面级 CSS/JS 用 `@section` 挂。
- 已把巨量内联 CSS/JS **外提**到 `wwwroot/css/*.css`、`wwwroot/js/*.js`（newindex/newnews/newnewsread/newart + publication-gallery + exhibition-important）。NewArt 用「CSS 变量 + JS 桥接」保留动态值（`@artist.Color1/Path1`）。
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
