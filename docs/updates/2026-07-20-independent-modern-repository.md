# 独立现代仓库与持久登录密钥契约（2026-07-20）

## 为什么拆仓库

当前分支的现代源码已经不引用旧 MVC5 项目。体积核算：

- 现代跟踪边界在加入本批资源后约 20.4 MiB；
- 旧 `szaipa2022` 跟踪文件约 1,139.77 MiB，其中旧 Content 约 1,082.99 MiB；
- 本机稳定参考版 `web24.05/Content` 约 1.2 GiB；
- 当前仓库历史 pack 约 1.8 GiB。

因此不能只是把当前分支原样推到一个新 remote，否则旧大文件历史仍会跟过去。脱敏历史审计还
发现早期现代文档/示例配置中有不能证明无效的短示例凭据（与已知旧生产凭据不重合、非高熵）；
当前文件已改为明确占位符。新仓库须从干净 commit 导出白名单快照并建立单一全新 root commit，
记录原始 SHA；不携带旧 blob，也不在用户正在工作的仓库中改写历史。完整旧历史继续由现有远端
和已推送的 `legacy/archive-before-frontend-prune-20260710` 保存。

这里的 20.4 MiB 只是可替换应用，不是完整网站总占用。约 1.2 GiB 图片、预览、图标、字体和
旧共享脚本仍保存在服务器外部 Content 目录，现代程序继续把它映射为 `/Content`。拆仓库不会
删除、复制或重编码这套内容。

完整路径白名单、外部 Content/双数据库/后台单写者、CI artifact、Windows 原子切换和回滚
门槛见 `docs/repository-split.md`；根 `README.md` 作为未来独立仓库入口。

## Data Protection

原实现把后台 cookie 的 Data Protection key 放在应用 release 根下。换 release 目录会产生新
key ring，从而让已登录 `/Staff` 会话失效，也要求 IIS 对每个 release 有写权限。

现在新增：

- `PersistentDataProtectionOptions`：固定默认 `ApplicationName=Szaipa.Web`；
- `DataProtectionKeyPathResolver`：Development 未配时仍回退被忽略的项目
  `App_Data/DataProtection-Keys`；
- 非 Development 环境的 `KeysPath` 缺失、相对或位于当前 release 根内时启动前失败；
- 合法外部绝对路径交给 `PersistKeysToFileSystem`，并用 `SetApplicationName` 隔离应用；
- Windows Production 使用 machine-scoped DPAPI 加密持久 key，并在启动阶段执行一次
  protect/unprotect 自检；非 Windows 的非 Development 部署会明确失败；
- 命令行与 `ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT` 先收敛成同一个环境名；两个环境变量
  冲突时失败，避免 host 与 Data Protection 安全策略各自判断成不同环境；
- `appsettings.Production.example.json` 只描述无密码安全模板，真实 Production/Local 配置和 key
  均不进入 Git 或 publish artifact；
- 生产模板显式保持公共数据源只读，两个后台写入口与 debug login 默认关闭。

测试使用两个不同 release root、同一外部 key ring 和同一 application name 真实 protect/unprotect；
也确认不同 application name 无法解密。本批复审还补齐派生图片 Windows ADS、中间 reparse 目录和
非法 UTF-16 的安全回退。最终仍须在 side-by-side IIS 测试站验证真实 Staff cookie
跨 release 切换不失效；测试站必须使用独立的 `Szaipa.Web.Test` application name/key 目录，不能
共享正式 Staff cookie ring。测试站还必须使用独立 hostname 和 HTTPS；程序为 Staging 自动使用
`Szaipa.Admin.Staging`，避免与正式 `Szaipa.Admin` 互相覆盖（仅换端口不能隔离 cookie）。

## 当前没有执行的外部动作

本批没有创建远端仓库、GitHub workflow、发布 artifact、服务器目录或 webhook；没有修改 IIS、
数据库、旧发布版或生产 Content。真正创建新仓库/切换 hook 前仍需用户确认：

1. 新仓库名称、所属账号/组织和可见性；
2. 旧后台继续作为唯一写入者，还是新 `/Staff` 正式接管；
3. Windows 测试站的 releases 根、外部 key 目录、共享 Content 路径和健康检查绑定。

## 验证

- build：0 warning / 0 error；
- Data 96 + Web 120 = 216/216；
- Release publish 成功，Local/Production example、真实 Local 配置和 App_Data 均未进入产物；
- Production 缺 key 配置实测按预期失败；非 Windows Production 即使给出外部绝对路径也会在
  DPAPI 平台门槛明确失败。跨 release key 复用由 provider 契约测试覆盖，真实 Windows DPAPI 与
  Staff cookie 连续性留给独立 Staging IIS 站验证。
