# 更新 — 2026-10-26：T0 本地镜像库（生产只读 → 本地 SQLite 可写副本）

## 1. 为什么

后台自动化 Epic（T1–T4）全要验证「后台录入 → 前台显示」。这台 Mac 没有 Docker / SQL Server，而生产库**永不写**，
此前读侧连生产只读、写侧连另一个本地库，两边是两个库，admin 建的数据前台看不到。

LocalMirror 把生产库用只读账号 `SELECT` 一遍，落成**两个 SQLite 文件**，然后让四个上下文
（`SzaipaLegacyReadContext`、`TongouLegacyReadContext`、`SzaipaAdminContext`、`TongouAdminContext`）全部改连它们：
读上下文仍禁 `SaveChanges`，只有 admin 上下文可写。读写同库，端到端点测成立；生产库零写入。

## 2. 怎么用

```bash
# 1) 构建镜像（用 appsettings.Local.json 里现成的 LegacyData:Szaipa/Tongou 只读连接）
~/.dotnet/dotnet run --project src/Szaipa.Web -- mirror-sync
#    可选参数
#    --days N            AccessData / Diary 等大日志表只拷最近 N 天（默认 90；0 = 全量）
#    --only szaipa|tongou 只重建其中一个库
#    --batch N           每批行数（默认 500）

# 2) 以镜像模式启动站点（二选一）
LocalMirror__Enabled=true ~/.dotnet/dotnet run --project src/Szaipa.Web/Szaipa.Web.csproj --urls http://127.0.0.1:5057
#    或在 appsettings.Local.json 里写 "LocalMirror": { "Enabled": true }

# 3) 后台登录：/Staff/Account/Login，调试账号 debug / debug（镜像里 Staff 表是生产的 MD5 密码，不要破解）
```

`mirror-sync` 随时可重复执行：**先写 `<目标>.syncing` 临时文件，全部成功后才替换正式文件**——中途断网或失败，旧镜像原样保留；
成功则等于「删库重建」。重建前请先停掉正在运行的站点。目标文件若已存在但不是 SQLite 文件，会拒绝覆盖（防配错路径误删）。

### 配置项（`LocalMirror` 节）

| 键 | 默认 | 说明 |
|---|---|---|
| `Enabled` | `false` | 总开关。启用后四个上下文全部 `UseSqlite`。 |
| `SzaipaPath` | `<仓库根>/.local-db/szaipa.db` | 相对路径按仓库根解析（仓库根 = 向上找到 `Szaipa.Modernization.slnx`）。 |
| `TongouPath` | `<仓库根>/.local-db/tongou.db` | 同上。 |
| `DebugLogin` | `true` | 镜像模式 + **Development** 环境下开放调试账号登录；设 `false` 关闭。账号密码取 `AdminWrite:DebugUserName/DebugPassword`，默认 `debug`/`debug`。 |

`.local-db/` 已加入 `.gitignore`（里面有生产 Staff 密码哈希等数据，**不要提交、不要外传**）。

## 3. 启用后行为变化（实现要点）

- **Production 拒绝**：`AddSzaipaData` 里 `LocalMirror:Enabled=true` 且环境为 Production（或环境名拿不到，按 ASP.NET Core 默认视为 Production）→ 直接抛 `InvalidOperationException`，启动失败。
- **选项被改写成「已配置」**：`LegacyDataOptions`（AccessMode=ReadOnly、连接串=镜像文件、来源标签 `LocalMirror`）、`AdminWriteOptions` / `TongouAdminWriteOptions`（`EnableWrites=true`、连接串=镜像文件）。因此 `HomeController` 的「读模型是否激活」判断、后台登录/仪表盘/运维页的 `IsConfigured` 门控、`/healthz` 全部自然通过，不需要改任何控制器逻辑。注册在原有 `PostConfigure` 之后，所以即使 Local.json 里还留着 `UseReadOnlyConnectionForDebug=true`，admin 连接也不会回落到生产只读连接。
- **镜像文件不存在** → 解析上下文时抛带 `mirror-sync` 指引的明确错误（否则 SQLite 会静默新建空库，之后到处 `no such table`）。
- **读上下文**：仍 NoTracking、`SaveChanges` 硬禁（有单测）。
- **调试登录**：沿用既有 `AccountController` 的调试账号机制，新增 `AdminWriteOptions.MirrorDebugLoginEnabled`（仅镜像 + Development + `DebugLogin`）；登录后显示名为「调试管理员（本地镜像）」。
- 无论是否镜像模式，默认（`Enabled=false`）行为与之前完全一致。

