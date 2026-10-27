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
