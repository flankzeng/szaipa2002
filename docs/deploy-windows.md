# Windows 并行验证与原子发布契约

> 当前服务器和旧发布版仍只供参考。本文件描述未来发布方法；本次文档/代码变更没有连接或修改
> Windows、IIS、hook、数据库或生产 Content。

新版是 ASP.NET Core net10.0。正确的发布单元是不可变应用 artifact，约 1.2 GiB 的历史 Content、
数据库、Production 配置和 Data Protection key 都是外部运行契约，不属于 Git artifact。

## 目录模型

推荐最终形态：

```text
C:\inetpub\szaipa-modern\
  releases\
    <commit-sha>\          # 每次解压一个新、只读的应用 artifact
    <previous-sha>\        # 至少保留上一版用于回滚

C:\ProgramData\Szaipa\
  DataProtection-Keys\     # 跨 release 持久化，IIS 身份仅此处需要 Modify；内容由本机 DPAPI 加密

<现有共享目录>\Content\   # 约 1.2 GiB，部署脚本永不复制/清理/删除
```

现有 Content 可以继续使用当前物理目录；是否迁到中立共享路径是独立服务器变更，必须先备份、
验证并取得用户明确批准。新版通过 `LegacyAssets:ContentRoot` 将它继续映射为 `/Content`。

## 1. 构建不可变 artifact

在仓库根执行：

```bash
npm --prefix src/Szaipa.Web ci
npm --prefix src/Szaipa.Web run build
~/.dotnet/dotnet build Szaipa.Modernization.slnx --disable-build-servers -m:1
~/.dotnet/dotnet test Szaipa.Modernization.slnx --no-build --disable-build-servers -m:1
~/.dotnet/dotnet publish src/Szaipa.Web/Szaipa.Web.csproj -c Release -o ./artifacts/publish
```

正式新仓库应由 CI 固定 Node 与 `global.json` 中的 .NET SDK，生成带 commit SHA 的 zip、文件清单和
SHA-256；服务器 hook 只消费已验证 artifact，不在活动 IIS 目录中 `git pull` 或编译。

产物必须包含 `Szaipa.Web.dll`、IIS `web.config`、已编译 admin 资源、字体子集和现代自有资源；
不得包含：

- `appsettings.Local*`、`appsettings.Production*` 或任何凭据；
- `App_Data` / Data Protection key；
- 外置 `Content`；
- `package*.json`、`node_modules`、`admin.input.css`、`admin/src`；
- 冗余 `.br` / `.gz` 发布副本。

## 2. Windows 前置条件

- 安装与 net10.0 匹配的 ASP.NET Core Hosting Bundle，确保 IIS 有 `AspNetCoreModuleV2`。
- 测试站和正式站应用池使用 **No Managed Code**。
- 应用池身份：
  - release 目录只读；
  - 共享 Content 默认只读；
  - 外部 `DataProtection-Keys` 目录可读写；
  - 只有用户选择新 `/Staff` 为唯一写入者后，才另行授予 Content 写权限和后台数据库凭据。

若服务器只下载 CI artifact，不需要安装 Node/npm，也不应在 IIS 目录运行前端构建。

## 3. Production 配置

安全字段模板见 `src/Szaipa.Web/appsettings.Production.example.json`。模板和真实配置都被排除出
publish；真实密码不得进入 Git。

优先通过 IIS/app-pool 环境变量提供。以下 `ApplicationName`/`KeysPath` 是未来正式站值：

```text
RuntimeSafety__UseLegacyDataSources=true
RuntimeSafety__AllowLiveDatabase=false
LegacyData__Szaipa__AccessMode=ReadOnly
LegacyData__Tongou__AccessMode=ReadOnly
LegacyAssets__ContentRoot=<现有共享 Content 的绝对路径>
DataProtection__ApplicationName=Szaipa.Web
DataProtection__KeysPath=C:\ProgramData\Szaipa\DataProtection-Keys
AdminWrite__EnableWrites=false
AdminWrite__UseReadOnlyConnectionForDebug=false
TongouAdminWrite__EnableWrites=false
TongouAdminWrite__UseReadOnlyConnectionForDebug=false
```

并行测试站不得与正式站共享 cookie key ring。测试池改用独立值，例如
`ASPNETCORE_ENVIRONMENT=Staging`、`DOTNET_ENVIRONMENT=Staging`、
`DataProtection__ApplicationName=Szaipa.Web.Test` 与
`DataProtection__KeysPath=C:\ProgramData\Szaipa\Test\DataProtection-Keys`；只在同一个测试池从 release A
切到 release B 时验证 cookie 保持。程序会给 Staging 使用独立 cookie 名 `Szaipa.Admin.Staging`，
但 cookie 不按端口隔离，因此测试站仍必须使用独立 hostname。正式切换才配置 `Szaipa.Web` 和正式
key 目录。

只读连接字符串可走现有 `SZAIPA_READONLY_CONNECTION` / `TONGOU_READONLY_CONNECTION` 或等价的
配置键。只能使用最小权限只读账号，不能复制旧 Web.config 的生产管理凭据。

