# 后台自动化上线与回退手册

> 本手册由用户执行。Agent 只在本机构建、测试、读生产快照；不部署、不登录服务器、不执行生产 SQL。
> 与 [Windows 发布契约](deploy-windows.md)、[凭据管理](security-secrets.md) 配套使用。
> **当前仍在集成 T1/T2/T4；最终包、SQL 汇总与验收记录以 HANDOFF 的最新状态为准，不能仅凭本手册判断已经上线就绪。**

## 1. 发布前留存

先备份 Szaipa/Tongou 数据库、共享 Content、现有受保护配置及 Data Protection key，并记录旧站 physical path、应用池和后台唯一写入者。备份保存在受控位置，不进入仓库或应用发布包。

新版包只替换应用目录。不得覆盖、清理或复制整个共享 Content，不得把数据库镜像当成上线数据库。旧站源码里的管理连接串不能作为新版配置来源。

## 2. 应用包

在已验证的集成 commit 上运行前端构建、.NET build/test，再面向 Windows x64 生成 framework-dependent 发布目录：

```bash
npm --prefix src/Szaipa.Web ci
npm --prefix src/Szaipa.Web run build
~/.dotnet/dotnet build Szaipa.Modernization.slnx --disable-build-servers -m:1
~/.dotnet/dotnet test Szaipa.Modernization.slnx --no-build --disable-build-servers -m:1
~/.dotnet/dotnet publish src/Szaipa.Web/Szaipa.Web.csproj -c Release \
  -r win-x64 --self-contained false -p:UseAppHost=false -o artifacts/publish-win-x64
python3 scripts/test-audit-release.py
python3 scripts/audit-release.py artifacts/publish-win-x64 \
  --manifest artifacts/publish-win-x64.manifest.json --revision <完整源码SHA>
```

使用新输出目录，不覆盖旧包。清单须在目录外，禁止复用已存在的清单文件。交付时连同 zip 的 SHA-256、源码 SHA、测试结果与 SQL 脚本一起留存。发布包不包含配置凭据、`App_Data`、qqwry.dat、SQLite 镜像、原图或 Content。

服务器须已有对应 net10.0 ASP.NET Core Hosting Bundle、AspNetCoreModuleV2 和 No Managed Code 应用池。包内不附 .NET runtime；必须先在独立测试 hostname 验证。SQLite 依赖保留但 Production 硬禁镜像模式。

## 3. 数据库升级顺序

停止所有后台写入，保留数据库备份后，由用户在正确目标数据库执行 SQL。新版公开只读账号仍只授予 SELECT，后台使用单独的受限可写账号。

1. 先检查 `2026-06-exhibition-template-columns.sql` 与 `2026-07-09-exhibition-works-catalog.sql` 的前置结构；如未建，再由用户执行。
2. 按本 Epic 上线汇总脚本的顺序创建同步记录、首页静态块、展览专题/推荐、艺术家官网结构。脚本路径和最终校验见最新 HANDOFF。
3. **不要求执行 slug 迁移 SQL**：生产 Publication 当前为空，14 个历史展览仍由只读兼容目录提供；三个特殊页仍使用原 URL。不能为了给新功能建表而顺便归一历史数据。
4. 结构创建后，用只读账号确认新表和列可见，再切换应用。重复执行种子脚本不得覆盖后台已编辑的数据。

SQL Server 脚本不能直接交给 SQLite。开发镜像通过当前模型重新运行 `mirror-sync` 建结构；此操作会重建镜像，先保留本地验证数据。

## 4. 配置与权限

以下均通过受保护环境变量或配置注入；尖括号是现场填写项，不是真实凭据。

| 配置 | 上线值 / 作用 |
|---|---|
| `ASPNETCORE_ENVIRONMENT`、`DOTNET_ENVIRONMENT` | `Production`；两者保持一致 |
| `LocalMirror__Enabled` | `false`；不得将开发镜像配置复制到服务器 |
| `RuntimeSafety__UseLegacyDataSources` | `true` |
| `RuntimeSafety__AllowLiveDatabase` | `false`；公开站仍通过显式 ReadOnly 数据源 |
| `LegacyData__Szaipa__AccessMode`、`LegacyData__Tongou__AccessMode` | `ReadOnly` |
| `LegacyData__Szaipa__ConnectionString`、`LegacyData__Tongou__ConnectionString` | 对应库的最小权限只读账号 |
| `ReadOnlyMigration__EnableSzaipaReadModels`、`ReadOnlyMigration__EnableTongouReadModels` | `true` |
| `ConnectionStrings__SzaipaAdmin`、`ConnectionStrings__TongouAdmin` | 后台真正可写目标库，不能填只读连接串；与公开读源对应同一数据集 |
| `AdminWrite__EnableWrites`、`TongouAdminWrite__EnableWrites` | 并行验证保持 `false`；停用旧后台写入、验证新后台后才开启 |
| `AdminWrite__UseReadOnlyConnectionForDebug`、`TongouAdminWrite__UseReadOnlyConnectionForDebug` | `false`，生产禁用固定调试账号 |
| `LegacyAssets__ContentRoot` | 现有共享 Content 的绝对路径；上传启用后给应用池写所需子目录的权限 |
| `DataProtection__ApplicationName` | `Szaipa.Web`，跨 release 保持稳定 |
| `DataProtection__KeysPath` | release 根外的绝对持久目录，可读写；Windows machine-scoped DPAPI，不可跨机器直接复用 |
| `IpGeolocation__QqwryPath` | 外部 qqwry.dat 绝对路径；缺失时仅地理图表降级 |
| `AllowedHosts` | 正式 hostname；测试站用独立 hostname |
| `SecurityMonitoring__ServerSecurityRoot` / `AuditDirectory` | 既有受控外部目录；写权限仅给审计和指定队列目录 |

