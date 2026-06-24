# 2026-06-24 后台改到 /Staff + 品牌纠正 + 本地调试固定账号

用户三点反馈,均已处理:

## 1. 后台 URL 应在 /Staff 下(原建在 /Admin,设计偏差)
用户指出「前台都在 Home、后台都在 Staff」,而迁移把后台建在了 `Areas/Admin`(URL `/Admin/...`)。按用户选择「清爽结构」,把整个后台区从 **Admin 改名为 Staff**:

- 目录 `Areas/Admin` → `Areas/Staff`(`git mv`);布局 `_AdminLayout.cshtml` → `_StaffLayout.cshtml`。
- 区内全量替换:命名空间 `Areas.Admin` → `Areas.Staff`;`[Area("Admin")]` → `[Area("Staff")]`;`[Route("Admin/X")]` → `[Route("Staff/X")]`;`asp-area="Admin"` → `asp-area="Staff"`;`[HttpGet("/Admin")]` → `[HttpGet("/Staff")]`;表单上传 `/Admin/Upload/Image` → `/Staff/Upload/Image`;画廊 `/Admin/Publication/*` → `/Staff/Publication/*`;`Layout="_AdminLayout"` → `_StaffLayout`。
- `Program.cs` cookie 路径:`LoginPath`/`LogoutPath`/`AccessDeniedPath` → `/Staff/Account/Login`、`/Staff/Account/Logout`。
- 前端 JS 源 `/Admin/` → `/Staff/`(`upload-field.js`、`editor.js`、`dashboard.js`)并重新打包。
- 公开站 footer 12 处入口 `/Admin/Account/Login` → `/Staff/Account/Login`。
- **保持不动**(非 URL、内部命名):`AdminAuthorization`(策略类)、`AdminWriteOptions`/`AdminWrite` 配置段(用户 Local.json 依赖)、静态资源目录 `wwwroot/admin/`(资源路径,非路由)。
- **命名碰撞修复**:区改名 Staff 后,`Szaipa.Web.Areas.Staff` 命名空间与 `Staff` 实体同名,`AccountController` 里对实体的 `Staff?` 声明改为全限定 `Szaipa.Data.Contexts.Szaipa.Staff?`。
- 登录现为 `/Staff/Account/Login`;旧 `/Admin/*` 已 404;未登录访问受保护页正确 302 到 `/Staff/Account/Login?ReturnUrl=...`。

## 2. 品牌错误:赛丽美术馆 → 深圳市艺术产业促进会
后台登录页/布局把站点写成了「赛丽美术馆」「赛丽」,实际是「深圳市艺术产业促进会」。已改全部 4 处(`_StaffLayout` 标题+顶栏 logo、`Login.cshtml` 主标题+脚注)。全 src 无「赛丽」残留。

## 3. 本地只读登录:加固定调试账号(用户要"账号密码")
之前已说明:只读模式无法新建账号、生产 Staff 密码是不可逆 MD5,我无法"导出"一个账号。按用户选择「加本地调试固定账号」:

- `AdminWriteOptions` 加 `DebugUserName`(默认 `debug`)、`DebugPassword`(默认 `debug`)、`DebugLoginEnabled`(= `UseReadOnlyConnectionForDebug`)。
- `AccountController.Login` POST 最前面:当 `DebugLoginEnabled` 且账号密码匹配调试账号时,**不碰数据库**直接签发为「调试管理员（只读）」身份。这样即使生产 Staff 表连不上(当前 szaipa_ro 报 18456)也能登录进去看后台结构。
- **严格门控**:仅在 `UseReadOnlyConnectionForDebug=true` 时生效,该开关只放 gitignore 的 `appsettings.Local.json`、提交/生产配置永不启用;`appsettings.Local.example.json` 文档化并警示。
- 账号密码重构出 `SignInStaffAsync` 辅助,正式登录与调试登录共用签发逻辑。

**给用户**:本地 `appsettings.Local.json` 已配 `AdminWrite:{EnableWrites:true, UseReadOnlyConnectionForDebug:true}`,用 **用户名 `debug` / 密码 `debug`** 即可登录只读后台(可在 Local.json 用 DebugUserName/DebugPassword 改)。生产环境配真正可写库后用真实 Staff 账号。

## 验证
- `~/.dotnet/dotnet build` 0/0;`test` 75 绿;`npm run build` 通过(产物已是 `/Staff/`)。
- 路由冒烟:`/Staff/Account/Login` 200(品牌"深圳市艺术产业促进会");`/Staff`、`/Staff/Dashboard`、`/Staff/News`、`/Staff/Artist`、`/Staff/TongouAtrist`、`/Staff/Dashboard/DailyVisits`、`/Staff/Account/PasswordChange` 未登录 302;旧 `/Admin/*` 404。
- 调试登录端到端:POST `debug/debug` → 302 跳 `/Staff/Dashboard`,带 cookie 访问 `/Staff/Dashboard` 200、显示「调试管理员（只读）」,只读库连不上时优雅显示「无法连接数据库」(不 500)。

## 备注
- 实测过程中发现 `src/Szaipa.Web/tailwind.config.js` 被误移到 `szaipa2022/`(致 build:css 失败),已从 git 恢复、删除误放副本。
- 生产读库 `szaipa_ro` 当前 18456 认证失败(凭据/环境问题,需用户核对密码)——调试固定账号让登录不再依赖它,但真实只读数据仍需该凭据修好。
