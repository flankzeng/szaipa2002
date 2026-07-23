# 交接（HANDOFF）

> 新 session 接手时：先读 [PROJECT_MAP.md](PROJECT_MAP.md) 和记忆目录里的各条记忆，再开工。下面的 prompt 可直接粘进新 session。

## 现状（已完成的核心，均 build 0/0 + 222 测试绿）
- **Phase 0 基座**：可写 `SzaipaAdminContext`（门控本地副本）、cookie 认证替换 Session、`Areas/Staff` 外壳、Tailwind 主题（品牌红 #bf272d，贴近 NewIndex）。
- **Phase 1**：TipTap 富文本编辑器 + 上传服务（唯一 GUID 命名，规避旧版图片误删 bug）。
- **Phase 2**：News + ArtNews 全 CRUD。
- **Phase 3（完成）**：Fav / Auction / Exhibition / **Works**（泛型基类 `ArtistScopedAdminRepository<T>`）、**Artist 主表 CRUD**（照 News 范本而非泛型子类）。Artist 详见 `docs/updates/2026-06-24-artist-admin-module.md`；Works 详见 `docs/updates/2026-06-24-works-admin-module.md`（legacy 重复的 `ArtWorksAdd/Edit` vs `WorkAdd/Edit` 两套写入流程已合并为一个，未照搬已死的 Width/Height/transverse/long 字段）。
- **Phase 4（完成）**：**Company**（主表 CRUD，详见 `docs/updates/2026-06-24-company-admin-module.md`）、**Tongou(Atrist/Works)**（新建独立可写 `TongouAdminContext`，详见 `docs/updates/2026-06-24-tongou-admin-module.md`——含跨库审计模式、`upload-field.js` 的 `data-store="url"` 开关）。
- **Phase 5（完成）**：访问分析仪表盘（KPI + ECharts 折线/饼/双旭日 + 操作记录 feed，只读聚合 `SzaipaAdminContext`）+ 修改密码。ECharts 走 npm+esbuild 自包含打包。详见 `docs/updates/2026-06-24-dashboard-analytics-phase5.md`。
- **前台 bug**：后台登录入口失效已全站修复（详见 `docs/updates/2026-06-24-admin-login-entry-fix.md`）。
- **展览模块**：普通型 + 重要型（数据驱动 `Publication` + 画廊上传器 + 两套皮肤）。
- **前台优化**：NewIndex / NewNews / NewNewsRead / NewArt 内联 CSS/JS 外提到缓存文件；成员头像区 table→flex 去重复。
- **旧后台 bug**：news-add 丢正文 / newsImg 误删 已在 `szaipa2022` 源码打补丁。
- **前台清理 Phase 1（2026-07-10）**：公共依赖按页加载、新闻详情移除 Vue/Element Plus、图片懒加载、Brotli/Gzip + 缓存、同字体核心子集、手机 viewport/navbar 修复、隐藏 Swiper 不再误初始化。详见 `docs/updates/2026-07-10-frontend-cleanup-phase1.md`。
- **ProjectTongou 公开层退役（2026-07-10）**：线上和本地均确认无前台入口；12 个公开视图及迁移仪表盘死入口已移除，历史 URL 返回可缓存 410 且不查库。Tongou 后台、实体、仓储和上传资源保留。
- **前台清理 Phase 2（2026-07-10）**：本地 legacy 归档分支已建；zengfeng 退役并移除 140MB 专属资源；关于我们、真实会员列表、数据驱动展会列表上线；零引用模板库清理后 Release publish 约 35MB→24MB；三个大自定义展览页移除约 2.9MB/页无效依赖并补齐图片懒加载。详见 `docs/updates/2026-07-10-frontend-cleanup-phase2.md`。
- **前台清理 Phase 3（2026-07-11）**：三个大自定义展览页的内联 CSS/JS 已外提；三页共享特殊页 CSS，chunyu3/tonggou2 共享行为脚本，页面差异由 CSS 变量/data 配置保留；远程原字库增加失败保护但未替换字形。详见 `docs/updates/2026-07-11-frontend-cleanup-phase3.md`。
- **前台清理 Phase 4（2026-07-11）**：新增可复跑 Legacy Content 白名单审计；修复首页唯一真实缺图；当前分支归档旧 Content、两套旧 NuGet packages 和零引用静态副本，累计修剪约 501MB。运行资源根仍有约 95.9MB 候选，未取得生产日志前不删除。详见 `docs/updates/2026-07-11-frontend-cleanup-phase4.md`。
- **前台清理 Phase 5（2026-07-11）**：保留原 Alibaba 普惠体，将六类公共页面的有字体第三方脚本改为延迟加载，字体初始化等待 DOM 就绪，滚动监听改为 passive。详见 `docs/updates/2026-07-11-frontend-cleanup-phase5.md`。
- **前台清理 Phase 6（2026-07-11）**：只读扫描旧发布版 `web24.05`；确认实际 Content 约 1.2GB，现代源码候选 95.9MB、旧版源码候选 87.8MB，旧版仍在线时不得删除差出的约 8.1MB。官方普惠体源已定位；旧 Web.config 明文凭据需轮换。详见 `docs/updates/2026-07-11-frontend-cleanup-phase6.md`。
- **本地只读库状态（2026-07-11）**：Szaipa/Tongou 只读连接均可用，真实首页读取成功；`AllowLiveDatabase=false`。但当前库尚无 Publication 92001–92015 迁移行，首页硬编码展会暂不能退役。
- **数据库资源路径审计（2026-07-12）**：只读导出得到 19 条明确 `/Content/` 路径；加入审计后候选仍为 95.9MB，说明候选目录无数据库精确引用。物理删除仍等待 IIS 日志。
- **生产 IIS 访问审计（2026-07-13）**：只读扫描近 90 天 92 个日志、553,963 条请求行；`_preview`/`TempFile`/`js`/`Filme` 均有成功访问，必须保留；仅 `Award`（94 文件/7,197,911 B）和 `layui`（15 文件/876,709 B）在源码、数据库、IIS 三层均为零引用。服务器未移动/删除任何发布文件。**用户已明确服务器只供只读参考、发布版以稳定为主；不得部署、隔离或改 IIS，任何必须的服务器改动须先停下说明并取得明确确认。**详见 `docs/updates/2026-07-13-production-iis-content-audit.md`。
- **静态资源缓存防回退（2026-07-13）**：新增可测试的缓存策略；带 `?v=` 的自有资源改为 1 年 `immutable`，普通自有 CSS/JS 保留 7 天，外接 `/Content` 图片/字体恢复生产基线 30 天、CSS/JS 7 天、未知类型 1 天；新增 Web 测试项目 14 项，总测试 96→110。详见 `docs/updates/2026-07-13-static-asset-cache-policy.md`。
- **Alibaba 普惠体本地化（2026-07-11）**：官方 2.0 Light/Regular/Medium 已校验并生成约 170KB/档的本地核心子集，保留原 L/R/M 视觉层级；公共页已移除有字体第三方脚本。详见 `docs/updates/2026-07-11-alibaba-font-localization.md`。
- **Noto Sans/Serif SC 本地化（2026-07-13）**：保留仓库旧 Noto 轮廓与原字重匹配；只读扫描两库 2,716,118 个文本值，将当前 2,922 个 codepoint 纳入 core，GB2312 余字按需拆成小分片。121 个本地 WOFF2 合计 17,820,936B，7 个正式路由验证只加载 core、无远程/legacy Noto；刻意新字只加载一个 74,428B 分片。13 个 92,580,992B 全量源逐 blob 核对后仅保留在远端 legacy archive，已从当前分支移除。详见 `docs/updates/2026-07-13-noto-font-localization.md`。
- **首页首图优先级（2026-07-13）**：把唯一 `fetchpriority="high"` 从装饰文字图转移到实际首张轮播封面；数据库轮播为空时自动落到首张硬编码封面，其余图片继续 lazy。真实只读首页验证仅首张封面 eager/high。详见 `docs/updates/2026-07-13-home-carousel-lcp-priority.md`。
- **公共布局 CSS 外提（2026-07-13）**：`_newLayout` / `_Artist` 的静态内联样式拆为 common + main/artist 三个带版本的缓存文件；两页面、两视口 computed style 前后等价，均无横向溢出，首页 navbar 保持单行横排。首页 HTML 减少 9,625B（12.84%），NewArt 减少 7,201B（8.83%）。详见 `docs/updates/2026-07-13-layout-css-extraction.md`。
- **展览页 jQuery/移动 viewport（2026-07-13）**：普通展览和三个特殊展览页移除无必要 jQuery（每页约省 30,977B gzip），Swiper/页面脚本改为有序 `defer`；同时修复特殊页缺 viewport 导致手机 390px 按 1440px 桌面画布渲染的问题。三页手机均 `scrollWidth=clientWidth=390`，Swiper 3/3/2、0 控制台错误。详见 `docs/updates/2026-07-13-publication-jquery-removal.md`。
- **图片加载时机（2026-07-13）**：NewArt 的真实 Path1 首屏背景图加入唯一 preload/high，未重复下载；首页 849KB 章程 CSS background 改为同原图 lazy `<img>`，手机截图逐字节一致、桌面几何一致。详见 `docs/updates/2026-07-13-image-loading-priorities.md`。
- **首页静态缩略图（2026-07-13）**：用户确认无需视觉 pilot，直接复用既有 q30w1200。6 张在售图列表由 9,860,285B→981,672B，Magnify 改为首次真实交互才请求对应原图；另 29 张章程/成员/活动下折图由 14,315,658B→1,477,621B。两批完整滚动合计减少 21,716,650B（约 89.83%），顶部/LCP、Logo、透明装饰和同页复用原图均未替换。详见 `docs/updates/2026-07-13-selling-thumbnail-previews.md`、`2026-07-13-homepage-static-preview-expansion.md`。
- **公共图片固有比例（2026-07-13）**：为公共 Logo、NewArt 静态活动图、关于我们合作伙伴图和三个特殊展览开篇图共 16 处补真实 `width`/`height`；只提供比例元数据，不改变现有 vh/vw/%/rem 响应式 CSS。详见 `docs/updates/2026-07-13-public-image-intrinsic-sizes.md`。
- **动态图片预览回退（2026-07-13）**：新增只读 `ILegacyImagePreviewResolver`，以大小写不敏感的安全索引查找现有 q30w1200，命中用预览、缺失/歧义/非法路径自动回原图；首批接入首页/新闻列表封面、会员卡/艺术家头像、展会列表卡片。新增 34 个 Web 测试用例，总测试 110→144。详见 `docs/updates/2026-07-13-dynamic-image-preview-resolver.md`。
- **动态预览扩展（2026-07-20）**：额度恢复后补齐 5 个真实只读路由冒烟；NewArt 作品在已有 q30 时先显示预览，Magnify 首次真实交互才加载原图，真实王玉波样本少传 1,908,434B；新闻详情侧栏和重要展览作品卡片也接安全回退解析器，并修复新闻详情手机 navbar 最后一项被裁切。详见 `docs/updates/2026-07-20-dynamic-preview-expansion.md`。
- **新闻详情手机/平板排版（2026-07-20）**：局部覆盖 legacy ≤991 的 26px 根字号和正文 `5em` 强制行高，清理正文两侧浮动占位；320/390/768/991 实页均无横向溢出，navbar 四项保持单行横排。详见 `docs/updates/2026-07-20-news-detail-mobile-typography.md`。
- **公共页手机/平板/窄桌面字号（2026-07-20）**：修复 legacy ≤991 强制 26px 和 992–1279 强制 12px 的两段根字号；现代公共布局、新闻详情与特殊展览在 991→1280 连续为 16px 基准，公共 navbar 从 15px 平滑接到 24px 且始终单行。另修 NewAbout 合作伙伴图溢出卡片。详见 `docs/updates/2026-07-20-public-mobile-font-scale.md`。
- **响应式细节/字体缓存加固（2026-07-22）**：三个特殊展览的作品详情在手机端改为单列文档流；NewArt 展讯和 NewNewsRead 富文本补齐窄屏溢出保护；数据展览修正 legacy/现代 CSS 加载顺序。125 个本地 WOFF2 保持原字形和按需分片，只新增内容哈希 URL 以进入一年 immutable 缓存，并由 Web 契约测试逐文件核对。总测试 216→217。详见 `docs/updates/2026-07-22-responsive-font-cache-hardening.md`。
- **图片放大器按需加载（2026-07-22）**：NewIndex/NewArt 的非放大行为已原生化；jQuery、Magnify 插件和 CSS 从首轮移除，首次真实交互才由共享 loader 加载，未使用放大镜时冷加载原始体积净省 97,277B。另修 NewArt 320px 导航箭头造成的 1px 横向溢出，链接保持单行横排。总测试 217→218。详见 `docs/updates/2026-07-22-magnify-on-demand.md`。
- **Bootstrap 分页退役试点（2026-07-22）**：NewIndex/NewVip/PublicationList/NewNews/NewAbout/NewArt 不再加载 143,947B Bootstrap CSS，改用带版本的 3,105B 兼容基线；每个冷页面原始体积少 140,842B，320–1440 共 81 个响应式检查通过，代表元素的计算样式/几何一致。其余页面默认仍加载 Bootstrap。总测试 218→219。详见 `docs/updates/2026-07-22-bootstrap-optout-pilot.md`。
- **前台/Staff 字体清单拆分（2026-07-22）**：原 147 个 `@font-face` 的单一清单拆成 public 114 / Staff 88，交集只保留双方需要的 55 个 Sans 声明；125 个 WOFF2、字形、字重、哈希和 `unicode-range` 均未改。每个页面只下载自己的清单：public 原始 CSS 少 80,909B（gzip -58.88%），Staff 少 136,438B（gzip -63.42%）。11 个路由×宽度组合及 Staff 仪表盘/编辑器实页验证无几何回退，测试 219→221。详见 `docs/updates/2026-07-22-font-manifest-split.md`。
- **NewArt 运行时/横幅审计（2026-07-23）**：移除视图中不存在的 `.mySwiper2` 初始化，并把 Swiper、Magnify loader、页面脚本改为保序 `defer`，避免 135,660B Swiper 阻塞尾部 HTML 解析。主横幅的 AVIF 原型未达无损收益门槛，故不加入派生资产、不替换原图。1440 与 390 实页均保持首屏几何、单行导航和零横向溢出；测试 221→222。详见 `docs/updates/2026-07-23-newart-runtime-and-banner-audit.md`。
- **特殊展览图库分层加载（2026-07-20）**：三页 201 张现场图的主/缩标记收敛为同一数组；170 张已有 q30 先显示预览，图库接近视口后只预载 active/prev/next 原图，理论首轮少传 50,506,952B。三页统一共享 JS，退役重复的 tonggou2024 脚本。详见 `docs/updates/2026-07-20-special-gallery-layered-loading.md`。
- **高质量首页首图（2026-07-20）**：现代仓库内置 1080×791 AVIF，首页当前 LCP 从 1,783,805B 降到 167,097B（约 -90.63%），原 `/Content` 图保留为 fallback；新增严格 allowlist 派生解析器。NewArt 空 Path 不再拼 Banner 目录/产生 404；三张现有全屏 Banner 经审计后因高 DPR 画质风险暂不强换 1600px 版本。总测试 144→181。详见 `docs/updates/2026-07-20-high-quality-derived-images.md`。
- **独立现代仓库契约（2026-07-20）**：现代跟踪边界约 20.4MiB，旧 Content 约 1.1–1.2GiB 继续作为外部共享卷而非删除；当前约 1.8GiB Git 历史不进入新仓库，改从脱敏后的干净 commit 导出现代白名单并建立全新 root。Data Protection 使用固定 Production ApplicationName、外部持久 KeysPath、Windows machine-scoped DPAPI 与启动自检；Staging 使用独立 key/cookie/hostname。用户已创建 Gitee 目标仓库及本地 `~/Project/GitClone/szaipa2026`，目前只有初始 README，尚未导出现代源码；本轮 UI 人工验收前不得填充，也未改服务器/IIS/hook。由于远端已有初始 root，最终需用户决定是经明确授权替换 `master` 以保持单 root，还是接受两 commit 偏差。详见 `README.md`、`docs/repository-split.md`、`docs/updates/2026-07-20-independent-modern-repository.md`。
- **未来发布包瘦身（2026-07-13）**：禁用当前 `UseStaticFiles` 不会选取的 SDK `.br/.gz` 发布副本，并排除 Node 清单、示例配置和 `.gitkeep`；本地对照实测少 761,732B，发布进程仍正确协商 Brotli/Gzip。仅影响未来本地生成包，服务器未部署/改动。详见 `docs/updates/2026-07-13-publish-payload-trim.md`。
- **旧凭据清理（2026-07-12）**：旧 MVC Web.config 与跟踪中的 bin 配置副本已改为部署占位符；历史密码仍必须在数据库服务器轮换。详见 `docs/updates/2026-07-12-legacy-credential-sanitization.md`。