如果运维必须使用 `appsettings.Production.json`，应由服务器受保护的 master 在新 release 激活前
复制进去并收紧 ACL；artifact 和 Git 中仍不得有该文件。部署不能覆盖或删除 master。

### Data Protection 启动门槛

非 Development 环境现在会在启动前检查：

- `KeysPath` 必须存在配置；
- 必须是绝对路径；
- 必须位于当前 release 根之外；
- Production 的 `ApplicationName` 必须精确保持 `Szaipa.Web`；每个非 Production 环境使用自己稳定、
  且与 Production 不同的名称（测试站为 `Szaipa.Web.Test`）；
- Production 必须运行在 Windows；key ring 使用 machine-scoped DPAPI 静态加密；
- 启动时必须完成一次 protect/unprotect，自检写权限、key ring 与 DPAPI 上下文。

缺少契约时应用按设计启动失败，防止上线后每次换 release 都让 `/Staff` 登录 cookie 失效。
machine-scoped DPAPI 适合当前单台 Windows 服务器；DPAPI 解密不绑定 app-pool 身份，但目录 ACL
仍必须在身份变更时更新。密文绑定本机，
不能把 key 目录复制到另一台机器就当作可用灾备。跨机器恢复/迁移须另行批准证书加密方案。
首次并行部署使用全新空 key 目录；DPAPI 只保护后续写入的 key，不会自动重写既有明文 key ring。

## 4. 创建并行 IIS 测试站

1. 把 artifact 校验并解压到新的 `releases\<commit-sha>`，不要覆盖现有 release。
2. 在 IIS 建独立站点/应用池，如 `szaipa-modern-test`，绑定独立内部测试 hostname 和有效 HTTPS；
   可同时使用测试端口，但不能只靠端口隔离 cookie。不要碰公开 80/443 binding。
3. physical path 指向新 release；配置上述环境变量和 NTFS 权限。
4. 启动后先检查 `/healthz`，再做真实只读路由冒烟。

`/healthz` 只说明应用与安全配置已启动，不代表数据库、Content 或页面都可用。

## 5. 必做冒烟

- `/healthz`：200，`liveDatabase=false`、无 ReadWrite legacy source；
- `/Home/newIndex`、`/Home/NewNews`、`/Home/NewNewsRead/1124`；
- `/Home/NewVip`、`/Home/NewArt/1000`、`/Home/NewAbout`；
- `/Home/PublicationList`、一个普通展览、一个重要展览和三个特殊展览；
- `/Content/123/favicon.ico`、代表性新闻/艺术家/展览图片与现代 AVIF；
- 全程通过测试 hostname 的 HTTPS 登录 `/Staff`，确认 cookie 名为 `Szaipa.Admin.Staging`，并在
  release A→B physical-path 切换后保持会话；
- 390px 手机和常用桌面宽度无横向溢出，navbar 单行横排；
- 已退役 URL 保持 410、兼容重定向保持预期状态码。

数据库结构还须以只读方式确认 `Publication` 新列、slug 固定 ID 与 `ExhibitionWork` 表已满足
`docs/sql/` 前置条件。hook 不执行 SQL、`dotnet ef database update` 或任何 Content 写入。

## 6. 原子切换与回滚

未来部署脚本应先提供 `ValidateOnly`：校验 SHA-256、DLL/web.config、禁止文件、外部配置和路径，
不接触 IIS。真实切换时：

1. 记录测试站/正式站当前 physical path；
2. 将 physical path 指向新 release 并受控回收应用池；
3. 运行上述健康检查和路由冒烟；
4. 任一失败，立即恢复旧 physical path 并再次回收；
5. 保留上一 release，不自动清理旧站、Content、keys 或数据库。

IIS in-process 会锁定活动 DLL，因此不允许对活动目录直接 publish 覆盖。最终公开切换应只改
binding/reverse proxy 或已验证站点的 physical path，且必须在用户明确授权后进行。

## 7. 新仓库 hook 的职责

hook 不是“收到 push 后执行任意 shell”的入口。它应：

- 校验 webhook 签名和受保护 release 分支/tag；
- 获取 CI 已构建 artifact，而不是克隆完整历史；
- 校验 commit SHA、artifact SHA-256 和内容清单；
- 调用原子部署脚本，失败自动回滚；
- 使用最小权限服务账号；
- 不改数据库、不改 Content、不切后台写权限。

创建新 remote、配置 workflow/hook 和测试 IIS 站之前，必须先确认仓库名称/可见性、Windows
releases 路径与测试绑定。

## 8. 后台唯一写入者（人工确认）

并行验证阶段默认新版公开站只读、`/Staff` 写入关闭。正式切换前必须由用户选择：

- **旧后台继续写**：旧后台保留内部入口和唯一写权限，新 `/Staff` 只读/关闭；或
- **新 `/Staff` 接管**：先停用旧后台写入，再单独验证登录、anti-forgery、全部 CRUD、上传、画廊
  排序、作品目录和操作记录，最后才授予新应用后台 DB 与 Content 写权限。

两套后台不得未经确认同时写共享数据库和 Content。
