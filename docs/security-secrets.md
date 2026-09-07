# 数据库凭据安全管理

**原则：任何仓库跟踪文件与部署目录里都不出现明文凭据。**

- 本地开发：`dotnet user-secrets`（凭据存用户目录，不进仓库）。
- 生产（szaipa2026 / Windows Server + IIS）：**环境变量**注入，应用目录零明文。
- legacy `szaipa2022/`（将弃用）：连接串在 gitignored 的 `connectionStrings.config`，Web.config 用 `configSource` 引用。

## 现代版（src/Szaipa.Web）

配置加载优先级（Program.cs）：`appsettings.json` → `appsettings.{环境}.json` → `appsettings.Local.json` → **user-secrets（仅开发）** → **环境变量** → 命令行。

### 生产：环境变量（IIS）

IIS 10 支持在应用池上设环境变量（`<environmentVariables>`）。在部署机上为应用池 `Szaipa2026` 设置（PowerShell，管理员）：

```powershell
$pool = "Szaipa2026"
$vars = @{
  "LegacyData__Szaipa__ConnectionString"      = "Server=<SQL_HOST>,1433;Database=Szaipa;User Id=<RO_USER>;Password=<SECRET>;Encrypt=True;TrustServerCertificate=True;"
  "LegacyData__Tongou__ConnectionString"      = "Server=<SQL_HOST>,1433;Database=Tongou;User Id=<RO_USER>;Password=<SECRET>;Encrypt=True;TrustServerCertificate=True;"
  "ConnectionStrings__SzaipaAdmin"            = "Server=<SQL_HOST>,1433;Database=Szaipa_LocalCopy;User Id=<ADMIN_USER>;Password=<SECRET>;Encrypt=True;TrustServerCertificate=True;"
  "ConnectionStrings__TongouAdmin"            = "Server=<SQL_HOST>,1433;Database=Tongou_LocalCopy;User Id=<ADMIN_USER>;Password=<SECRET>;Encrypt=True;TrustServerCertificate=True;"
  # 可选：后台调试账号
  "AdminWrite__DebugUserName"                 = "<DEBUG_USER>"
  "AdminWrite__DebugPassword"                 = "<DEBUG_PASS>"
}
foreach ($k in $vars.Keys) {
  & "$env:windir\system32\inetsrv\appcmd.exe" set config -section:system.applicationHost/applicationPools "/[name='$pool'].environmentVariables.[name='$k'].value:$($vars[$k])" /commit:apphost
}
iisreset /restart
```

- 环境变量名用 `__` 表示 JSON 层级（如 `LegacyData__Szaipa__ConnectionString` 对应 `LegacyData:Szaipa:ConnectionString`）。
- 设置后，发布目录里**不需要也不应存在** `appsettings.Production.json`（或其中只放非机密项）。`csproj` 已禁止把 `appsettings.Local.json` / `appsettings.Production.json` 拷入发布产物。
- 若个别项必须走文件，用 `appsettings.Production.json` 但设 NTFS ACL 仅应用池身份可读。

### 本地：user-secrets

在 `src/Szaipa.Web/` 下（`UserSecretsId=szaipa-web-2026`）：

```bash
dotnet user-secrets set "LegacyData:Szaipa:ConnectionString" "Server=...;Database=Szaipa;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True;"
dotnet user-secrets set "LegacyData:Tongou:ConnectionString" "Server=...;Database=Tongou;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:SzaipaAdmin" "Server=...;Database=Szaipa_LocalCopy;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True;"
dotnet user-secrets list   # 查看（勿外发）
```

- 存于 `~/.microsoft/usersecrets/szaipa-web-2026/secrets.json`（macOS）/ `%APPDATA%\Microsoft\UserSecrets\szaipa-web-2026\secrets.json`（Windows），**不在仓库**。
- `appsettings.Local.json`（gitignored）仍是合法回退通道，但 user-secrets 更彻底（不产生任何仓库内文件）。

## legacy（szaipa2022/，将弃用）

- `Web.config`：`<connectionStrings configSource="connectionStrings.config" />`——**不存任何凭据**。
- `connectionStrings.config`：真实连接串，**gitignored**（勿提交）。
- `connectionStrings.config.example`：占位示例，随仓库提交。

## 验收清单

- [ ] `git status` 无含密码/主机名的跟踪文件
- [ ] 生产发布目录无 `appsettings.Local.json` / `appsettings.Production.json`
- [ ] IIS 应用池环境变量已设置、`iisreset` 后站点正常连接 DB
- [ ] 误提交含凭据文件时立即 `git rm --cached` + 改密码
