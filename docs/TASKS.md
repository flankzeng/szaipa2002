# 任务看板（TASKS）—— 后台自动化 Epic

> 规范见 [AGENT_SPEC.md](AGENT_SPEC.md)。领任务：把「状态」改为 `进行中`、填「负责人/分支」，提交这一行改动再开工。
> 状态：`待办` / `进行中` / `待合并` / `完成` / `阻塞（原因）`。
> 用户需求原话（2026-10-26）：展览（上传、展览中、已结束）、展览专题页、艺术家官网、新闻、组织架构（会长/副会长/会员…）现在都靠手打，后台不方便；新闻 99% 搬自微信公众号，要做自动同步，同步版上线后旧的手录新闻可以先不用。

## 用户已确认的决策（2026-10-26，开工前不必再问）
| 问题 | 决定 |
|---|---|
| 数据库 | 生产只读 + 本地 SQLite 镜像写；建表/上线 SQL 由用户执行 |
| 公众号资质 | 未认证/不确定 → **链接导入 + 官方接口两套都做**，接口在配好 AppID/AppSecret/IP 白名单后开启 |
| 同步文章上线 | 自动上线，后台可隐藏/置顶 |
| 旧手录新闻 | 列表隐藏，旧详情 URL 保留可访问，数据不删 |
| 公众号历史文章 | **全部补齐**：接口开通后全量回填；接口未开时支持**批量粘贴多条链接**导入 |
| 专题页 | 后台积木拼装；chunyu3/tonggou2/tonggou2024 三个手写页不动 |
| 组织架构 | 独立名单，可选关联艺术家（关联则跳转其官网） |
| 视觉 | 允许小幅统一（对齐/间距/字号），不改版；大 CSS 改动写中文注释 |
| 艺术家官网痛点 | 作品逐张上传（要批量+拖拽排序）、展讯活动写死、横幅配色手配（要实时预览） |
| 交付 | 发布包 + 上线手册，用户自己部署；agent 不碰服务器 |
| 资源 | 最多 4 个并行子 agent（Sonnet），集成由主 agent 把关 |
| 漫MAN 结束日期 | 以 05.28 为准（首页原文）；兼容目录与 slug 迁移 SQL 的 05.30 需改为 05.28 |

## 总览

| ID | 任务 | 状态 | 负责人 / 分支 | 依赖 |
|---|---|---|---|---|
| T0 | 本地镜像库（生产只读 → 本地 SQLite 可写副本） | 完成（已合并，375 测试绿） | Sonnet 子 agent / f1b854c | 无 |
| T1 | 微信公众号文章同步 → 填入现有新闻结构 | 已合并，最终真实来源验收受限（见 HANDOFF） | Codex / 883535f、224132f | 无（端到端验证需 T0） |
| T2 | 展览自动化：状态自动流转 + 首页展览数据化 + 专题页模板 | 已合并，HTTP 闭环通过，积木 GUI 待复验 | Codex / da166c4、4312776 | 无 |
| T3 | 组织架构与首页静态块数据化（成员/顾问/在售作品/合作伙伴） | 完成（已合并，422 测试绿） | Sonnet 子 agent / e79f4b6 | 无 |
| T4 | 艺术家官网一站式编辑 + 展讯数据化 + 头像裁切/黑白 | 已合并，艺术家 UI 闭环通过；成员裁切待复验 | Codex / c2748aa、c5a0363、5c63175 | 无 |
| T5 | 集成与上线就绪（合并、端到端、发布包、SQL 汇总、上线/回滚手册） | 进行中（639 测试绿，SQL/手册/候选包；剩余验收见 HANDOFF） | Codex 主 agent / `codex-refactor-dotnet-modernization` | T0–T4 |

