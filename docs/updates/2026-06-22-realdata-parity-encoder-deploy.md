# 更新 — 2026-06-22：接通生产只读库、真实数据校验、CJK 编码修复、发布与调试配置

本次会话工作范围（属于把 `src/` 下 ASP.NET Core 只读迁移项目首次纳入版本库的那次基础提交）。
对照基线是本地 `web24.05` 发布产物；`www.szaipa.com` 仅作辅助的真实数据参照（它读的是同一个生产库）。

## 1. 接通生产只读数据库
- 新应用以**只读**方式连上**生产** SQL Server。最小权限 `db_datareader` 账号 `szaipa_ro` / `tongou_ro`
  （服务器 `120.78.90.25,1433`，同一实例承载 `Szaipa`、`Tongou`）。
- 凭据只存在于被 git 忽略的 `src/Szaipa.Web/appsettings.Local.json`（`AccessMode: ReadOnly`、
  `AllowLiveDatabase: false`、两个读模型开关开启）。绝不入库，也不进发布产物。
- 在生产库上重新核验写安全：两个读上下文的 `SaveChanges` 全部硬抛异常，所有仓储用 `AsNoTracking`，
  无任何写路径；`ReadCount/HotCount/VisityCount` 仅为只读列映射。`/healthz` 显示
  `liveDatabase:false`、`hasReadWriteLegacySource:false`。

## 2. 真实数据一致性校验（NEW vs LIVE vs web24.05 源，共 9 个维度）
通过真实数据发现并修复 2 个真回归（skeleton 模式曾掩盖它们）：

1. **缺失的 `PublicationController`** —— 19 个命名展览静态页（`/Publication/chunyu`、`/Publication/man`、
   `shuimai`、`tonggou2024` …，走默认约定路由，全部 `return View()`、不连库）从未迁移，导致首页上 18 个
   链接 404。**已补齐**控制器 + 19 个视图（`~/`→`/`、`../Content`→`/Content`；均用 `_newLayout` 或
   `Layout=null`，零旧 chrome 依赖）。现在 19 个全部返回 200。
2. **中文被渲染成 HTML 数字实体** —— ASP.NET Core 默认 `HtmlEncoder` 会把所有非 ASCII 字符编码成
   `&#x5F20;…`，导致来自数据库的中文体积膨胀约 30%，且与旧站的原始 UTF-8 不一致（newnews 一页有 2477 处
   实体）。**已修复** `Program.cs`：`services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(UnicodeRanges.All))`。
   全站生效，实体数归零。

其余全部判定为忠实或按范围对齐：新闻列表/详情、首页、艺术家、Tongou、出版页、资源——渲染内容与 live
一致；移植视图里 CSS 注释/空白的精简属无害（CSS 规则块与 web24.05 一一对应）。`_newLayout` 页脚有意去掉
`旧版首页`（旧 Home/Index）和 `后台登陆`（`/Staff` 后台）——与"旧链暂缓 + 不做后台"的既定约束一致。
4 个资源引用在 NEW 与 LIVE 上都 404（旧站本就坏的死链，忠实复刻）。

## 3. 发布与跨平台相关改动
目标已明确：**在 Mac 本地开发/调试 → 发布部署到 Windows 服务器**（不是"在 Windows 上调试"）。
- `appsettings.Development.json`：移除写死的 Mac 绝对路径 `LegacyAssets:ContentRoot`（之前入库了，会让
  Windows 上无法提供静态资源）。机器相关的路径现在放在被忽略的 `appsettings.Local.json`；
  `appsettings.Local.example.json` 记录了 Mac/Windows 两种写法。
- `Program.cs`：配置加载改为读取 `appsettings.{Environment}.json`（原先写死 `Development`），这样服务器的
  `appsettings.Production.json` 才会被加载；未设环境变量时默认 `Production`。
- `Szaipa.Web.csproj`：将 `appsettings.Local.json` 设为 `CopyToOutputDirectory/CopyToPublishDirectory=Never`
  ——它之前会被拷进发布产物，**泄露生产密码**。已确认从产物中去除。
- `docs/deploy-windows.md`：按用户的 git-pull 流程写的发布指南（发布到 `web24.05`（Gitee 部署仓库）的 `core/`
  子目录 → push → 服务器 `git pull`；IIS 进程内托管，先用测试端口并存；一次性安装 Hosting Bundle + 应用池
  设"无托管代码"）。该任务暂缓，已登记为待办。

## 4. VS Code 本地调试（Mac）
F5 此前失败，原因是 `.vscode/settings.json` 指向了旧的 Windows-only `szaipa2022.sln`、`launch.json` 没有任何
配置，且 C# 扩展的 BuildHost 用的是只有 6/7.x 的系统 dotnet。已修复 `.vscode/`：
- `settings.json`：`dotnet.defaultSolution` → `Szaipa.Modernization.slnx`；通过
  `dotnetAcquisitionExtension.existingDotnetPath` 把 C# 扩展钉到 `~/.dotnet`（真正的 .NET 10 SDK）。
- `launch.json` + `tasks.json`：F5 用 `~/.dotnet/dotnet` 构建并启动 `src/Szaipa.Web`，连接生产只读库，
  监听 `http://localhost:5057` 并自动打开 `/Home/newIndex`。

## 校验
构建 0 警告 / 0 错误；测试 28/28 通过；发布产物在 `ASPNETCORE_ENVIRONMENT=Production` 下可独立运行、
所有页面 200；产物中不含任何生产凭据。

## 环境说明
- 使用 `~/.dotnet/dotnet`（SDK 10.0.301，运行时 10.0.9）——PATH 上的 `dotnet` 只有 6/7.x。
- 本地调试：`~/.dotnet/dotnet run --project src/Szaipa.Web` 或 VS Code 按 F5。