## 剩余工作（多为既有模式复制，适合便宜模型）
1. ~~**展览「参展作品目录」**~~ **2026-07-09 完成**：新增 `ExhibitionWork` 实体 + works-manager.js 管理器（分类/标题/艺术家/尺寸/材质/图 + 排序），集成进 `_ExhibitionImportant.cshtml`。详见 `docs/updates/2026-07-09-exhibition-works-catalog.md`。
2. ~~**slug 页退役**~~ **2026-07-09 完成**：14 个简单 slug 页迁成 `Publication` 数据行（预留 ID 92001-92015），对应 action 改 301 重定向、硬编码视图已删、首页/NewArt 改链。chunyu3/tonggou2/tonggou2024（3 个特大自定义页）+ zengfeng（翻页书迷你站，非画廊结构）保留原样未迁移。**注意**：9 个已迁移展览的磁盘图片目录编号不连续/不规范，画廊会 404，需用户重新编号（清单见 `docs/updates/2026-07-09-publication-slug-migration.md`）；且需先执行 `docs/sql/2026-07-09-publication-slug-migration.sql`（核对生产库 92001-92015 未被占用后再跑）。
3. ~~**Phase 6 加固**~~ **2026-07-09 完成**：审查授权/anti-forgery/操作日志覆盖，均未发现遗漏；与 legacy 比对校验规则，排查的疑似缺口均核实排除；补测试 82→94。详见 `docs/updates/2026-07-09-phase6-hardening.md`。
4. ~~**可选增强：独立的全量操作记录页**~~ **2026-07-12 完成**：`/Staff/Operations` 按日期分页查看完整历史，仪表盘保留近 7 天 feed 并链接完整页。详见 `docs/updates/2026-07-12-operation-history-page.md`。
5. **前台图片继续优化**：静态/动态卡片图、NewArt 作品预览、三个特殊展览现场图分层加载和首页 LCP 高质量 AVIF 均已完成。NewArt 全屏图的 1600px 试验不足以安全覆盖高 DPR 桌面，继续时先验证 2400px 高质量档，不能直接换 q30；不在服务器或旧稳定发布版生成/替换图片。
6. **新需求（用户 2026-07-09 提出，尚未开工）**：微信公众号接口对接——新闻页面自动抓取公众号最新文章，格式化后新增到网站。需要先确认：走微信官方素材/草稿箱接口（需公众号是服务号+已认证、有对应 API 权限）还是第三方抓取方案；抓取节奏（定时轮询 vs webhook）；写入哪张表（News？新建 WeChatArticle？）；图片/图文消息里的媒体资源怎么落地到 `/Content`；去重与增量更新策略。
7. **前台 Legacy 清理**：生产数据库和 IIS 日志审计均已完成；`Award` 与 `layui` 只是审计候选。服务器当前只供参考，不移动、不删除、不部署；若未来确有必要，必须先向用户说明并确认。其余候选根有真实访问，继续保留。公众号需求继续后置。
8. ~~**992–1279 窄桌面字号**~~ **2026-07-20 完成**：用户确认采用连续缩放；根字号与两侧统一为 16px，navbar 用 `rem + vw` 从 992 的 15px 平滑接到 1280 的 24px。991/992/1024/1199/1200/1279/1280 及主要前台实页均无横向溢出或换行。