### 文件归属（防冲突）
`Views/Home/Index.cshtml` 多任务共用，**只改自己负责的 section**：
- T1：`#section06`（新闻动态）
- T2：`#section02`（轮播）、`#section07`（展会活动）
- T3：`#section05`（核心成员）、`#section05guwen`（顾问团队）、`#section5to6`（在售作品）；以及 `About.cshtml`
- T4：`Art.cshtml`、`ArtArchive.cshtml`
共用热点 `SzaipaDataServiceCollectionExtensions.cs`、`_StaffLayout.cshtml`、两个 DbContext 的 `OnModelCreating`、`Program.cs`：**只追加，不重排已有代码**，便于合并。

---

## T0 本地镜像库
**目标**：本机拥有一份可写、数据与生产一致的数据库，前台读侧和后台写侧都指向它，从而能端到端验证"后台录入 → 前台展示"。生产库零写入。
- 本机无 Docker/SQL Server，用 **SQLite 文件**。
- 配置 `LocalMirror:Enabled`、`LocalMirror:SzaipaPath`、`LocalMirror:TongouPath`（默认 `<repo>/.local-db/*.db`，加入 .gitignore）。启用时 4 个上下文（两读两写）全部 `UseSqlite`；**`Production` 环境下启用直接启动失败**。
- 同步工具：`dotnet run --project src/Szaipa.Web -- mirror-sync`（或独立 console 项目），用只读连接逐表 SELECT → `EnsureCreated()` 后批量写入 SQLite；可重复执行（先删库重建）。新表（T1–T4 新增实体）由 EnsureCreated 建空表。
- 读上下文在镜像模式下仍禁 SaveChanges；只有 admin 上下文可写。
- 验收：mirror-sync 跑通两库；镜像模式下前台首页/新闻/艺术家/展览与生产只读模式渲染一致；后台新建一条新闻后前台可见；单测覆盖 provider 切换与 Production 拒绝。

## T1 微信公众号同步
**目标**：公众号新发布的文章自动**按现有新闻结构**进入网站新闻栏目，替代手录。
> 用户补充（2026-10-26）：**不是照搬公众号整页**，而是像现在一样把内容「填入」现有新闻结构；前台新闻版式不重构——详情页仍是「左边正文、右边近 3 期推荐」，列表页也保持现状。

- **落库到现有 `News` 表**（字段映射：Title、Subtitle←摘要 digest、Autor、Date←发布时间、CoverPath←封面本地化后的文件名（与后台上传封面同目录/同存法）、Content←清洗后的正文、`link`←公众号原文 URL、`original`←按 legacy 语义设置（先核对现有数据里 original/link 的用法））。这样前台、后台 News 模块、首页 `#section06` 全部零改结构即可显示。
- 新增**同步记录表** `WeChatSyncRecord`（ArticleId+ItemIndex 或 SourceUrl 唯一、NewsId、ContentHash、SyncedAt、IsHidden）用于去重/增量更新/标记来源；`docs/sql` 建表脚本。表不存在时同步功能禁用、前台照旧。
- 两条导入通道共用一个导入器：
  1. **按文章链接导入**（首要，当前公众号未认证）：后台粘贴一条或**多行批量**链接 `https://mp.weixin.qq.com/s/...`，抓取页面解析标题/作者/发布时间（页面脚本里的 ct 时间戳）/封面(og:image)/`#js_content` 正文。
  2. **官方接口**（接口资质到位后开启）：`POST https://api.weixin.qq.com/cgi-bin/stable_token` 取 token 并缓存，`POST /cgi-bin/freepublish/batchget` 分页（count≤20）拉全部已发布图文，支持**全量回填历史**；配置 `WeChat:{Enabled,AppId,AppSecret,SyncIntervalMinutes}`，凭据只走 Local.json/环境变量；BackgroundService 定时增量同步，仅在 Enabled 且凭据齐全时运行，失败记日志不崩。
