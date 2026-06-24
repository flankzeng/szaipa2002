# 2026-06-24 本地只读登录调试模式 + 登录页优雅降级

## 背景 / 边界（用户澄清）
绑定规则是「**agent（Claude）绝不写库**」——不执行任何 INSERT/UPDATE/DELETE、不对真实库做迁移/`EnsureCreated`/播种、不跑打生产库的测试、不在代码或 commit 里放生产连接串/凭据（单测一律内存 SQLite，冒烟只做只读 302）。**用户本人**用自己的账号让运行中的应用连生产库是允许的（那是用户操作）。真正的写入推迟到**发布/部署环境**才做；本地调试只需要**只读登录、查看后台**。

## 问题
登录页在未配置可写库时直接抛 500（`SzaipaAdminContext was requested but the admin write path is not configured`）。而本地又没有可写副本，按边界也不该指向生产可写。

## 改动
### 1. 登录页优雅降级（修 500）
`AccountController` 注入 `IOptions<AdminWriteOptions>`：未配置可写库时在登录页提示「后台尚未配置可写数据库…」；配置了但连不上/认证失败时 catch `DbException` 提示「无法连接后台数据库…」。不再抛未处理异常 500。

### 2. 本地只读调试模式 `UseReadOnlyConnectionForDebug`
`AdminWriteOptions` / `TongouAdminWriteOptions` 各加一个布尔开关。当 `EnableWrites=true`、**未设** `ConnectionStrings:SzaipaAdmin`、且 `UseReadOnlyConnectionForDebug=true` 时，admin 上下文**复用已有的只读连接**（`szaipa_ro`/`tongou_ro`，`db_datareader`），来源同读侧（`LegacyData:Szaipa:ConnectionString` / 环境变量 `SZAIPA_READONLY_CONNECTION` / `ConnectionStrings:Szaipa`）。

- 效果：登录（读 Staff）、仪表盘、列表/编辑 GET 都能跑；任何写（POST→`SaveChanges`）会被 SQL 层挡掉，因为账号物理上无写权限——**可证明的只读**，不靠约定。
- 安全：该开关只放在 gitignore 的 `appsettings.Local.json`，提交/生产配置永不启用；生产用真正可写的 `SzaipaAdmin` 连接。
- DI 里新增 `ResolveReadOnlyConnection` 辅助；`appsettings.Local.example.json` 文档化两种用法（A 本地只读检查 / B 发布环境真实写入）。

## 验证
- `~/.dotnet/dotnet build` 0/0；`test` 75 绿（未动测试逻辑）。
- 本地运行（在 gitignore 的 Local.json 临时打开 `AdminWrite:{EnableWrites,UseReadOnlyConnectionForDebug}`）冒烟：登录 GET 200；POST 假账号返回 **200 + 「无法连接后台数据库」**（而不是旧的 500）——证明优雅降级生效、且只读复用机制确实把 admin 上下文接到了读侧连接。
- **遗留环境问题（非代码）**：本次连接 prod 读库时 SQL 报 `18456 用户登录失败`——即 `szaipa_ro` 凭据当前被服务器拒绝（2026-06-22 时可用，现在认证失败；公开读侧因 `AllowLiveDatabase=false` 处于 skeleton/DB-off，所以一直没暴露这个问题）。需用户核对/刷新 `appsettings.Local.json` 里的只读账号密码（属用户凭据，agent 不碰）。凭据修好后，本地只读登录即通。

## 用户本地操作（要登录进后台检查）
1. 确认 `appsettings.Local.json` 里 `LegacyData:Szaipa:ConnectionString` 的 `szaipa_ro` 密码当前有效（修掉 18456）。
2. 该文件已加 `"AdminWrite": { "EnableWrites": true, "UseReadOnlyConnectionForDebug": true }`（Tongou 同）——本会话已替你写入（gitignore，不提交）。
3. `~/.dotnet/dotnet run --project src/Szaipa.Web/Szaipa.Web.csproj`，访问 `/Admin/Account/Login` 用真实 Staff 账号登录即可只读浏览。写操作会被只读账号挡下，留到发布环境再开真正可写库。
