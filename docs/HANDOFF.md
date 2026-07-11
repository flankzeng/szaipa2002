# 交接（HANDOFF）

> 新 session 接手时：先读 [PROJECT_MAP.md](PROJECT_MAP.md) 和记忆目录里的各条记忆，再开工。下面的 prompt 可直接粘进新 session。

## 现状（已完成的核心，均 build 0/0 + 94 测试绿）
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
- **Alibaba 普惠体本地化（2026-07-11）**：官方 2.0 Light/Regular/Medium 已校验并生成约 170KB/档的本地核心子集，保留原 L/R/M 视觉层级；公共页已移除有字体第三方脚本。详见 `docs/updates/2026-07-11-alibaba-font-localization.md`。

## 剩余工作（多为既有模式复制，适合便宜模型）
1. ~~**展览「参展作品目录」**~~ **2026-07-09 完成**：新增 `ExhibitionWork` 实体 + works-manager.js 管理器（分类/标题/艺术家/尺寸/材质/图 + 排序），集成进 `_ExhibitionImportant.cshtml`。详见 `docs/updates/2026-07-09-exhibition-works-catalog.md`。
2. ~~**slug 页退役**~~ **2026-07-09 完成**：14 个简单 slug 页迁成 `Publication` 数据行（预留 ID 92001-92015），对应 action 改 301 重定向、硬编码视图已删、首页/NewArt 改链。chunyu3/tonggou2/tonggou2024（3 个特大自定义页）+ zengfeng（翻页书迷你站，非画廊结构）保留原样未迁移。**注意**：9 个已迁移展览的磁盘图片目录编号不连续/不规范，画廊会 404，需用户重新编号（清单见 `docs/updates/2026-07-09-publication-slug-migration.md`）；且需先执行 `docs/sql/2026-07-09-publication-slug-migration.sql`（核对生产库 92001-92015 未被占用后再跑）。
3. ~~**Phase 6 加固**~~ **2026-07-09 完成**：审查授权/anti-forgery/操作日志覆盖，均未发现遗漏；与 legacy 比对校验规则，排查的疑似缺口均核实排除；补测试 82→94。详见 `docs/updates/2026-07-09-phase6-hardening.md`。
4. **可选增强**：独立的全量操作记录页（分页查看历史，仪表盘现只显示近 7 天 feed）。
5. **NewIndex 进一步优化**（用户说后面单独聊，用新 session）。
6. **新需求（用户 2026-07-09 提出，尚未开工）**：微信公众号接口对接——新闻页面自动抓取公众号最新文章，格式化后新增到网站。需要先确认：走微信官方素材/草稿箱接口（需公众号是服务号+已认证、有对应 API 权限）还是第三方抓取方案；抓取节奏（定时轮询 vs webhook）；写入哪张表（News？新建 WeChatArticle？）；图片/图文消息里的媒体资源怎么落地到 `/Content`；去重与增量更新策略。
7. **前台清理 Phase 5**：Legacy 审计工具已完成；下一步需要生产 `/Content` 访问日志和数据库资源路径导出，复跑 `scripts/audit-legacy-content.py` 后才能处理运行根约 95.9MB 候选。Alibaba 普惠体需取得准确原文件后才能本地子集化。公众号需求继续后置。

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

约束：构建/测试用 ~/.dotnet/dotnet build|test Szaipa.Modernization.slnx（须 0/0 + 全绿，当前 94）；前端 cd src/Szaipa.Web && npm run build。DB 只读、绝不用生产凭据、不写 Windows 连的库；admin 写本地副本。没有可写库时只做 代码+单测+路由冒烟(302)，端到端留给用户本地。边做边验证、遇 legacy bug 顺手修。

加 admin 模块照 News 范本：仓储(SzaipaAdminContext)+控制器(Areas/Staff)+_Form/Index/Create/Edit 视图+导航；艺术家子模块用泛型基类 ArtistScopedAdminRepository<T>；写操作走 IOperationRecorder。

先告诉我你打算先做哪一项、用什么模型（建议：slug 迁移/Phase 6 用 Sonnet 4.6 medium；仪表盘/作品目录/架构决策才用 Opus）。
```
