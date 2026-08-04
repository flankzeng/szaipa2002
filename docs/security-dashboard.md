# 安全监控 Dashboard（Staff 后台）

安全监控页嵌入 Staff 后台：`/Staff/SecurityMonitoring`（登录 staff 后顶部导航"安全监控"）。它把三类数据汇到一个页面：

1. **服务器登录尝试 / 异常访问** —— 读 `C:\Security` 下 AutoBlock/Alert 产出的文件
   （`blocked_ips.csv`、`whitelist.txt`、`alert_errors.log`、`security_snapshot.json`）。
2. **网站异常流量** —— 应用内请求审计中间件逐请求落盘（`requests-YYYYMMDD.log`）。
3. **后台登录尝试** —— `AccountController.Login` 每次尝试落盘（`logins-YYYYMMDD.log`）。

操作（解封 IP / 白名单增删）通过**待办队列文件**交给服务器的 SYSTEM 计划任务执行，
Web 应用不持有任何提权权限。

---

## 权限模型（关键）

| 角色 | 能做什么 | 为什么 |
|---|---|---|
| IIS 应用池（Web 应用） | 只读 `C:\Security` 的 4 个文件；在 `unblock_queue/`、`whitelist_edit_queue/` 写请求文件；写自己的审计日志 | 无提权，即使被攻破也不能改防火墙规则 |
| SYSTEM 计划任务 `SzaipaSecuritySnapshot` | 读安全事件日志；跑 `netsh`；改 `blocked_ips.csv` / `whitelist.txt`；写 `security_snapshot.json`；消费队列 | 唯一有特权的参与者 |

队列文件一旦被任务处理，会变成 `<name>.done.txt`（内含 `processedAtUtc|ok|message`），
原 `.txt` 被删除。Dashboard 据此显示"待处理 / 已处理"及结果。

---

## 服务器部署（Windows Server，SYSTEM）

### 1. 拷贝脚本

把仓库 `scripts/security-snapshot.ps1` 拷到服务器 `C:\Security\security-snapshot.ps1`。
它读取安全日志并生成快照、消费队列。与已有的 `AutoBlock.ps1`（负责封禁）`Alert.ps1`（负责发信）
互不冲突，是新增的一环。

### 2. 注册计划任务（每 5 分钟，SYSTEM）

```powershell
schtasks /Create /TN "SzaipaSecuritySnapshot" `
  /TR "powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Security\security-snapshot.ps1" `
  /SC MINUTE /MO 5 /RU SYSTEM /RL HIGHEST
```

验证它真的在写快照：

```powershell
type C:\Security\security_snapshot.json
schtasks /Query /TN "SzaipaSecuritySnapshot" /V /FO LIST   # 上次结果应为 0
```

> 事件属性索引依赖 Windows 版本（4625 源 IP = `Properties[19]`，已在 Server 2019 上验证）。
> 换机器时先用 `Get-WinEvent -LogName Security -MaxEvents 1` 核对。

### 3. NTFS 权限（最小化）

| 路径 | 应用池需要 |
|---|---|
| `C:\Security`（含 4 个文件、`audit\`） | **读取** |
| `C:\Security\unblock_queue` | **修改**（写/删请求文件） |
| `C:\Security\whitelist_edit_queue` | **修改** |
| 其余 | 不需要 |

给 IIS 应用池身份（如 `IIS AppPool\szaipa-modern`）授 `Read` 于 `C:\Security`、
`Modify` 仅于两个 queue 子目录。**绝不要把 Modify 授给 4 个服务器文件本身。**

### 4. 配置 Production

`appsettings.Production.json`（或 IIS 环境变量）需含：

```json
"SecurityMonitoring": {
  "ServerSecurityRoot": "C:\\Security",
  "RequestAuditEnabled": true,
  "AuditDirectory": "C:\\Security\\audit",
  "AuditKeepDays": 30,
  "AuditMaxLineLength": 512,
  "AuditSkipPaths": [ "/healthz", "/favicon.ico" ]
}
```

`ServerSecurityRoot` 在非 Development 环境**必填**且必须是 release 目录外的绝对路径，
否则启动即失败（fail-closed）。`RequestAuditEnabled` 默认 false，生产需显式开启。

### 5. 冒烟

登录 staff → 顶部"安全监控" → 应看到 KPI 卡、攻击源图、请求量图、封禁/白名单表、
登录记录、操作队列、告警日志。

---

## 文件契约（Web 应用只读）

| 文件 | 格式 |
|---|---|
| `blocked_ips.csv` | 每行 `IP,unix秒`；容忍表头/空行/坏行 |
| `whitelist.txt` | 每行一个 IP；`#` 注释、空行跳过 |
| `alert_errors.log` | 自由文本，取尾部最近 N 行 |
| `security_snapshot.json` | 见下 |
| `unblock_queue\<ip>.txt` | `requestedAtUtc|requestedBy` |
| `unblock_queue\<ip>.done.txt` | `processedAtUtc|ok|message` |
| `whitelist_edit_queue\<guid>.txt` | `requestedAtUtc|requestedBy|add|ip` / `...|remove|ip` |
| `whitelist_edit_queue\<guid>.done.txt` | `processedAtUtc|ok|message` |

`security_snapshot.json`：

```json
{
  "schemaVersion": 1,
  "generatedAtUtc": "2026-08-04T05:00:00.0000000Z",
  "windowStartUtc": "2026-08-03T05:00:00.0000000Z",
  "failedLogons24h": 1234,
  "lockouts24h": 5,
  "bySourceIp": [ { "ip": "203.0.113.5", "failedLogons": 500, "users": ["administrator"], "lastSeenUtc": "..." } ],
  "lockoutsByUser": [ { "user": "administrator", "count": 3 } ],
  "lastAlert": { "atUtc": "...", "level": "Warning", "message": "..." }
}
```

审计日志（Web 应用写，`audit/` 下）：

- `requests-YYYYMMDD.log`：`ISO时间|客户端IP|方法|路径|状态码|耗时ms|UA`
- `logins-YYYYMMDD.log`：`ISO时间|用户名|客户端IP|1/0`
- 按日轮转，超过 `AuditKeepDays` 自动清理。

---

## 开发环境

`appsettings.Local.json` 保持 `ServerSecurityRoot` 为空 → 自动落到提交的 `SecuritySampleData/`
样例目录，Dashboard 本地即可渲染。请求审计写到 `App_Data/security/audit/`（gitignored）。

样例数据在 `src/Szaipa.Web/SecuritySampleData/`（发布时排除，不进生产产物）。

---

## 已知边界

- **解封后会再封**：AutoBlock 对 10 分钟内失败 ≥5 的 IP 会自动封禁。解封的是被误封的正常 IP
  不会复发；仍在攻击的 IP 会被重新封，属预期。想永久放行请在白名单操作里加它。
- **时间**：快照用 UTC（`...Z`）；审计日志用本地偏移 ISO。Dashboard 以浏览器本地时间显示。
- **超时/权限故障**：任一读失败都会降级为空数据并在页面上提示，不会 500。
- **IPv6**：封禁/白名单操作仅支持 IPv4（IPv6 含 `:`，不适合做文件名/防火墙规则）。
  快照里出现的 IPv6 仅展示。