## 4. mirror-sync 怎么拷（给后续任务读）

- 数据源：`SqlServerMirrorSource`，用 `LegacyData:Szaipa/Tongou:ConnectionString`（缺省回落环境变量 / `ConnectionStrings:*`，优先级同读上下文）。**它是触碰生产库的唯一入口，只发三种 SELECT**：读列名（`INFORMATION_SCHEMA.COLUMNS`）、`COUNT_BIG`、`SELECT TOP (@take) … WITH (NOLOCK) WHERE [Id] > @after ORDER BY [Id]`（按主键 keyset 分页，不用 OFFSET）。所有 SQL 文本集中在 `MirrorSql`，单测断言每条以 `SELECT` 开头、不含任何写 / DDL 关键字。输出不打印连接串。
- 目标库：`SzaipaAdminContext` / `TongouAdminContext` 的 EF 模型 → `EnsureCreated()` 建表（含 `HasDefaultValue`）→ 按实体逐表分批写入，**保留原 Id**（SQLite 自增序列自动跟上最大 Id）。写入走 EF 实体而不是手拼 SQL，所以 `DateTime` / `bool?` / `decimal` 的 SQLite 存储格式与 EF 读取天然一致。
- **新实体自动出现**：镜像表结构完全由 admin 上下文的模型生成。T1–T4 新增实体 / 列后，只要在 `SzaipaAdminContext`（以及读上下文）注册了，**重跑 `mirror-sync` 就自动建出对应表**；生产还没有的表保留空表（日志里显示「来源无此表，保留空表」），生产没有的列只拷两边共有的列、其余取默认值（日志列出「来源缺列」）。这同时意味着：镜像里这些表**永远存在**，「缺表时前台降级」的分支在镜像模式下测不到，需要时手动删表或用生产只读模式验证。
  - ⚠️ 合并了带新实体 / 新列的分支后，旧镜像文件里没有这些表——记得重跑 `mirror-sync`。
- 日志表：`AccessData`、`Diary`（见 `MirrorSyncSettings.DateFilteredTables`）只拷 `Date ≥ 今天 − N 天`，默认 90 天；其余表一律全量。
- 生产里没有真主键约束的表（实测 `TongouWorks` 的 `id=211` 出现两次）：按主键升序读，重复键仅保留首条并在日志里提示跳过行数。NOT NULL 列遇到 NULL 取默认值并计数。
- 目标文件最终切到 WAL 模式（写进文件头），站点运行时读上下文与写上下文各持连接互不阻塞。

## 5. 与生产的差异 / 注意事项

- **SQLite 与 SQL Server 语义差异**：字符串 `=` 比较区分大小写（SQL Server 默认不区分）；`LIKE` 仅对 ASCII 不区分大小写；`decimal` 聚合（Sum/Avg）EF SQLite 不支持。当前仓储查询未受影响，后续新仓储若在镜像上出现怪异结果先想到这几点。
- 镜像是**快照**，生产上之后的变化不会同步，需要新数据就重跑 `mirror-sync`。`AccessData` 等是最近 90 天的子集，仪表盘访问统计据此计算。
- 后台上传图片仍落到 `LegacyAssets:ContentRoot`（本地 `web24.05/Content` 副本），与镜像库无关；别把它指向服务器目录。
- 发布包会多带 SQLite 原生库（`Microsoft.EntityFrameworkCore.Sqlite` 的 `runtimes/*/native/e_sqlite3`）。镜像模式在 Production 被硬禁，这些库在线上只是无用负载；若要瘦身可后续把 SQLite 引用挪到仅 Development 的条件项（T5 发布包检查时评估）。
- 镜像里的 `Staff.Password` 是生产的 MD5：不要破解，本地用调试账号；`Staff` 表在镜像里的 `OperationRecord` 等字段也会被后台操作改写，仅影响本地副本。

## 6. 本次改动文件

