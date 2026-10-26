# 2026-10-26 — 发布边界与依赖补丁（T5 前置）

## 改动与原因

接手集成分支时 422 项测试全绿，但实际 restore 缓存已经报告 NU1902/NU1903，不能沿用旧文档里的「只有 NU1900」描述。

- `Szaipa.Data` 显式固定 `SQLitePCLRaw.bundle_e_sqlite3` **2.1.14**（保持 2.1 系列，不切换 3.x）。此前 T0 已让 SQLite 随应用发布，测试项目「SQLite 仅测试使用」的漏洞压制与注释失效，现已删除。
- 显式固定 `System.Security.Cryptography.Pkcs` **10.0.13**，覆盖 SqlClient 的旧版间接依赖。
- 发布排除补全：`wwwroot/public/src/**`、整个 `App_Data/**`、所有 Local/Production 配置变体。qqwry.dat、密钥与上传原图都属于外部运行数据。
- 新增 `scripts/audit-release.py`：只读检查必需 DLL/编译资源/IIS 入口、禁止文件、非空凭据与调试写开关；可在目录外生成逐文件 SHA-256 JSON 清单。诊断只报告配置键，绝不打印密钥。
- 新增 `scripts/test-audit-release.py`：回归覆盖误打包数据库/外置资源/源代码、连接串不泄露、错误 IIS 入口、符号链接与内容哈希变化。

补丁依据：[SQLitePCLRaw 2.1.14 包及依赖](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/2.1.14)、[Microsoft Pkcs 修复公告](https://github.com/advisories/GHSA-846f-f764-76mx)。没有屏蔽审计或只隐藏告警。

## 本机验证

- 使用 `~/.dotnet/dotnet`，补丁 restore 成功。
- build：**0 warning / 0 error**；Data 155 + Web 267 = **422 全绿**。
- `python3 scripts/test-audit-release.py`：**5 全绿**。
- 初步 Windows 目标发布：`dotnet publish ... -c Release -r win-x64 --self-contained false -p:UseAppHost=false` 成功，包含正确的 IIS `web.config`。
- 发布边界审计通过：211 文件、42,168,863 B；SQLite 相关共 2,590,912 B。采用明确 `win-x64` RID 已避免携带其他平台原生库，暂保留 SQLite，减少条件编译与依赖文件被手删的风险；Production 仍硬禁 LocalMirror。
- 此为 **T1/T2/T4 合并前的基线试包** `artifacts/baseline-win-x64/`，不能作为本 Epic 完成交付。集成完成后必须重建、重测、重出清单。

测试进程需绑定回环端口；受限沙箱内会在测试框架启动时 Permission denied。允许本地端口后全部通过，此问题与业务测试失败无关。

## 复跑

```bash
~/.dotnet/dotnet publish src/Szaipa.Web/Szaipa.Web.csproj -c Release \
  -r win-x64 --self-contained false -p:UseAppHost=false -o artifacts/publish-win-x64
python3 scripts/test-audit-release.py
python3 scripts/audit-release.py artifacts/publish-win-x64 \
  --manifest artifacts/publish-win-x64.manifest.json --revision <完整的已验证源码SHA>
```

清单路径须在发布目录外且不得已存在，避免覆盖旧交付证据。该工具不会连接数据库、执行 SQL、打包外置 Content 或部署服务器。

## 用户待办

本步骤无需手动操作。真正上线仍须满足部署手册的 Hosting Bundle、受保护配置、SQL、Content 与 Data Protection 契约；最终手册由 T5 集成补齐。
