# 公众号接口配置页（T9）：后台录入 + 加密保存 + 测试连接

## 改动

- 新增后台页 `/Staff/WeChat/Settings`（「内容管理 → 公众号接口配置」，并与 `/Staff/WeChat` 用「同步记录 / 接口配置」选项卡互通），`StaffPolicy` + 全部 POST 防伪：
  - **接入指引**：在 mp.weixin.qq.com「设置与开发 → 基本配置 / 开发接口管理」取 AppID/AppSecret、重置 AppSecret 的风险（旧密钥立即作废）、IP 白名单填**服务器出口公网 IP**、填到本页 → 测试 → 再开启自动同步；未认证号以链接导入为主；群发回调默认关闭及其影响（自动回复/自定义菜单失效）。内容来自 `docs/wechat-setup-guide.md`。
  - **表单**：AppID（明文）、AppSecret（**只写**密码框，永不回显；已保存显示「已设置，末 4 位 ****xxxx」，留空 = 不修改，另有「清除已保存的 AppSecret」）、启用自动同步、两个来源开关、同步间隔（5～1440 分钟）、旧新闻分界 Id（留空 = 沿用配置文件，0 = 不隐藏）。另有「清空本页全部设置」（删除存储文件，恢复配置文件）。
  - **测试连接**：用当前**已保存**的有效配置运行 `WeChatProbe`（token → freepublish → 素材数量 → 永久图文素材），逐项中文显示；微信返回 40164 时从 errmsg 解析调用方 IP，醒目提示「请把 x.x.x.x 加入公众号 IP 白名单」。不回显密钥 / access_token。
  - 页面提示「本页仅管理员使用」。存储位置不可用时显示原因、表单禁用。
- 新增 `src/Szaipa.Data/Services/WeChat/WeChatCredentialStore.cs`：
  - `WeChatCredentialStoreLocation`：配置项 `WeChat:CredentialStorePath`。Development 未配置 → `{ContentRoot}/App_Data/secure/wechat-credentials.json`（App_Data 已 gitignore）；其他环境必须是**发布目录之外的绝对路径**，未配置 / 相对 / 位于发布目录内 → 不可用（fail-closed：页面不可保存，站点照常，配置只来自 appsettings/环境变量）。
  - `WeChatCredentialStore`：JSON 文件；AppSecret 用 Data Protection `CreateProtector("Szaipa.WeChat.Credentials.v1")` 加密后只存密文；写入走「同目录临时文件 → `File.Move` 覆盖」原子替换，非 Windows 下文件权限 600。文件损坏 → 忽略（页面提示，重新保存即修复）；密文解不开（密钥目录丢失/换了） → 按「未设置」处理并提示重新录入，不抛异常。
  - 快照类 `WeChatStoredSettings` / 输入类 `WeChatSettingsUpdate` 刻意不用 record（record 的 ToString 会打印密钥）。
  - 校验 `WeChatSettingsValidator`：AppID `^wx[0-9A-Za-z]{16}$`，AppSecret `^[0-9A-Fa-f]{32}$`，间隔 5～1440，分界 Id ≥ 0；开启自动同步时必须有 AppID 与 AppSecret。
- **有效配置与无需重启生效**：`AddWeChatEffectiveOptions`（站点 `AddSzaipaData` 与 `wechat-probe` 共用）= 绑定 `WeChat` 配置段 → `IPostConfigureOptions` 用存储值覆盖（存储里没有的项沿用配置）→ `IOptionsChangeTokenSource` 在保存后触发，`IOptionsMonitor<WeChatOptions>` 立即重算。以下读取点改为 `IOptionsMonitor<WeChatOptions>.CurrentValue`：
  - `WeChatApiClient`（每次调用读最新值；按「接口地址 + AppId + AppSecret」的 SHA-256 指纹缓存 token，凭据一变旧 token 立即作废）；
  - `WeChatSyncService`（scoped，每请求取当前值）；
  - `WeChatSyncBackgroundService`（重写主循环：未启用时挂起等待配置变化；从关到开、或凭据变化时立即跑一轮并重新全量发现；只改间隔时按新间隔排下一轮；被关掉后不再同步）；
  - `NewsReadRepository`（旧新闻分界 Id）、`WeChatController`（同步记录页状态）；
  - `wechat-probe` 命令：移到 Data Protection 配置解析之后，用与站点相同的 Data Protection（`configureDataProtection` 委托，站点与命令共用）解密存储，输出同样不含密钥。
  - 未改：`WeChatImageLocalizer`、`WeChatCallback*`（只读图片白名单/回调 Token，不在存储范围内，仍用 `IOptions`）。
- 审计：保存/清除/清空经 `IOperationRecorder` 记「更新了公众号接口配置（AppID ****xxxx[，已更换 AppSecret]）」等，只含 AppID 末 4 位；后台写库未配置时只写日志，不影响保存。日志里同样只有 AppID 末 4 位。
- `appsettings.Production.example.json` 增加 `WeChat:CredentialStorePath` 占位。

## 顺手修复的旧 bug

