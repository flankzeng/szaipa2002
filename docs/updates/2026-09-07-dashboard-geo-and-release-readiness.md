# 更新 — 2026-09-07：访客分布图表改版 + 发布版就绪核查

## 1. 访客分布：旭日图 → 横向柱状图 + 钻取

旭日图在省市两层、分类多时标签互相挤压，实际看不全。改为：

- 横向柱状图，按访问量降序，只展示前 10 名，其余合并为「其他」；
- 点击某个省份钻取到该省城市分布，卡片右上角出现「← 返回全国」回到全国视图；
- ECharts 模块导入由 `SunburstChart` 换成 `BarChart`（`LineChart`/`PieChart` 不变），
  打包体积不增加。

对应文件：`wwwroot/admin/src/dashboard.js`（源）、`wwwroot/admin/dashboard.js`（esbuild 产物）、
`Areas/Staff/Views/Dashboard/Index.cshtml`。

## 2. 省市来源改为按 IP 实时解析（qqwry）

legacy `AccessData` 的 `Porvince` / `Ctiy` 列在百度接口失效后基本是空值或异常值，直接聚合没有意义。
新增只读定位器：

- `Szaipa.Data/Services/IpGeolocation/`：`IQqwryIpLocator` / `QqwryIpLocator` / `IpRegion`；
- 首次查询才加载 25MB 的 `qqwry.dat`（不在启动路径上），文件缺失时 `Lookup` 返回 `null`，
  图表降级显示既有字段或「未知」，**不报错、不影响其他面板**；
- 路径解析：`IpGeolocation:QqwryPath` → `<contentRoot>/App_Data/security/qqwry.dat`。
  `Program.cs` 用已知的 contentRoot 覆盖 `AddSzaipaData` 中基于 cwd 的注册（cwd 在 IIS 下不可靠）。

`qqwry.dat` 属于外部运行契约（同 Content / Data Protection key），**不进仓库、不进发布包**，
部署时由运维放到发布根的 `App_Data/security/qqwry.dat` 或用配置显式指定。

## 3. 操作记录 feed：按条数而非天数

`GetRecentOperationsAsync` 的参数由「最近 N 天」改为「最近 N 条」（忙的一天就有几十条，
按天取会忽多忽少），仍按天分组、日期倒序、日内按时间正序。

## 4. 开发凭据改用 dotnet user-secrets

`Development` 环境额外加载 user-secrets（`UserSecretsId=szaipa-web-2026`），优先级
`appsettings < user-secrets < 环境变量 < 命令行`。本地凭据落在用户目录，仓库和部署目录都不出现明文。

## 5. CSS 分层合并的收尾（发现于本次核查）

2026-08-04 的分层合并已删除 Bootstrap 选择开关和 `public-bootstrap-baseline.css`，但：

- 9 个公开视图仍残留失效的 `ViewData["UseBootstrapCss"] = false;`（布局已不再读取）→ 删除；
- `PublicBootstrapOptOutContractTests` 断言的是已退役的旧契约，**该测试自 59e21be 起一直是红的** →
  换成 `PublicLegacyCssDecouplingContractTests`，锁定新契约：
  1. `Views/` 下任何 `.cshtml` 都不得再引用 legacy 的 `bootstrap.css` / `Site.css` /
     `responsive.css` / `responsive/Artist.css`，也不得残留 `UseBootstrapCss`；
  2. 两个公开布局按 `public-core.css` → `font-subsets-public.css` → `public-layout.css` 顺序加载，
     文件必须真实存在，且都在 `RenderSectionAsync("Styles")` 之前。

## 6. 发布版核查结果

| 项目 | 结果 |
|---|---|
| Release 构建 | 0 error（4 个 NU1900 为离线取漏洞库的告警，与代码无关） |
| 测试 | Data 102 + Web 243 = **345 全绿**（此前 Web 有 1 红） |
| `dotnet publish -c Release -o ./artifacts/publish` | 成功，产物 42MB |
| 产物排除契约（见 `docs/deploy-windows.md`） | 通过：无 `appsettings.Local*` / `appsettings.Production*`、无 `App_Data`、无 `package*.json` / `node_modules` / `admin.input.css` / `admin/src`、无 `.br` / `.gz` 副本 |
| 产物必需项 | `Szaipa.Web.dll`、`web.config`、已编译 `admin.css` / `dashboard.js`、字体子集齐全 |

另清理了一次本地调试留下的空目录 `src/Szaipa.Web/src/Szaipa.Web/SecuritySampleData/whitelist_edit_queue`
（相对路径按 cwd 解析产生，仅 Development 回退路径会走到，生产要求绝对路径且 fail-closed）。

## 部署前仍需人工确认

1. 服务器放置 `qqwry.dat`（否则访客分布图为空/降级，其余功能不受影响）；
2. `C:\Security` 的目录与 NTFS 权限、`SzaipaSecuritySnapshot` 计划任务（见 `docs/security-dashboard.md`）；
3. 数据库可写副本连接串与 `AdminWrite:EnableWrites`（agent 从不写库）；
4. 服务器仍只供只读参考，本次未部署、未改 IIS、未动生产 Content。