- 新增 `src/Szaipa.Data/LocalMirror/`：`LocalMirrorOptions`（配置 / 仓库根 / 环境解析 / Production 拒绝）、`LocalMirrorRegistration`（DI 接线）、`IMirrorSource` + `SqlServerMirrorSource` + `MirrorSql`（只读来源）、`LocalMirrorSyncService`（逐表拷贝）、`LocalMirrorSyncRunner`（`mirror-sync` 命令）。
- 修改（最小追加）：`SzaipaDataServiceCollectionExtensions.cs`（4 个 `UseSqlServer` → `LocalMirrorRegistration.UseProvider`，加一段镜像接线，`ResolveReadOnlyConnection` 改 `internal`）、`AdminWriteOptions.cs`（`LocalMirrorActive`、`MirrorDebugLoginEnabled`、`DebugLoginEnabled`）、`Szaipa.Data.csproj`（`Microsoft.EntityFrameworkCore.Sqlite` 10.0.9）、`Program.cs`（`mirror-sync` 命令入口）、`AccountController.cs`（调试账号显示名）、`.gitignore`（`.local-db/`）、`appsettings.Local.example.json`（示例）、`docs/PROJECT_MAP.md`（镜像模式说明）。
- 单测：`tests/Szaipa.Data.Tests/LocalMirrorTests.cs`（30 项：选项解析、Production 拒绝、provider 切换、读上下文禁写、后台写入读侧可见、拷贝逻辑、只读 SQL 护栏等）。

## 7. 验证结果（2026-10-26，本机）

- `build` 0 error；`test` 全绿（原基线 345 → 375：Data 132 + Web 243）。
- 真实跑了一次 `mirror-sync`（默认 90 天），各表行数：

  | Szaipa 库 | 行数 | | Tongou 库 | 行数 |
  |---|---|---|---|---|
  | AccessData（近 90 天） | 163,339 | | TongouAtrist | 108 |
  | Diary（近 90 天） | 89 | | TongouWorks | 204（来源 205 行，`id=211` 重复，跳过 1） |
  | News | 73 | | | |
  | Works | 117 | | | |
  | WorksTag | 983 | | | |
  | Tag | 91 | | | |
  | Artist | 24 | | | |
  | Staff | 18 | | | |
  | ArtNews | 6 | | | |
  | Auction | 4 | | | |
  | Exhibition | 2 | | | |
  | Fav | 2 | | | |
  | Company | 1 | | | |
  | Publication / Activity / MonthData | 0（生产即为空；Publication 生产缺 `Type/Preface/Signature` 三列，镜像取默认值） | | | |
  | ExhibitionWork | 0（生产无此表，保留空表） | | | |

  合计 Szaipa 164,749 行 17 表、Tongou 312 行 2 表，耗时约 10 秒；`.local-db/szaipa.db` ≈ 18MB。
- 前台：同一份代码分别以「生产只读模式」和「镜像模式」启动，逐页对比响应——首页、新闻列表、新闻详情（1123/1124）、艺术家会员页 `/Home/Vip`、艺术家官网 `/Home/Art/1000`、`/Home/Art/1009`、`/Home/Art/1000/Archive`、`/Home/Art/News/1000`、`/Home/PublicationList`、`/Home/About`、`/Home/Constitution`、`/Publication/chunyu3`：状态码一致且**响应正文逐字节相同**。
- 后台（镜像模式，调试账号登录）：Dashboard（含 DailyVisits / ContentAccess / Geo）、News、Artist、ArtNews、Works、Fav、Auction、Exhibition、Company、Publication、Operations、TongouAtrist、TongouWorks、SecurityMonitoring、PasswordChange 全部 200。
- 端到端：登录 → `/Staff/News/Create` 新建「MIRROR-E2E-…」→ 前台 `/Home/News` 与首页立即可见、详情 200 → `/Staff/News/Delete` 删除 → 前台列表不可见、详情 404。测试数据已删除。
- Production 拒绝：单测覆盖（`AddSzaipaData` 抛异常）。实机以 `ASPNETCORE_ENVIRONMENT=Production` 启动在 macOS 上会先被 DPAPI 策略拦下，到不了这一步，所以以单测为准。

## 8. 需要用户手动做的事

无。（`appsettings.Local.json` 里已有只读连接即可跑 `mirror-sync`；不需要执行任何生产 SQL。）