- **官方接口 token 永远取不到（HTTP 412）**：`WeChatApiClient` 用 `JsonContent.Create` 发 POST，请求体是 chunked 编码、没有 Content-Length；微信 `api.weixin.qq.com` 对 chunked 请求直接返回 HTTP 412。本地用格式合法的假凭据实测：同一 JSON 定长发送 → `errcode 40013`，chunked → `412`。改为 `StringContent`（带 Content-Length），并加单测锁定。修复前即使凭据、白名单都正确，`stable_token` 也会失败。
- 40164 提示原先丢掉了微信报出的 IP，现在解析并展示。

## 验证

- `~/.dotnet/dotnet build Szaipa.Modernization.slnx`：0 error（唯一告警为既有的 `ImageVariants.cs` CS0618，与本任务无关）。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx`：Data 423 + Web 288 = **711 全绿**（基线 673，新增 38）。新增测试：
  - `WeChatCredentialStoreTests`：存储位置（Development 默认、Production 未配置/相对/发布目录内 fail-closed）；保存后文件不含明文、换实例读取解密一致、文件权限 600、无残留临时文件；换 key ring 无法解密 → 安全降级为未设置；留空不改/更换/清除/清空；损坏文件；覆盖优先级（存储覆盖配置、未存项沿用配置、0 覆盖分界 Id、清空后恢复）；保存后 `IOptionsMonitor` 立即变化且 OnChange 触发；凭据变化后 token 重新获取、只改开关不重取；40164 IP 解析（IPv4/IPv6/无 IP）；探测结果带出 IP 且不含密钥；表单校验；遮罩。
  - `WeChatSyncBackgroundServiceTests`：未启用不同步 → 设置页开启后无需重启立即首轮全量 → 换凭据再次全量 → 关闭后不再同步。
  - `WeChatApiClientTests`：POST 请求必须带 Content-Length。
  - `WeChatSettingsControllerTests`（Web）：非法输入返回中文错误且 AppSecret 不回显、不进 ModelState；开启同步需凭据；Production 未配置路径不可保存/清除；合法保存立即生效、页面只给末 4 位、清除后接口通道停用。
- 本地实跑（LocalMirror，worktree 内的镜像库副本，`http://127.0.0.1:5091`，debug/debug）：
  - 未登录访问设置页 → 302 登录；不带防伪令牌 POST → 400。
  - 非法 AppID/AppSecret/间隔 → 三条中文错误，页面不含提交的密钥，不生成文件。
  - 保存格式合法的**假** AppID/AppSecret → 页面「已设置，末 4 位 ****xxxx」，页面源码与存储文件里都搜不到明文；文件权限 `-rw-------`，`appSecretProtected` 为 `CfDJ8…` 密文。
  - 测试连接 → 「access_token：AppId 或 AppSecret 无效（errcode 40013）」，其余步骤跳过；页面不含密钥/token。（微信先校验 AppID，假 AppID 拿不到 40164；40164 + IP 提示由单测覆盖。修 412 之前这里显示的是「微信接口返回 HTTP 412」。）
  - 只勾「启用自动同步」保存（AppSecret 留空）→ 不重启，后台服务被唤醒，30 秒预热后跑首轮全量，日志 `errcode 40013`（日志中无密钥）。
  - 重启站点后配置仍在（末 4 位一致）；`wechat-probe` 命令行同样读到后台保存的配置并解密（输出 40013，不含密钥）。
  - 清除 AppSecret → 文件中 `appSecretProtected` 为 null；清空全部 → 文件删除，页面恢复「从未保存」。操作记录（镜像库 Diary）只有 AppID 末 4 位。
  - 1440 / 390 宽度查看：无横向溢出；窄屏时接入指引排在表单前。
  - 验证结束已删除本地存储文件、停止站点。全程未使用任何真实凭据。

## 部署要点（需要用户/运维手动做）

1. 服务器配置 `WeChat:CredentialStorePath`（`appsettings.Production.json` 或环境变量 `WeChat__CredentialStorePath`），**必须在发布目录之外**，例如：
   `C:\SzaipaData\secure\wechat-credentials.json`
2. 给应用池账号该目录修改权限（目录不存在时站点会在首次保存时创建，所以给上级目录权限也可）：
   ```
   icacls "C:\SzaipaData\secure" /grant "IIS AppPool\SzaipaNew:(OI)(CI)M"
   ```
   建议同时去掉 Users 组对该目录的读权限，仅保留 Administrators / SYSTEM / 应用池账号。
3. `DataProtection:KeysPath` 的密钥目录（已是 Production 必填）**不能丢失或更换**：AppSecret 用它加密。丢了之后页面会提示「已保存的 AppSecret 无法解密」，需重新录入（其他已保存项不受影响）。迁移服务器时连同密钥目录一起迁移。
4. 未配置存储位置时页面显示「未配置存储位置」且不能保存，站点其他功能与配置文件里的 `WeChat` 段照常生效（fail-closed）。
5. 之后按页面「接入指引」：取 AppID/AppSecret → 白名单加服务器出口 IP → 保存 → 测试连接 → 开启自动同步。

## 遗留

- 后台目前只有一个 `StaffPolicy`（登录即可），没有「管理员」角色区分；页面仅以文字提示「本页仅管理员使用」。如需限制到特定账号，需要另做角色/权限。
- 「测试连接」结果以 POST 后直接渲染返回（与链接导入一致），刷新浏览器会提示重新提交。
- 存储为单机文件；多实例部署需共享路径与同一套 Data Protection 密钥（当前为单站点 IIS，不涉及）。