Staging 使用独立 cookie 名、hostname、ApplicationName 和 key 目录，不能共享 Production 登录会话。`/healthz` 成功仅说明应用启动，不代表数据库、资源或表单写入已通过。

## 5. 微信启用顺序

公众号为未认证「其他组织」，access_token 正常不代表各内容接口有权限。默认先用后台 `/Staff/WeChat` 批量链接导入补齐群发历史；freepublish「发布」记录与「群发历史」不是同一份数据。

1. 在受保护配置填写 `WeChat__AppId`、`WeChat__AppSecret`，将实际调用端公网 IP 加入公众号白名单。不要在聊天、命令日志或 Git 填写凭据。
2. `WeChat__Enabled=false` 时链接导入仍可使用；先选几条真实文章核对正文、发布时间、图片落盘、来源链接与去重。
3. 运行 `wechat-probe`，按能力分别设置 `WeChat__Sources__FreePublish` / `WeChat__Sources__Material`。关闭无权限来源，不反复消耗 token。
4. 选定来源后开启 `WeChat__Enabled=true`；启动后先全量发现链接，成功后按 `WeChat__SyncIntervalMinutes` 增量查询（默认 60，最低 5）。重启会重新完整发现链接，已存在去重键只跳过、不覆盖人工编辑；失败的首轮会继续回填。接口来源只发现文章链接，正文仍按新闻模板导入。
5. 切换前记录当时旧 News 的最大 Id，设置 `WeChat__LegacyNewsMaxId=<切换分界Id>`。只从列表/首页/推荐隐藏分界之前的手录新闻；旧详情 URL 不删除，同步新闻不受此分界误伤。未配或 0 时不隐藏。
6. 在后台抽查隐藏/取消隐藏、置顶与现有 News 编辑。自动发现不会覆盖已有去重键的人工编辑；「重新抓取」会覆盖正文，需要明确用于希望更新的文章。

**回调默认关闭**：`WeChat__Callback__Enabled=false`。开启公众号「服务器配置」会接管消息，使编辑模式的自动回复/菜单失效；必须由用户决定是否切换。当前回调只支持明文验签，不支持消息加解密模式。文章页返回微信验证页时不要绕过验证；先重试正常浏览、按可用来源导入，保留错误记录。

## 6. 切换验证

先在独立测试站验证，所有写操作只对本地镜像或明确选择的新后台目标进行；Agent 的验收仅使用本地镜像。

- 公开路由：`/Home/Index`、`News`、`NewsRead/{id}`、`Vip`、`About`、`Constitution`、`Art/{id}`、`Art/{id}/Archive`、`Art/News/{id}`、`PublicationList`、普通/重要/新专题与三个手写专题，390/1440 两种宽度。
- 兼容行为：旧 `new*` URL 永久重定向、退役公开层 410、旧新闻详情仍能访问、14 个兼容展览可读。
- 资源：首图、新闻封面和正文图、艺术家头像/横幅/作品、图库与字体；微信正文不热链第三方图片。
- 后台：登录/退出、各内容模块新增修改删除、排序/显隐与操作审计；新专题至少四种块；艺术家批量作品、拖拽排序、配色预览、头像裁切与原图重裁。
- 缺表/列：新结构未执行时公开页回退，后台展示升级提示；不能让旧路由变 500。
- 会话：从 release A 切到 B 后，同一正式 hostname 的后台 cookie 仍可用。

全部通过后由用户停用旧后台写入，再启用新后台为唯一写入者。Agent 不自动授予权限或切换 IIS。

## 7. 回退

优先将 IIS physical path 恢复到上一个已验证 release，关闭微信自动同步与新版后台写入，保留数据库新增表/列及 Content 新文件。新增结构是增量，应用回退无需删除它们，保留同步记录才能继续避免重复导入。

如果回退原因是数据损坏，先冻结所有写入、备份当前现场，再由用户从发布前数据库备份恢复；这会丢弃备份后的有效编辑，必须先决定如何保留。不要直接批量删除 News 或按文件名清理图片。

本 Epic 的数据库回退脚本默认执行只读盘点；真正的结构拆除须备份并显式启用。新增表非空，或作品排序、隐藏默认展讯、展览主题色/横幅列已有非默认编辑值，都会拒绝拆除。Content、keys、qqwry.dat 都不属于脚本清理范围。回退后重跑公开路由、登录和旧后台唯一写入验证。