## 待用户本地操作
- **本地只读登录检查（推荐，不写库）**：`appsettings.Local.json` 配 `AdminWrite:{EnableWrites:true, UseReadOnlyConnectionForDebug:true}`（Tongou 同），不设 `ConnectionStrings:SzaipaAdmin`——admin 上下文复用只读 `szaipa_ro` 连接，能登录/看仪表盘/浏览，写操作被 SQL 层挡掉。前提：`LegacyData:Szaipa` 的只读凭据当前有效（2026-06-24 实测遇到 `18456` 认证失败，需核对密码）。详见 `docs/updates/2026-06-24-admin-readonly-debug-login.md`。
- **发布环境真实写入**：`AdminWrite:EnableWrites=true` + `ConnectionStrings:SzaipaAdmin`（真正可写副本）。写入推迟到发布/部署环境，agent 永不写库。
- 若要点测 Tongou 写：再配 `TongouAdminWrite:EnableWrites=true` + `ConnectionStrings:TongouAdmin`（Tongou 可写副本，与 Szaipa 是两个库）。
- 可写 `LegacyAssets:ContentRoot`（上传图片落盘需要）。
- 跑 `docs/sql/2026-06-exhibition-template-columns.sql`（给 Publication 加 Type/Preface/Signature；重要型渲染需要）。
- 旧 `szaipa2022` .NET Framework 改动需在能跑该栈的机器上重新生成确认编译。