- **公众号实际情况（用户 2026-10-26 核实）**：「深圳市艺术产业促进会」(SZAIPA)，主体「其他组织」，**未认证**；已有 AppID/AppSecret、IP 白名单，access_token 正常（2000 次/天）。要同步的是**已群发**文章。freepublish 只含「发布」记录，与群发历史不完全相通；未认证号的 freepublish/material 接口可能无权限。→ 方案：**多来源 + 统一导入器 + 规范化去重键（biz+mid+idx，从文章页解析，兼容 `/s/短码` 与长链接两种 URL）**：
  1. 链接导入（主通道，群发历史全量补齐靠它，支持多行批量）；
  2. 官方接口：freepublish/batchget + material/batchget_material(news)，各自独立开关/失败，错误码转可读提示；stable_token 进程内缓存，控制调用次数；
  3. 群发完成事件回调 `/wechat/callback`（MASSSENDJOBFINISH 的 ArticleUrlResult），**默认关闭**——启用「服务器配置」会让公众平台编辑模式的自动回复/自定义菜单失效，需用户权衡；
  4. `wechat-probe` 能力探测命令：用用户配置的凭据逐个探测接口可用性（不打印密钥），决定实际启用哪些来源。
- **正文清洗（重点：不照搬）**：图片 `data-src` → 下载到 `{ContentRoot}/WeChat/{yyyy}/{MM}/{hash}.{ext}` 并改写为本地 URL（禁止热链 mmbiz）；去掉公众号专有内容（关注引导、二维码、"往期推荐/往期回顾"、"阅读原文"、点赞在看、底部名片、空白占位段落等，规则可配置+有单测）；剥离内联 `style` 中的字号/字体/颜色/固定宽度，让正文吃前台 `.article-body` 的排版；白名单过滤 script/iframe/事件属性。
- 前台：
  - 列表 `/Home/News` 与首页 `#section06`：同步上线后**只显示来自公众号的新闻 + 上线后在后台新建的新闻**；旧手录新闻从列表隐藏（判定建议：无同步记录且 `Id <= WeChat:LegacyNewsMaxId`，该值在上线时配置），**旧详情 URL 继续可访问**，数据不删。IsHidden 的同步文章不在列表出现。
  - 详情 `NewsRead`：结构不变（左正文、右近 3 期推荐），推荐同样排除隐藏/旧新闻。
- 后台 `/Staff/WeChat`：同步记录列表（来源链接、对应新闻、同步时间、状态）、批量链接导入、立即同步、隐藏/取消隐藏、重新抓取；导入后可在现有 News 模块里继续编辑。
- 验收：导入器/清洗规则/去重/图片改写/旧新闻隐藏判定均有单测（本地 HTML fixture，测试不打外网）；本地镜像库上用真实链接导入一篇文章，前台列表和详情（390/1440）与现有新闻版式一致。

## T2 展览自动化 + 专题页
**目标**：展览录入后自动按日期显示为「即将开幕 / 展览中 / 已结束」，首页展览区与轮播不再手打，新专题页在后台拼装而非手写 1000+ 行模板。
- 状态由日期计算（统一一个纯函数/服务，带单测）：StartDate>今天=即将开幕；区间内=展览中；EndDate<今天=已结束；`Status`=false 视为下架不显示（先核对 legacy 对 Status 的真实用法再定）。
- 展会列表页按状态分组/筛选；后台列表显示状态徽标与筛选。
- 新增「首页推荐」字段（如 `IsFeatured`、`FeaturedSort`）：首页 `#section02` 轮播与 `#section07` 展会活动由数据驱动，替换目前 4 个硬编码展览块；用现有数据把它们登记为推荐项，视觉保持一致。缺列时回退现有硬编码。
- 专题页：`Publication.Type=2「专题」`+ 新表 `PublicationBlock`（PublicationId、Sort、BlockType：横幅/富文本/序言/作品目录/图库/视频/引言…、JSON 内容）+ 每个专题可配主题色/横幅图；前台 `_ExhibitionTopic.cshtml` 按块渲染（复用 `_ExhibitionGallery`、作品目录、`publication-special.css` 的样式语言）；后台块编辑器（增删、上下移、每块表单）。现有 chunyu3/tonggou2/tonggou2024 手写页**保持不动**。
- 验收：状态计算单测；后台建一个专题（≥4 种块）→ 前台正确渲染；首页展览区与改动前截图一致。

