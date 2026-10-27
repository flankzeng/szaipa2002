# 2026-10-27 服务器 staging 部署（用户授权，Claude 经 `ssh szaipa` 操作）

## 授权范围
用户同意：A 不影响线上的准备工作全部执行；Hosting Bundle 立即安装（接受线上中断约半分钟）；存量图片预热放夜间。**未做**：建写库测试副本库、任何线上站点/绑定/IIS 全局配置改动、开放端口。

## 服务器现状（只读摸底）
- Windows Server 2019 Datacenter，2 核，8GB 内存，仅 C 盘（60GB，剩约 8.9GB）。
- IIS：`szaipa`（线上，`C:\Users\Administrator\Desktop\git\web24.05`，443 szaipa.com/www）、`cjt`、`szaipa-http-redirect`（80→443）。
- SQL Server 默认实例 MSSQLSERVER 运行中。

## 已完成的改动
| 项 | 内容 |
|---|---|
| 运行时 | 安装 ASP.NET Core Hosting Bundle **10.0.14**（官方地址下载，SHA-512 校验通过），注册 `AspNetCoreModuleV2`。安装期间 IIS 重启；前后线上首页均 200。 |
| 目录 | `C:\SzaipaData\{releases,image-cache,dp-keys,logs,installers}` |
| 发布包 | `C:\SzaipaData\releases\faadcb8`（SHA-256 与本地一致）；旧的 `efb56aa` 目录保留作对照 |
| 配置 | 发布目录内 `appsettings.Production.json`：只读浏览、后台写入关闭、Content 指向线上 `web24.05\Content`、图片缓存 `C:\SzaipaData\image-cache`、DataProtection 密钥 `C:\SzaipaData\dp-keys`、`SecurityMonitoring:ServerSecurityRoot=C:\Security`（请求审计关闭）。web.config 打开 stdout 日志到 `C:\SzaipaData\logs\`。 |
| 凭据 | 只读连接串（`szaipa_ro`/`tongou_ro`，改连 `localhost`）作为应用池 `SzaipaNew` 的环境变量注入，未写入文件；`ASPNETCORE_ENVIRONMENT=Production`。 |
| IIS | 新应用池 `SzaipaNew`（无托管代码）+ 新站 `szaipa-new`，**仅绑定 127.0.0.1:8088**，外网不可访问；不动线上站。 |
| 权限 | `IIS AppPool\SzaipaNew`：发布目录读、`C:\Security` 读、缓存/密钥/日志目录修改。 |
| 定时任务 | `Szaipa-ImageWarm-Once`：2026-10-28 02:00（服务器本地时间）一次性、低优先级、最长 3 小时，执行 `image-variants-warm`，日志 `C:\SzaipaData\logs\image-warm.log`。 |

## 部署中发现并修复的上线阻断
`Program.cs` 自建宿主只调用 `UseKestrel()`，没有 `UseIIS()`：IIS 进程内托管收不到请求，启动超时 **HTTP 500.30**，应用池回收时新旧进程还争抢 5057 端口。修复为 `UseKestrel().UseIIS().UseIISIntegration()`，新增契约测试（提交 `faadcb8`，673 测试绿）。

## 验证（经 SSH 隧道 `localhost:18088 → 服务器 127.0.0.1:8088`）
- `/healthz`、首页、新闻、关于、会员风采、艺术家、展会列表、展览、春语三专题、后台登录：全部 200。
- 读取真实生产数据（只读账号）；WebP 变体在服务器上生成并写入缓存，二次命中缓存；小目录试跑预热：25 张 0 失败。
- 线上 www.szaipa.com 全程 200。

## 发现的既有安全隐患（未改动，需用户决定）
线上 `web24.05\Content` 目录 ACL 为 **Everyone: FullControl**。任何能在该机运行代码的账户都能改写网站图片/脚本。建议收紧为：Administrators/SYSTEM 完全控制，IIS 应用池只读（后台上传目录单独给修改）。

## 回退
- 新站：`Stop-Website szaipa-new; Remove-Website szaipa-new; Remove-WebAppPool SzaipaNew`，删除 `C:\SzaipaData`。
- 定时任务：`Unregister-ScheduledTask Szaipa-ImageWarm-Once`。
- Hosting Bundle 可在「程序和功能」卸载（不影响 .NET Framework 旧站）。

## 下一步
1. 次日查看 `image-warm.log`，确认预热完成。
2. 真实写入验收：需在 SQL Server 建 `Szaipa_staging`/`Tongou_staging` 副本 + 专用可写账号（高权限操作，待用户确认），然后把 staging 站的 `AdminWrite` 指向副本库，后台全模块增删改走一遍。
3. 正式切换按 `docs/automation-rollout.md`。

## 追加：真实 SQL Server 写入验收（用户授权的 9 步）
- **副本库**：`BACKUP ... WITH COPY_ONLY, CHECKSUM` 备份 Szaipa/Tongou（不影响备份链）→ 还原为 `Szaipa_staging`/`Tongou_staging`（`C:\SzaipaData\staging\db\`，SIMPLE 恢复模式）。生产库仅被读取。
- **上线 SQL 首次在真实 SQL Server 2016 执行**：`automation-deploy.sql` 目标改为 Szaipa_staging，11 张新表 + 种子写入成功，重复执行幂等。⚠️ **服务器上必须用 `sqlcmd -f 65001`**（或 SSMS 以 UTF-8 打开）：默认按 GBK 读 UTF-8 脚本会吞掉 `:setvar` 行导致「未定义 TargetDatabase」。另有警告：`UX_FeaturedExhibition_LinkUrl` 键长上限 2000 字节超过 1700 建议值（链接超过 850 字才会失败）。
- **专用账号** `szaipa_staging_rw`：仅两个 staging 库的 db_datareader/db_datawriter；已验证对生产 Szaipa/Tongou 无访问权限、非 sysadmin。密码仅存在于应用池环境变量。
- **图片副本** `C:\SzaipaData\staging-content`（1.15GB）+ 独立缓存 `C:\SzaipaData\staging-image-cache`；staging 站配置已切到副本库与副本图片，后台写入开启。
- **测试管理员** `claude_staging_test`（仅 Szaipa_staging，Id 1019），密码在本机 gitignore 的 `artifacts/verification/staging-test-login.txt`。
- **验收**：后台 16 个模块增删改 + 超长名称兜底 = **17/17 通过**（脚本 `artifacts/verification/claude-2026-10-26/staging-http-crud.py`，写入前强制核对连接串指向 *_staging）。首轮发现并修复 4 个只在真实库出现的问题（`43f7eb6`）：Artist.Sex、Publication.CoverPath/Location 为 NOT NULL；TongouAtrist/TongouWorks.id 非自增；超长/必填错误改为中文提示（`StaffSaveErrorFilter`）。
- 部署版本现为 **`43f7eb6`**（`C:\SzaipaData\releases\43f7eb6`，SHA-256 `f6763dc68dfa9e29ea3add7c62d1f779441883ec3e927517623dc95efffec32b`）；夜间预热任务已改指向 43f7eb6，并用环境变量固定为线上 Content + 正式缓存目录。
- 登录 cookie 在 Production 强制 Secure：经 http://localhost 隧道时浏览器可用（Chrome 视 localhost 为安全来源），脚本需放宽 cookie 策略。