## 模型建议
核心难点已做完，剩下多是「照模式复制」的 CRUD/功能活，**不必再用 Opus 4.8 高 effort**：
- **默认 Sonnet 4.6（medium）**：slug 迁移、Phase 6。模式清晰、性价比最高。
- **Haiku 4.5**：纯机械活。
- **升回 Opus 4.8（high）**：仅 Phase 5 仪表盘数据可视化设计、展览作品目录 UX、dev/prod 库架构决策、棘手调试。
- **每个 phase 开新 session**（上下文短=更省更快），靠记忆文件 + 本交接 + PROJECT_MAP 续接。

---

## 粘贴用 prompt（新 session）
```
你接手「szaipa2002」ASP.NET Core 迁移项目。先读 docs/PROJECT_MAP.md、docs/HANDOFF.md 和记忆目录里的各条记忆，用中文给我汇报，再开工。

约束：构建/测试用 ~/.dotnet/dotnet build|test Szaipa.Modernization.slnx（须 0/0 + 全绿，当前 222）；前端 cd src/Szaipa.Web && npm run build。DB 只读、绝不用生产凭据、不写 Windows 连的库；admin 写本地副本。Windows 服务器及旧发布版只供参考，不部署、不改 IIS、不移动/删除发布文件；确需服务器变更时先停下说明并取得明确确认。没有可写库时只做 代码+单测+路由冒烟(302)，端到端留给用户本地。边做边验证、遇 legacy bug 顺手修。

加 admin 模块照 News 范本：仓储(SzaipaAdminContext)+控制器(Areas/Staff)+_Form/Index/Create/Edit 视图+导航；艺术家子模块用泛型基类 ArtistScopedAdminRepository<T>；写操作走 IOperationRecorder。

先告诉我你打算先做哪一项、用什么模型（建议：slug 迁移/Phase 6 用 Sonnet 4.6 medium；仪表盘/作品目录/架构决策才用 Opus）。
```