## T3 组织架构与首页静态块
**目标**：成员职务变动、在售作品、合作伙伴在后台改，不再改模板。
- 新实体 `OrgMember`（Name、Role 职务：会长/荣誉会长/副会长/监事/秘书长/理事/会员/顾问…、Title 头衔（艺术家/教授…）、Group 分组（核心成员/顾问团队…）、ArtistId 可空（关联则链接到 /Home/Art/{id}）、AvatarPath、Sort、IsVisible）。
- 新实体 `SaleWork`（在售作品：ArtistId、标题、尺寸/材质、图片、Sort、IsVisible）、`Partner`（合作伙伴：名称、Logo、链接、简介、Sort）。是否需要「合作案例」视 About.cshtml 现状决定。
- 后台三个模块（列表支持排序），种子 SQL 用**当前硬编码内容**生成，保证上线后视觉一致。
- 前台：Index `#section05`/`#section05guwen`/`#section5to6`、About 合作伙伴改为数据渲染；缺表回退现有硬编码。Vip 会员列表按需支持按职务分组。
- 验收：单测；镜像库里调整一位成员职务/排序 → 首页即时变化；页面与改动前截图一致。

## T4 艺术家官网
**目标**：一个艺术家的官网页（/Home/Art/{id}）所有内容在一处后台编辑完成。
- Art.cshtml「展讯」区目前硬编码 3 张活动图（春语II/同构II/春语I）→ 改为数据（艺术家参与的 Publication 或 Exhibition 记录，可在后台勾选/排序），缺数据时回退现状。
- 后台新增「艺术家官网」总览页 `/Staff/Artist/{id}/Site`：基本信息、横幅与配色（Color1/Color2/Path1/Path2，实时预览）、作品（**批量上传**+排序）、资讯、收藏、拍卖、展讯的入口与数量汇总，一页可达全部子模块。
- 检查 ArtArchive 页其他硬编码块并数据化。
- **头像裁切 + 黑白（用户 2026-10-26 追加）**：后台上传艺术家照片/成员头像时，弹出裁切框（固定比例，默认 1:1，可缩放拖动）→ 可选「转黑白」（成员头像默认开，首页核心成员现用的就是 `/Content/images/to black/` 下的黑白图）→ 浏览器 canvas 输出处理后的图片再走现有上传接口（`IAdminAssetStorage`），原图另存一份以便重新裁切。做成可复用组件（如 `wwwroot/admin/image-cropper.js`，Cropper.js 经 npm+esbuild 打包，MIT），接入 Artist 头像（Path）与 T3 的 OrgMember 头像；横幅 Path1/Path2 用宽幅比例、不转黑白。先核对前台各处头像实际是彩色还是黑白再定默认值。
- 验收：在镜像库里新建一位艺术家并通过总览页补齐全部内容 → 前台官网页完整显示，无硬编码残留；与现有艺术家页面视觉一致。

## T5 集成与上线就绪
- 合并 T0–T4，解决冲突，全量 build/test。
- 镜像库上逐页端到端走查（前台全部正式路由 × 390/1440；后台全部模块 CRUD）。
- 汇总 `docs/sql/` 本 Epic 新增脚本为一份按序执行的上线脚本 + 回滚脚本。
- `dotnet publish -c Release` 产物检查（见 `docs/deploy-windows.md` 的排除契约）。
- 写上线切换手册：配置清单（WeChat 凭据/IP 白名单、可写连接串、ContentRoot、DataProtection、qqwry.dat）、执行顺序、验证清单、回滚步骤。
