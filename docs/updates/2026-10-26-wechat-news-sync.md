# 微信公众号文章同步（T1）

## 改动

- 新增 `WeChatSyncRecord` 来源表与 SQL Server 建表脚本。文章正文仍写入现有 `News`，沿用原新闻列表、详情和后台编辑结构；公众号正文先经白名单清洗，图片本地化后才入库。
- 新增批量链接导入、freepublish 和永久图文素材两个独立 API 来源、stable_token 进程内缓存、后台定时同步、群发完成回调队列与 `wechat-probe` 能力探测。官方接口与回调均可独立关闭，密钥只从本机配置、user-secrets 或环境变量读取。
- 新增 `/Staff/WeChat` 导入记录页，支持批量导入、重新抓取、隐藏/取消隐藏、置顶/取消置顶。隐藏只影响列表/首页/推荐，详情链接仍按 ID 可访问；旧手录新闻在配置分界 ID 后退出列表，数据和详情不删。
- 修复清洗后图片清单仍含已删除二维码/推荐图的问题；修复链接批量输入把自然语言按空格拆成多个错误项的问题，并保留同一行多个有效 URL 的导入能力。
- 图片存储在文件移动失败时不再误报成功；失败或取消时清理临时文件。
- 文章页和图片下载命名客户端关闭自动 HTTP 重定向；图片本地化入口再次校验 scheme、白名单 host、userinfo 和默认端口，收到 3xx 不会跟随或重试。文章 URL 拒绝 userinfo 和非默认端口，清洗器将 `http:80` 升级为标准 HTTPS 端口。
- 定时 API 同步每次进程启动的首轮都完整发现来源历史；完整轮有接口错误或文章失败时，后续定时轮继续全量，直到所有启用来源成功且没有失败才转增量。全量通过已有 `biz + mid + idx` 来源键去重，不覆盖已存在文章。这里不保存跨重启的分页 checkpoint，因此每次重启会重新完整发现链接；这不是持久断点续跑。
- T1 worktree 合并 T0/T3 与依赖修复后，统一解决 DbContext、DI、Staff 导航冲突。

## 原因与旧问题

公众号文章占新闻来源的大多数。手工复制会重复录入，公众号图片热链也会失效。文章正文需要成为本站新闻正文，而不是嵌入公众号整页。同步记录以 `biz + mid + idx` 作为稳定去重键，兼容短链接和长链接。

早期 T1 工作区测试还有三个缺口：测试中的置顶 helper 是空操作；自然语言错误行被空格拆散；清洗器删除噪声后仍会下载其图片。现在都由 SQLite/清洗路径测试覆盖。

## 验证

- `~/.dotnet/dotnet restore Szaipa.Modernization.slnx` 成功。
- `~/.dotnet/dotnet build Szaipa.Modernization.slnx --no-restore --disable-build-servers -m:1`：0 warning、0 error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx --no-restore --disable-build-servers -m:1`：T1 补丁全量验证 Data 266 + Web 269，共 535 项全绿。
- SQLite 内存库测试覆盖字段映射、图片落盘映射、链接批量校验、重复/更新/再导入、隐藏、置顶、详情可访问与前台列表可见性；Web 测试覆盖临时 ContentRoot、路径穿越拒绝、图片存储 I/O 失败与取消清理、回调关闭状态和签名。
- BackgroundService 单测覆盖启动首轮全量、同步表缺失、来源错误/文章失败时保持全量并在后续周期重试、成功后切换增量。
- 现有 API 探测测试覆盖 token、freepublish/material 权限错误分流与敏感字段遮蔽；未使用真实 AppSecret 或 access_token。
- 仓库已有真实公众号文章 fixture，对应原文链接 `https://mp.weixin.qq.com/s/2omPr817EGqhsHa8HR3txw`。对此链接的只读 GET 返回 HTTP 200，但响应是微信“环境异常”验证页，不是文章正文；无法在当前网络下完成真实文章抓取。使用本地 HTML fixture 验证解析与导入，不将测试绑定到外网。
- 已在 SQLite 内存库完成导入→前台读仓储→隐藏/详情仍可访问→取消隐藏/置顶的闭环。主检出已有镜像文件，但 T1 worktree 未在隔离副本上启动后台 HTTP 站点；不读取或写入主检出数据库/本地配置。真实公开页在当前网络返回微信验证页，故后台真实链接导入未完成；T5 可在隔离副本和临时 ContentRoot 下补测。

## 用户上线时需要手动完成

1. 在站点读取库和后台可写库执行 `docs/sql/2026-10-26-wechat-sync.sql`。部署到同一库时只需执行一次。生产 SQL 由用户执行。
2. 配置 `WeChat:LegacyNewsMaxId` 为切换时刻旧 `News` 最大 Id；缺省为不隐藏旧新闻。
3. 如启用官方接口，填写 `WeChat:Enabled`、`AppId`、`AppSecret` 和相应来源开关，并先运行 `wechat-probe`。无权限的来源应关闭；链接导入不依赖 API 凭据。
4. 如启用群发回调，设置 Token 并将 `/wechat/callback` 配到公众平台。开启“服务器配置”会接管消息处理，编辑模式的自动回复与自定义菜单会失效；默认关闭，启用前需要接受此影响。回调只支持明文模式并校验签名。
5. 配置可写的 `LegacyAssets:ContentRoot`；封面存 `newsImg/`，正文图存 `WeChat/yyyy/MM/`。确认磁盘空间及 `/Content` 映射可读。

## 尚未验证

- 尚未取得可被微信公开页读取的真实文章响应，也未使用公众号凭据探测接口。
- 隔离 SQLite 镜像的端到端走查与服务器视觉验收留给主集成任务 T5。
