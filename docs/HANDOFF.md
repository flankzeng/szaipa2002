# 交接（HANDOFF）

> 新 session 接手时：先读 [AGENT_SPEC.md](AGENT_SPEC.md)（协作规范）→ [PROJECT_MAP.md](PROJECT_MAP.md) → [TASKS.md](TASKS.md)（任务看板，含用户已确认的全部决策），再开工。下面的 prompt 可直接粘进新 session（Claude Code / Codex 通用）。

## 最新交接（2026-10-27：服务器 staging + 真实写入验收）

- **服务器**（`ssh szaipa`）：`szaipa-new` 仅 127.0.0.1:8088，现跑 **`43f7eb6`**，读写指向副本库 `Szaipa_staging`/`Tongou_staging` 与图片副本；线上站未动。全部记录与回退：`docs/updates/2026-10-27-server-staging.md`。
- **真实写入验收 17/17 通过**；修复 4 个真实库才暴露的问题 + 保存失败中文兜底（`43f7eb6`，673 测试绿）。**当前交付包 `artifacts/szaipa-automation-43f7eb6-win-x64.zip`，SHA-256 `f6763dc68dfa9e29ea3add7c62d1f779441883ec3e927517623dc95efffec32b`**（faadcb8 作废）。
- 上线 SQL 在服务器执行必须 `sqlcmd -f 65001`（见部署记录）。
- 今晚 02:00 预热任务已改指向 43f7eb6（线上 Content → 正式缓存）。
- 进行中（子 agent）：T9 公众号接口配置页（后台填写 AppID/AppSecret，Data Protection 加密存文件，测试连接，40164 显示服务器出口 IP）；staging 抽测（公众号导入/专题积木/现场编辑/会员排序/图片上传）。
- 待用户决定：线上 `web24.05\Content` 的 Everyone 完全控制 ACL；正式切换时间。

---

## 第四轮交接（2026-10-26：图片流量优化 T8）

### 当前基线
- 分支 `codex-refactor-dotnet-modernization`；**发布包对应源码 `efb56aa`**（之后仅文档）。测试 **672 全绿**（Data 389 + Web 283）。
- 交付包：`artifacts/szaipa-automation-efb56aa-win-x64.zip`，**32,716,533 B**，SHA-256 `1ab532e54eb8ab928ed60cff93c09b032cb91eca258d1b1dbc856b06c11a03d6`；清单 `artifacts/publish-win-x64-efb56aa.manifest.json`（222 文件，审计通过）。旧包 83e9dee / f884a55 作废。
- 本轮记录：`docs/updates/2026-10-26-image-variants.md`；部署步骤 `docs/automation-rollout.md` §4.5。

### 本轮完成（T8）
- `/_img/{480|960|1600|2400}/Content/...` 按需生成 WebP + 磁盘缓存（SkiaSharp），原图不动、失败回退；预览解析 28 处统一 960 WebP；全局 TagHelper 给 /Content 图片补 srcset；画廊大图 1600、缩略图 480、放大镜 2400；新闻正文补 srcset；`image-variants-warm` 存量预热命令。
- 实测：全站存量 1047.6MB → 960 宽 156.8MB（-85%）；13 个页面完整浏览手机 -57%、电脑 -56%；画质抽检无可见差异；预热 0 失败。
- 发布时排除 SkiaSharp 96MB 调试符号。
- 死图检查：13 页 × 2 宽度 2386 个图片 URL，排除本地样本/本地快照缺失文件后全部可用；修复会员风采头像路径前导空格导致优化未生效（`efb56aa`）。

### 上线前剩余（按顺序）
1. **服务器准备**（用户）：缓存目录 + 应用池读写权限 + `ImageVariants:CacheRoot`；部署后跑一次 `image-variants-warm`（服务器上预计 15–25 分钟）。
2. **真实 SQL Server 写入验收**（用户提供环境，agent 协助）：建 `Szaipa_staging` 副本 → 执行 `docs/sql/2026-10-26-automation-deploy.sql` → 旁路端口跑新站 → 后台各模块增删改走一遍（脚本 `artifacts/verification/` 下 HTTP CRUD 可参考）。至今写路径只在 SQLite 镜像验证过。
3. **正式切换**：按 `docs/automation-rollout.md`（执行 SQL、旧后台停写、切 IIS 绑定、验证、回退预案）。
4. 上线后可补：T6 现场编辑其余页面加标记（清单见下一节「第三轮」）；公众号凭据与 `wechat-probe`；后台上传即时生成变体、艺术家横幅 CSS 背景图多尺寸。

### 本机续测
- 本地缓存目录 `src/Szaipa.Web/App_Data/image-cache/e3/`（已全量预热，gitignore）。对照实例：变体开 5070 / 关 `ImageVariants__Enabled=false`；测量脚本在 scratchpad 外未保留，方法写在更新文档里。

### 可粘贴给下一位 agent（Claude Code / Codex 通用）
```text
接手 szaipa2002 上线收尾。先读 docs/AGENT_SPEC.md → PROJECT_MAP.md → TASKS.md → HANDOFF.md 顶部最新交接。T0–T5、T7、T8 已完成，T6 现场编辑核心完成（其余页面待加标记）。当前发布包 efb56aa。下一步优先：协助用户在服务器副本库 Szaipa_staging 上做真实 SQL Server 写入验收（agent 只给步骤和脚本，不碰服务器），然后按 automation-rollout.md 上线；有余力再给剩余页面接入现场编辑。生产只读，写只在本地 SQLite 镜像。中文 commit 与 docs/updates。5 小时额度剩 5% 时停下更新交接。
```

---

## 第三轮交接（2026-10-26：现场编辑 + 会员风采排序）

### 当前基线
- 分支 `codex-refactor-dotnet-modernization`；**发布包对应源码 `83e9dee`**（之后仅文档提交）。测试 **661 全绿**（Data 389 + Web 272）；SQL 汇总 **9 份来源**一致（新增 `site-text.sql`、`vip-order.sql`）。
- 交付包：`artifacts/szaipa-automation-83e9dee-win-x64.zip`，**27,170,654 B**，SHA-256 `790bb7622cb5dfd8451319c5f659ccc6a7afd48f3d8c1a87a623da0b37e5bbf7`；清单 `artifacts/publish-win-x64-83e9dee.manifest.json`（220 文件，审计通过）。旧包 1f51e81 作废。
- 本轮记录：`docs/updates/2026-10-26-live-edit.md`、`2026-10-26-vip-order.md`。

### 本轮完成
- **T6 现场编辑核心**（`4d06828`）：后台导航「现场编辑」→ 微缩官网（手机/平板/电脑），点虚线框文字直接改、富文本弹窗、立即保存+操作记录；访客页面零变化；白名单 `LiveEditRegistry` + 新表 `SiteText`。浏览器实测：首页标题、新闻标题、新闻正文（含 12 张图保留）三类编辑均成功（测试数据已还原）。
- **T7 会员风采排序**（`05ec21f` 合入 `a05823a`）：`/Staff/VipOrder` 拖拽/▲▼/显隐，Vip 页按序显示，缺表回退。

### 下一位 agent 要做（T6 剩余：给其余页面加编辑标记，纯视图工作）
模式见 `docs/PROJECT_MAP.md`「现场编辑」节与已接入的 `Views/Home/Index.cshtml`、`Views/Shared/_HomeMemberItems.cshtml`、`Views/Home/NewsRead.cshtml`：
1. `About.cshtml`：简介段落 → `@LiveEdit.Lines("about.xxx", "原文")`；合作伙伴名称/简介 → `@LiveEdit.Field("Partner", p.Id, ...)`（`PartnerModel.Id` 已有）。
2. `News.cshtml`（新闻列表）：标题/摘要 → `Field("News", ...)`。
3. `Art.cshtml` / `ArtArchive.cshtml`：艺术家姓名/职务/简介（Field Artist）、事迹 `DeedsThings` 容器加 `Attr`（富文本）、作品名 `Works.Title`、出版著作（ArtistBook）。
4. `Views/Shared/_ExhibitionGallery.cshtml` / `_ExhibitionImportant.cshtml` / `_ExhibitionTopic.cshtml`：展览中英文标题、序言、落款、地点、主办承办（Field Publication）；注意兼容目录展览（固定 id 92001–92015，数据库无行）不可编辑，需判断行真实存在（读模型若无此标志，可只在 Id 不在 92001–92015 时输出）。
5. `Vip.cshtml`：卡片姓名/头衔（Field Artist）。`Constitution.cshtml`：章程正文较长，建议按段落 `@LiveEdit.Lines("constitution.pN", ...)`，或暂不接入。
6. 每接一页：普通访客 HTML 必须与改动前一致（`curl` 对比，`grep -c data-le-` 应为 0）；编辑态在浏览器实测一次保存；`~/.dotnet/dotnet test` 全绿；重建发布包并更新本节。

### 其他待办
- 用户：按 `docs/wechat-setup-guide.md` 配公众号凭据 → 运行 `wechat-probe`；生产执行 `docs/sql/2026-10-26-automation-deploy.sql`；按 `docs/automation-rollout.md` 部署。IIS 不能对本站加 `X-Frame-Options: DENY`（现场编辑预览需同源 iframe，至多 `SAMEORIGIN`）。
- 可选：现场编辑支持换图（目前只改文字）；SiteText 改动历史/撤销。

### 本机续测
- 主镜像 `.local-db/` 已手工建 `SiteText` 表（未重跑 mirror-sync，样本 Artist 1053 / Publication 2 / News 1126、1127 仍在）；`ArtistDisplay` 表未建（Vip 走回退）。如重跑 `mirror-sync` 会按最新模型建全表但丢样本。
- 启动：`LocalMirror__Enabled=true LegacyAssets__ContentRoot=/var/folders/jr/yvdkbjjd7czcsljyhxtvf96r0000gn/T/szaipa-epic-assets-w0anexd9/Content ~/.dotnet/dotnet run --project src/Szaipa.Web --urls http://127.0.0.1:5070`，后台 debug/debug，导航「现场编辑」。

### 可粘贴给下一位 agent（Claude Code / Codex 通用）
```text
接手 szaipa2002「后台自动化」Epic。先读 docs/AGENT_SPEC.md → PROJECT_MAP.md（含「现场编辑」节）→ TASKS.md → HANDOFF.md 顶部最新交接。T0–T5、T7 已完成，T6 现场编辑核心已完成并在首页/新闻详情接入；你的任务是按 HANDOFF「下一位 agent 要做」给关于我们、新闻列表、艺术家官网/档案、展览、会员风采等页面加现场编辑标记，逐页保证访客 HTML 不变并在浏览器实测保存，最后全量测试、重建发布包、更新 HANDOFF。生产只读，写只在本地 SQLite 镜像；上传用 HANDOFF 里的临时 Content。中文 commit 与 docs/updates 文档。5 小时额度剩 5% 时停下更新交接。
```

---

## 上一轮交接（2026-10-26 第二轮，Claude 集成验收后交付候选）

### 当前基线
- 分支 `codex-refactor-dotnet-modernization`。**发布包对应源码 `1f51e81da3f30113a63be9178c776701ef144e42`**；之后的提交只改文档（看板/交接）。
- build 0 error；测试 **648 全绿**（Data 376 + Web 272）；`scripts/build-automation-sql.py --check` 七份来源一致；发布审计脚本测试 OK。
- 本轮完整记录：`docs/updates/2026-10-26-claude-acceptance.md`；上一轮（Codex）：`docs/updates/2026-10-26-automation-integration.md`；上线手册 `docs/automation-rollout.md`；公众号管理员指引 `docs/wechat-setup-guide.md`。

### 本轮修复（均已实测）
1. 生产 SQL Server 缺 `WeChatSyncRecord` 时**首页 500**（中文错误信息漏判）→ 统一按错误号 208（`f292f05`，附反射构造中文 SqlException 的回归测试）。
2. 后台组织架构/在售作品/合作伙伴缺表 500 → `_SchemaMissing` 提示页（`f292f05`）。
3. 执行种子 SQL 后首页轮播少 4 张 → 只有勾选「接管轮播」的推荐才接管（`f292f05`）。
4. 专题富文本积木加载为空、保存/重排清空正文（`abac36d`）+ 积木编辑器易用性。
5. 公众号真实长链接导入失败（误删 `chksm` 被跳人机验证）（`2eb196f`）。
6. 出版著作无数据源 → 新 `ArtistBook` 模块（`98b7c44`，SQL 已并入汇总）。
7. 后台菜单分组、首页推荐表单图片上传（`b3e898d`）。

### 验收证据（本机 `artifacts/verification/claude-2026-10-26/`，gitignored）
- 生产只读实例（真实 SQL Server，新表全不存在）爬取 239 页、灌入 T3+T2 种子的隔离镜像 240 页：零 500。
- 首页 6 个区块 + 关于我们：回退路径 vs 种子数据路径逐字节一致；轮播接管三态正确。
- 16 个公开路由 × 390/1440 无横向溢出；专题积木 GUI 闭环；2 篇真实群发文章导入并与旧手录版版式一致。

### 交付包（本机，未部署）
- `artifacts/szaipa-automation-1f51e81-win-x64.zip`：**27,117,885 B**，SHA-256 `c22f9e1857ff3a253376661b26e907a49e22ee047c950504c81e2ab97ec0b90e`（旁有 `.sha256`）。
- 目录 `artifacts/publish-win-x64-1f51e81/`，清单 `artifacts/publish-win-x64-1f51e81.manifest.json`：217 文件、42,982,704 B，revision 已记录；审计通过（无凭据/Content/App_Data/镜像库）。
- win-x64 包在 Apple Silicon 上不能直接运行（架构不同，预期）；已用同源码的 portable 发布冒烟：首页/新闻/关于/展会列表/后台登录 200，后台未登录 302。
- SQL：`docs/sql/2026-10-26-automation-deploy.sql`（七份来源，SQLCMD 模式）与 `-automation-rollback.sql`。**未在真实 SQL Server 执行**，由用户执行。旧候选包 `szaipa-automation-128a585-*` 作废。

### 仍待用户 / 下一位 agent
1. 用户按 `docs/wechat-setup-guide.md` 配置 AppID/AppSecret/IP 白名单 → 运行 `wechat-probe` → 按结果开关来源、`WeChat__Enabled`；用链接导入补齐群发历史；上线时设 `WeChat__LegacyNewsMaxId`。
2. 用户在生产执行 deploy SQL、按手册部署 Windows/IIS（agent 不碰服务器）。代码已能在 SQL 之前部署而不 500，但仍建议先 SQL。
3. 可选优化：「会员风采」页无后台排序/隐藏；`_SchemaMissing` 文案「前台照常显示原有内容」对出版著作模块略不贴切；后台表单顶部空校验条（Exhibition 等表单既有现象）。

### 本机续测
- 主镜像 `.local-db/`：保留样本 Artist 1053、Works 1143/1144、Publication 2（专题）、News 1126/1127（真实公众号导入）。重跑 mirror-sync 会丢失。
- 上传安全副本：`/var/folders/jr/yvdkbjjd7czcsljyhxtvf96r0000gn/T/szaipa-epic-assets-w0anexd9/Content`（勿写 web24.05/Content）。
- 启动：`LocalMirror__Enabled=true LegacyAssets__ContentRoot=<上述路径> ~/.dotnet/dotnet run --project src/Szaipa.Web --urls http://127.0.0.1:5070`，后台 debug/debug。
- 种子对照工具（可复跑）：`artifacts/verification/claude-2026-10-26/{seed2sqlite.py,cmp.py,crawl.py}`。

### 可粘贴给下一位 agent（Claude Code / Codex 通用）
```text
接手 szaipa2002「后台自动化」Epic 收尾。先读 docs/AGENT_SPEC.md → PROJECT_MAP.md → TASKS.md → HANDOFF.md 顶部最新交接。T0–T5 代码与验收已完成，648 测试绿，交付包 1f51e81 见 HANDOFF。剩余主要是：用户配好公众号凭据后运行 wechat-probe 并按结果启用接口来源；协助用户执行生产 SQL/部署时只给步骤，不碰服务器。生产只读，写只在 SQLite 镜像；上传用 HANDOFF 中的临时 Content。改代码后需重建发布包并更新 HANDOFF。中文 commit 与 docs/updates 文档。5 小时额度剩 5% 时停下更新交接。
```

---

## 上一轮交接（2026-10-26，Codex 集成，已被上方覆盖）

### 当前基线
- 集成分支 `codex-refactor-dotnet-modernization`；**T0–T4 全部已合入**。不要再按下方旧交接重复合并 WIP 分支。
- 主要合并：T1 `883535f`，首轮全量重试 `224132f`；T4 `c2748aa`；T2 `da166c4`。补修：旧列 INSERT 降级 `4312776`、展讯顺序回显 `c5a0363`、拖拽持久化 `5c63175`。
- 本轮功能/发布准备基线 **`128a58528ba462b897834a38be5a661694a80c4a`**。之后的提交仅补交接、文档和产物审计必需文件清单，不改变应用行为。
- 前端 build 成功；.NET build **0 warning / 0 error**；**639 测试绿（Data 367 + Web 272）**；发布审计 Python **6 测试绿**；SQL 汇总 `--check` 一致。
- 完整记录：`docs/updates/2026-10-26-automation-integration.md`；上线手册：`docs/automation-rollout.md`。T5 仍是进行中，不能标全 Epic 验收完成。

### 已实际验证
- 主镜像 14 模块真实 HTTP CRUD，测试记录已清理；隔离镜像专题五种积木、清洗、错误 JSON 事务回滚、首页推荐排序/显隐/近期过往通过。
- 旧 schema 副本公开路由与旧后台 GET 均不 500；缺列的展览 POST 曾失败，已修复且 HTTP 复验返回升级提示、没有新增行。
- 浏览器：Artist 头像裁切/黑白/原图重裁、两件作品批量上传、拖拽刷新后排序保留、显式保存、配色实时预览、兼容展讯选择及顺序回显通过。OrgMember 默认黑白裁切也补验通过，灰度 432×432，原图保留。
- 正式公开路由 14 个 ×390/1440 共28组无溢出；四块专题两宽度通过。修复专题 header 零宽导致首屏空白。
- 微信：SQLite 导入闭环、隐藏/置顶/旧详情保留等测试通过；真实样本链接返回微信“环境异常”验证页，**未完成真实来源导入**，不能绕过验证。

### 候选发布包（只在本机，未部署、未推送）
- `artifacts/szaipa-automation-128a585-win-x64.zip`：**27,078,994 B**。
- SHA-256：`77c5663611dc52f74faccb326e53d004a29059c873b81a43c5308d169cfd73b2`，旁有 `.sha256`。
- 目录 `artifacts/automation-win-x64/`；清单 `artifacts/automation-win-x64.manifest.json`：216文件、42,878,186 B，完整源码SHA已记录；ZIP CRC及逐文件SHA均核对通过。
- `dotnet publish -c Release -r win-x64 --self-contained false -p:UseAppHost=false` 成功，IIS web.config 正确，禁止文件检查通过。保留SQLite相关2,590,912 B；Production硬禁镜像，不手删依赖。
- SQL：`docs/sql/2026-10-26-automation-deploy.sql`（六来源汇总，必须SQLCMD模式，核对目标库）与 `...-automation-rollback.sql`（默认只读盘点，有新增数据拒绝拆除）。**未在真实SQL Server执行**；用户自行执行生产SQL。

### Claude 下一步（按优先级）
1. 浏览器实操专题后台：富文本编辑后新增/上下移/删除，再保存，确认内容不丢；目前有代码修复、事务/HTTP验证，尚缺这一GUI闭环。
2. 将 T3 种子与 T2 推荐种子同时灌入隔离镜像，对比首页/About数据路径截图；补齐剩余后台CRUD边界与正式路由、重要展览/新专题交互检查。不要把已有28组的无溢出等同完整视觉验收。
3. 用户填好公众号凭据/IP白名单后，跑 wechat-probe，按可用来源真实导入与回填。每次进程启动先全量发现，失败保持全量重试，成功后增量；去重跳过人工编辑，重新抓取会覆盖。
4. 完成验收后重新生成以最终源码SHA命名的包/清单；现包是候选。Windows/IIS与SQL Server验收由用户执行，agent不碰服务器。

### 本机续测与边界
- 主 `.local-db/` 已按最新模型重建（日志只取近7天）。保留演示样本：Artist **1053**，Works **1143/1144**，Publication **2**（四块、folder `epic-ui-topic`），仅本地镜像。重跑 mirror-sync 会丢失样本。
- 上传安全副本：`/var/folders/jr/yvdkbjjd7czcsljyhxtvf96r0000gn/T/szaipa-epic-assets-w0anexd9/Content`（APFS独立克隆），不要写 `web24.05/Content`。
- 本轮主测试服务5070与浏览器已关闭；子agent测试服务也已关闭。续测用 `LocalMirror__Enabled=true LegacyAssets__ContentRoot=<上述临时路径> ~/.dotnet/dotnet run --project src/Szaipa.Web --urls http://127.0.0.1:5070`。若临时目录被清理，请重新独立克隆资源。
- 本地证据 `artifacts/verification/`：route matrix、CRUD JSON、裁切/专题截图。`/tmp/szaipa-epic-http-crud.py` 是可复跑临时HTTP脚本，仅允许本机镜像写入。
- 用户的 `szaipa2022/`、`.vs/`、`packages/` 与未跟踪 newsletter 文档均未 add/还原；不 `git add -A`，不改旧站。

### 可粘贴给 Claude
```text
接手 szaipa2002 后台自动化集成收尾。先读 docs/AGENT_SPEC.md → PROJECT_MAP.md → TASKS.md → HANDOFF.md 顶部最新交接。T0–T4已合并，639测试绿，别再合旧WIP。先实测专题积木GUI编辑/重排保存，再灌T3/T2种子做数据路径视觉验收，补剩余T5。候选包/校验信息见HANDOFF；验收后再以最终SHA重建包。生产只读，写只在SQLite镜像；上传用交接中的独立临时Content。真实微信页被环境验证拦截，须用户配置凭据/IP白名单后按可用接口复验，不绕过验证。不部署、不执行生产SQL、不推送、不碰旧站和用户改动。中文commit、更新交接与看板。
```

---

## 上轮交接（2026-10-26，Claude 后台自动化 WIP，现已由下述 Codex 集成覆盖）

### 已完成并合并到集成分支 `codex-refactor-dotnet-modernization`
- 规范与看板：`docs/AGENT_SPEC.md`、`docs/TASKS.md`（用户决策表在看板顶部，**不要再问一遍**）。
- **T0 本地镜像库**（merge of f1b854c）：`mirror-sync` 只读拷生产 → `.local-db/*.db`；`LocalMirror__Enabled=true` 启动后前后台都连镜像，后台 `debug`/`debug`。详见 `docs/updates/2026-10-26-local-mirror-db.md`。
- **T3 组织架构/首页静态块**（merge of e79f4b6 等 4 个 commit）：`OrgMember`/`SaleWork`/`Partner` + 后台 3 模块 + 首页 #section05/#section05guwen/#section5to6 与 About 合作伙伴数据化，缺表回退原硬编码。SQL：`docs/sql/2026-10-26-org-and-home-blocks.sql`。详见 `docs/updates/2026-10-26-org-and-home-blocks.md`。
- 合并后状态：build 0 error，测试 **422 全绿**（Data 155 + Web 267）。

### 进行中（各在独立 worktree 分支上，未合并）
| 任务 | 分支 | worktree 路径 |
|---|---|---|
| T1 公众号同步 → News | `worktree-agent-a27f15c31f63d6375` | `.claude/worktrees/agent-a27f15c31f63d6375` |
| T2 展览自动化 + 专题积木 | `worktree-agent-a47abf2973a8fa0ec` | `.claude/worktrees/agent-a47abf2973a8fa0ec` |
| T4 艺术家官网 + 头像裁切/黑白 | `worktree-agent-a2b2e509401a8294c` | `.claude/worktrees/agent-a2b2e509401a8294c` |

三个 agent 均已提交 WIP（commit 标题 `WIP(Tn):`，正文含已完成/未完成/下一步）：T1 af877f7、T2 c87c37c、T4 8e88857。接手时**先逐个检查**：
1. `git log --oneline codex-refactor-dotnet-modernization..<分支>` 看提交；读最新 WIP commit 正文。
2. `git -C <worktree路径> status --short` 看是否还有未提交改动（额度中断时可能有）；有则先审阅再提交到该分支。
3. 每个分支：`git merge codex-refactor-dotnet-modernization`（拿到 T0+T3）→ build/test → 按 TASKS.md 对应卡片补完剩余步骤 → 合回集成分支。

### 各任务已知要点（接手者必须知道）
- **生产 Publication 表是 0 行**；前台展览全部来自 `LegacyPublicationGalleryCatalog` 兼容目录（固定 id 92001–92015）+ 三个手写专题页 + 首页硬编码。T2 的「首页推荐」必须能指向兼容目录里的展览和三个手写页 URL，并在执行/未执行 slug 迁移 SQL 两种状态下都正确。
- **T1 公众号现实**：主体「其他组织」、**未认证**；有 AppID/AppSecret、IP 白名单，token 正常（2000 次/天）。要同步的是**群发**文章；freepublish 只含「发布」记录，与群发历史不相通。方案＝批量链接导入（主通道，补全历史）+ freepublish/永久图文素材接口（按能力启用）+ 群发完成事件回调（默认关，启用会使编辑模式自动回复/菜单失效）+ `wechat-probe` 探测命令；去重键 = 文章页解析出的 biz+mid+idx。**文章填入现有 News 表与现有版式**（详情左正文、右近 3 期推荐），不照搬公众号整页；旧手录新闻列表隐藏、URL 保留。
- **T4 裁切组件**：`wwwroot/admin/image-cropper.js`（Cropper.js + canvas 灰度，原图另存），T4 合并后要由集成者接到 T3 的 OrgMember 头像表单（`Areas/Staff/Views/OrgMember/_Form.cshtml`，T3 用的是 `upload-field` + `home-block-form.js`）。
- **T1 状态（WIP af877f7，已含 T0 合并，Program.cs 冲突已解：mirror-sync 与 wechat-probe 并存）**：生产代码全部完成（639ea67）——落库现有 News（Subtitle←摘要、Autor、Date←页面 ct、original=false+link=原文，与后台「链接非空即转载」一致；封面存 `newsImg/wx-{hash}`、正文图 `WeChat/{yyyy}/{MM}/`）、`WeChatSyncRecord`+`docs/sql/2026-10-26-wechat-sync.sql`、链接批量导入（≤50 条/次，去重键 `mp:{biz}:{mid}:{idx}`）、freepublish/material 两个接口来源（独立开关、只发现链接、错误码转提示、stable_token 缓存）、默认关闭的 `/wechat/callback`、`wechat-probe`、BackgroundService（首轮全量回填）、`/Staff/WeChat` 后台、前台可见性（`WeChat:LegacyNewsMaxId` 隐藏旧新闻、按 Id 详情不过滤）、自写 tokenizer 清洗（无新 NuGet）。68 项新测试已单独跑绿；**`WeChatSyncServiceTests` 写完未编译、全量 test 未跑**。剩余 7 步在 WIP commit 正文与该 agent 报告中：修 SyncService 测试（`ImportOrderedPinAsync` 空占位、Env.Respond 路由改精确匹配）、补 Web 测试（ImageStore/Callback 验签/Probe 输出）、全量 test、update 文档（含给用户的推荐操作与回调风险）、镜像端到端（真实链接导入、`WeChat__LegacyNewsMaxId` 验证）、隐藏/置顶审计。已知：接口来源不更新已存在的 key（只能「重新抓取」，会覆盖人工修改）；来源状态在进程内存，重启清空。
- **T2 状态（WIP c87c37c，已含 T0 合并）**：build 0 error、465 测试绿。已完成：状态计算器 `PublicationLifecycleCalculator`（新语义：`Status` true=按日期自动、false=强制已结束、null=下架；依据 legacy 首页「近期/过往」三态用法与 slug SQL 全写 0 的事实）、PublicationList 按状态分组并并入 14 个兼容展览、首页推荐独立表 `FeaturedExhibition`（靠 LinkUrl，可指向手写专题页）+ #section02/#section07 数据化、专题页 `PublicationBlock`+`_ExhibitionTopic.cshtml`+`exhibition-topic.css`、`RichTextSanitizer`、缺表探测改按 SQL 错误号 207/208（生产错误信息是中文，原英文匹配会漏——顺带修了旧的 Type/ExhibitionWork 探测）。生产只读实测首页/展览页字节级一致。**剩余 7 步在 commit 正文**：后台展览列表/表单接线、`block-manager.js` 块编辑器、首页推荐后台模块、封面回退、SQL 脚本（含 4 条推荐种子）、文档、镜像端到端。
- **T4 状态（WIP 8e88857）**：只完成数据层（`Works.Sort`、`Artist.HideDefaultExhibitions`、新表 `ArtistPublication`、`ArtistSiteAdminRepository`、`AdminAssetStorage` 原图存取、cropperjs@1.6.2 依赖），build 0 error 但**测试未跑**；视图/JS/裁切组件/总览页/Art.cshtml/SQL/文档/单测都未做，8 步剩余清单在该 commit 正文里。⚠️ 风险：`Works.Sort`、`Artist.HideDefaultExhibitions` 是实体列，**未执行 SQL 的后台库上 Works/Artist 模块会报错**——补完时要么给后台写路径也加缺列降级，要么在上线手册里写明「必须先执行 SQL 再部署」。该 worktree 的 node_modules 是真实拷贝（含 cropperjs），不是软链。
- 合并任何带新实体的分支后**重跑 `mirror-sync`**（镜像表结构来自 admin 上下文）。T3 的三张表在镜像里是空表，走数据路径需灌种子（SQLite 转换规则见 T3 update 文档）。
- `admin.css` 冲突不要手解，合并后重跑 `cd src/Szaipa.Web && npm run build`。
- 主检出目录里 `szaipa2022/`、`.vs/`、`packages/`、`docs/updates/2026-08-04-newsletter-subscribe-partial.md` 是用户自己的未提交改动，**不要 add、不要还原**。

### 之后的 T5（集成与上线就绪）
见 TASKS.md T5：全量端到端走查（镜像库，前台全部正式路由 × 390/1440，后台全部模块 CRUD）、汇总本 Epic 的 `docs/sql/*2026-10-26*` 为一份按序上线脚本 + 回滚脚本、`dotnet publish -c Release` 产物检查（SQLite 原生库是否需要瘦身——镜像模式在 Production 禁用）、写上线切换手册（WeChat 凭据/IP 白名单、`WeChat:LegacyNewsMaxId`、可写连接串、ContentRoot、DataProtection、qqwry.dat、执行顺序、验证清单、回滚）。**交付＝发布包 + 手册，用户自己部署，agent 不碰服务器。**

### 用户待办
- 公众号凭据：在 Mac 上 `cd src/Szaipa.Web && dotnet user-secrets set "WeChat:AppId" …` / `"WeChat:AppSecret" …`（不要贴到聊天）；跑 `wechat-probe` 前把本机公网 IP 临时加入公众号 IP 白名单（或部署后在服务器上探测）。
- 上线前在生产执行本 Epic 的 SQL（T5 会汇总成一份）。
- **已确认（用户 2026-10-26）**：「漫MAN」结束日期以 **05.28** 为准（首页原文正确）。需修正：`Services/LegacyPublicationGalleryCatalog.cs` 里漫MAN 的 EndDate 与 `docs/sql/2026-07-09-publication-slug-migration.sql` 中对应行的 05.30 → 05.28；T2 首页推荐种子保持 05.28。

### 粘贴用 prompt（新 session，Claude Code / Codex 通用）
```
你接手「szaipa2002」官网的「后台自动化」Epic。仓库 /Users/arthur/Project/GitClone/szaipa2002，集成分支 codex-refactor-dotnet-modernization。
先按顺序读：docs/AGENT_SPEC.md（硬约束）、docs/HANDOFF.md 顶部「最新交接」、docs/TASKS.md（含用户已确认的全部决策，不要再问）、docs/PROJECT_MAP.md。用中文给我简短汇报现状后开工。

本轮目标：
1. 逐个接手 T1/T2/T4 三个未合并分支（HANDOFF 表格里有分支名和 worktree 路径）：检查 WIP 提交与未提交改动 → 合入集成分支最新内容 → 按 TASKS.md 卡片补完剩余步骤 → build 0 error + 全部测试绿 → 合并回集成分支。T4 合并后把头像裁切组件接到 OrgMember 表单。
2. 然后做 T5：镜像库全站端到端走查、SQL 汇总成上线/回滚脚本、Release 发布包检查、上线切换手册。

约束：~/.dotnet/dotnet build|test Szaipa.Modernization.slnx；前端 cd src/Szaipa.Web && npm run build。生产库只读，写只在本地 SQLite 镜像（mirror-sync），生产 SQL 写进 docs/sql 由用户执行；不碰服务器/IIS；不改 szaipa2022/；上传测试用 LegacyAssets__ContentRoot 指向临时目录，不写 web24.05/Content。中文 commit（写细）+ docs/updates 中文文档 + 更新 TASKS.md 状态。可用子 agent 并行（最多 4 个，用 Sonnet）。额度将尽时停下，按 AGENT_SPEC §8 更新本交接。
```

---

## 历史交接（2026-07 之前的迁移阶段）


## 现状（已完成的核心，均 build 0/0 + 252 测试绿）
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
- **本地只读库状态（2026-07-11，后续已兼容）**：Szaipa/Tongou 只读连接均可用，真实首页读取成功；`AllowLiveDatabase=false`。当前库没有 Publication 92001–92015 行，2026-07-25 起由应用内只读兼容数据补齐这 14 个已结束展览，不再阻塞前端退役。
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
- **Bootstrap 分页退役试点（2026-07-22，2026-07-26 扩展）**：NewIndex/NewVip/PublicationList/NewNews/NewAbout/NewArt/Publication 不再加载 143,947B Bootstrap CSS，改用带版本的 3,105B 兼容基线；每个冷页面原始体积少 140,842B。原六页 320–1440 共 81 个响应式检查通过；Publication 再经 390/1440 严格 A/B 与 991/992 断点复核，几何/关键 computed style 一致、导航单行、无横向溢出。其余页面默认仍加载 Bootstrap。详见 `docs/updates/2026-07-22-bootstrap-optout-pilot.md`。
- **三个特殊展览 Bootstrap 退役（2026-07-26）**：chunyu3/tonggou2/tonggou2024 也直接改载 3,105B 轻量基线；每页冷加载少 140,842B 原始 / 19,627B gzip CSS。首轮 A/B 捕获 `.nav` 上移 10px 后把真实 `margin:0` 依赖收回页面 CSS，三页×390/1440 复测的关键几何/样式完全一致；三页×320/991/992 无溢出，导航保持横向单行，移动订阅字号保持既有 22.5–32px。服务器、旧发布版、Content 均未改。详见同一 Bootstrap 试点文档。
- **特殊展览首屏图片优先级（2026-07-26）**：三页首屏下方 84–90px 的品牌/装饰图不再错误标记 high，六张统一 lazy/async/low，让 267–781KB hero 保持真正首屏优先级。三页×390/1440 属性与几何验证通过、无溢出；新增 1 项契约测试，总测试 245→246。详见 `docs/updates/2026-07-26-special-publication-image-priority.md`。
- **公共滚动逻辑缓存化（2026-07-26）**：公共 navbar 收起和新闻详情返回顶部从重复内联 HTML 外提为两个带内容哈希的 defer 小脚本；跨页面/多文章访问可复用缓存。NewAbout 向下/向上 transform 与 NewNewsRead 500px 阈值、点击回顶均通过前后行为对照。新增 1 项契约测试，总测试 246→247。详见 `docs/updates/2026-07-26-public-scroll-runtime-cache.md`。
- **公开站 canonical 切换（2026-07-26）**：现代页面已从 `NewIndex/NewNews/NewVip/NewAbout/NewArt` 改为正式 `Index/News/Vip/About/Art`，布局和 CSS/JS 同步去掉 `new` 迁移命名；`new*` 仅保留永久重定向。旧 `ArtNews/ArtNewsRead` 的完整资讯正文、馆藏与拍卖字段先迁入现代只读仓储和 `/Home/Art/{id}/Archive`、`/Home/Art/News/{id}`，再做兼容跳转，未丢后端内容。公开迁移仪表盘、骨架视图及其模型/服务已删除。Data 100 + Web 151 = 251 全绿；390px 真实页无横向溢出。详见 `docs/updates/2026-07-26-public-canonical-cutover.md`。
- **公开视图迁移复核（2026-07-28）**：补迁完整协会章程与 `/Home/Constitution`，修复首页相对链接；`link/newlink/newConstitution` 改为 canonical 301。重新以新版导航、线上内部链接、旧控制器和 IIS 审计划分迁移/空壳/500/退役页面，确认 `Home`、`Publication` 与完整 `Areas/Staff` 都已在现代目录。白名单快照约 22 MiB，在不含 `szaipa2022` 的临时目录恢复并跑完 Data 100 + Web 152 = 252；目标 `szaipa2026` 尚待用户选择是否保留初始 README 历史。详见 `docs/updates/2026-07-28-public-view-migration-audit.md`。
- **前台/Staff 字体清单拆分（2026-07-22）**：原 147 个 `@font-face` 的单一清单拆成 public 114 / Staff 88，交集只保留双方需要的 55 个 Sans 声明；125 个 WOFF2、字形、字重、哈希和 `unicode-range` 均未改。每个页面只下载自己的清单：public 原始 CSS 少 80,909B（gzip -58.88%），Staff 少 136,438B（gzip -63.42%）。11 个路由×宽度组合及 Staff 仪表盘/编辑器实页验证无几何回退，测试 219→221。详见 `docs/updates/2026-07-22-font-manifest-split.md`。
- **NewArt 运行时/横幅审计（2026-07-23）**：移除视图中不存在的 `.mySwiper2` 初始化，并把 Swiper、Magnify loader、页面脚本改为保序 `defer`，避免 135,660B Swiper 阻塞尾部 HTML 解析。主横幅的 AVIF 原型未达无损收益门槛，故不加入派生资产、不替换原图。1440 与 390 实页均保持首屏几何、单行导航和零横向溢出；测试 221→222。详见 `docs/updates/2026-07-23-newart-runtime-and-banner-audit.md`。
- **首页运行时延后（2026-07-23）**：`NewIndex` 的 Swiper、Magnify loader、页面脚本改为保序 `defer`，与 NewArt 一致；保持依赖顺序，避免 `135,660B` Swiper 在页面尾部阻塞 HTML 解析。新增契约测试，测试 222→223。
- **展览旧库兼容（2026-07-23，2026-07-25 完成缺行回退）**：`Publication.Type/Preface/Signature` 与 `ExhibitionWork` 均按增量能力读取；旧库缺列/表时回退为普通画廊。固定 ID 92001–92015 缺行也已由只读兼容目录补齐，数据库真实行始终优先。详见 `docs/updates/2026-07-23-publication-legacy-schema-compatibility.md`。
- **退役展览精确图库目录（2026-07-25）**：14 个固定 Publication ID 通过小型只读目录提供页面元数据并复用旧页面的 604 张精确路径，数据库有行时始终以数据库为准；主图/缩略图不再假设连续编号，不复制、不重命名、不重压外置 Content，并修正 zhongfa 旧页面已有的一处错误文件名。迁移 SQL 保留为未来可选的事务化数据归一化工具，当前运行不再依赖数据库写入。测试 225→244。详见 `docs/updates/2026-07-25-publication-gallery-catalog-and-migration-safety.md`。
- **数据展览图库分层加载（2026-07-25）**：普通/重要两套共享画廊复用现有 q30w1200，14 个退役展览精确命中 344/604 张；缩略图永久使用预览，主图仅在图库接近视口后恢复 active/prev/next。命中集合的缩略表示 182,939,793→17,753,081B（-90.30%），未生成或修改图片。131 图最大页初始只恢复 3 张原图，切换时按需再恢复 1 张；桌面/手机无溢出和错误。详见 `docs/updates/2026-07-25-publication-gallery-layered-loading.md`。
- **公共 Swiper 按模块裁剪（2026-07-25）**：保持原 Swiper 9.0.3，只打包实际使用的 A11y/Autoplay/EffectCoverflow/FreeMode/Keyboard/Navigation/Pagination/Thumbs；JS 135,660→88,156B，CSS 17,863→13,029B，冷页面 gzip 合计约再少 12KB，并改为内容哈希一年缓存。首页、NewArt、普通/特殊展览桌面和手机实页通过，测试 244→245。同期 2400px NewArt Banner AVIF 试验因高质量档无体积收益、低档 SSIM 仅约 0.94 被拒绝，未加入派生图。详见 `docs/updates/2026-07-25-public-swiper-module-bundle.md`。
- **特殊展览图库分层加载（2026-07-20）**：三页 201 张现场图的主/缩标记收敛为同一数组；170 张已有 q30 先显示预览，图库接近视口后只预载 active/prev/next 原图，理论首轮少传 50,506,952B。三页统一共享 JS，退役重复的 tonggou2024 脚本。详见 `docs/updates/2026-07-20-special-gallery-layered-loading.md`。
- **高质量首页首图（2026-07-20）**：现代仓库内置 1080×791 AVIF，首页当前 LCP 从 1,783,805B 降到 167,097B（约 -90.63%），原 `/Content` 图保留为 fallback；新增严格 allowlist 派生解析器。NewArt 空 Path 不再拼 Banner 目录/产生 404；三张现有全屏 Banner 经审计后因高 DPR 画质风险暂不强换 1600px 版本。总测试 144→181。详见 `docs/updates/2026-07-20-high-quality-derived-images.md`。
- **独立现代仓库契约（2026-07-20）**：现代跟踪边界约 20.4MiB，旧 Content 约 1.1–1.2GiB 继续作为外部共享卷而非删除；当前约 1.8GiB Git 历史不进入新仓库，改从脱敏后的干净 commit 导出现代白名单并建立全新 root。Data Protection 使用固定 Production ApplicationName、外部持久 KeysPath、Windows machine-scoped DPAPI 与启动自检；Staging 使用独立 key/cookie/hostname。用户已创建 Gitee 目标仓库及本地 `~/Project/GitClone/szaipa2026`，目前只有初始 README，尚未导出现代源码；本轮 UI 人工验收前不得填充，也未改服务器/IIS/hook。由于远端已有初始 root，最终需用户决定是经明确授权替换 `master` 以保持单 root，还是接受两 commit 偏差。详见 `README.md`、`docs/repository-split.md`、`docs/updates/2026-07-20-independent-modern-repository.md`。
- **未来发布包瘦身（2026-07-13）**：禁用当前 `UseStaticFiles` 不会选取的 SDK `.br/.gz` 发布副本，并排除 Node 清单、示例配置和 `.gitkeep`；本地对照实测少 761,732B，发布进程仍正确协商 Brotli/Gzip。仅影响未来本地生成包，服务器未部署/改动。详见 `docs/updates/2026-07-13-publish-payload-trim.md`。
- **旧凭据清理（2026-07-12）**：旧 MVC Web.config 与跟踪中的 bin 配置副本已改为部署占位符；历史密码仍必须在数据库服务器轮换。详见 `docs/updates/2026-07-12-legacy-credential-sanitization.md`。

## 剩余工作（多为既有模式复制，适合便宜模型）
1. ~~**展览「参展作品目录」**~~ **2026-07-09 完成**：新增 `ExhibitionWork` 实体 + works-manager.js 管理器（分类/标题/艺术家/尺寸/材质/图 + 排序），集成进 `_ExhibitionImportant.cshtml`。详见 `docs/updates/2026-07-09-exhibition-works-catalog.md`。
2. ~~**slug 页退役**~~ **2026-07-25 完整完成**：14 个简单 slug 页已改为固定 Publication ID + 301，硬编码视图已删；chunyu3/tonggou2/tonggou2024 保留，zengfeng 已退役。14 组页面元数据和 604 张图片由只读兼容目录提供，数据库真实行优先，因此无需本地可写副本、无需改动外置 Content。SQL 仅保留为未来可选归一化工具。
3. ~~**Phase 6 加固**~~ **2026-07-09 完成**：审查授权/anti-forgery/操作日志覆盖，均未发现遗漏；与 legacy 比对校验规则，排查的疑似缺口均核实排除；补测试 82→94。详见 `docs/updates/2026-07-09-phase6-hardening.md`。
4. ~~**可选增强：独立的全量操作记录页**~~ **2026-07-12 完成**：`/Staff/Operations` 按日期分页查看完整历史，仪表盘保留近 7 天 feed 并链接完整页。详见 `docs/updates/2026-07-12-operation-history-page.md`。
5. ~~**前台图片继续优化**~~ **当前安全范围已完成**：静态/动态卡片图、NewArt 作品预览、三个特殊展览和数据展览图库分层加载、首页 LCP 高质量 AVIF 均已完成。NewArt 全屏图的 1600px 与 2400px 试验都未达到画质/体积门槛，候选均未提交；除非以后取得艺术家高质量源图，否则不再二次有损压缩现有 JPEG。
6. **新需求（用户 2026-07-09 提出，尚未开工）**：微信公众号接口对接——新闻页面自动抓取公众号最新文章，格式化后新增到网站。需要先确认：走微信官方素材/草稿箱接口（需公众号是服务号+已认证、有对应 API 权限）还是第三方抓取方案；抓取节奏（定时轮询 vs webhook）；写入哪张表（News？新建 WeChatArticle？）；图片/图文消息里的媒体资源怎么落地到 `/Content`；去重与增量更新策略。
7. **前台 Legacy 清理**：生产数据库和 IIS 日志审计均已完成；`Award` 与 `layui` 只是审计候选。服务器当前只供参考，不移动、不删除、不部署；若未来确有必要，必须先向用户说明并确认。其余候选根有真实访问，继续保留。公众号需求继续后置。
8. ~~**992–1279 窄桌面字号**~~ **2026-07-20 完成**：用户确认采用连续缩放；根字号与两侧统一为 16px，navbar 用 `rem + vw` 从 992 的 15px 平滑接到 1280 的 24px。991/992/1024/1199/1200/1279/1280 及主要前台实页均无横向溢出或换行。

## 待用户本地操作
- **本地只读登录检查（推荐，不写库）**：`appsettings.Local.json` 配 `AdminWrite:{EnableWrites:true, UseReadOnlyConnectionForDebug:true}`（Tongou 同），不设 `ConnectionStrings:SzaipaAdmin`——admin 上下文复用只读 `szaipa_ro` 连接，能登录/看仪表盘/浏览，写操作被 SQL 层挡掉。前提：`LegacyData:Szaipa` 的只读凭据当前有效（2026-06-24 实测遇到 `18456` 认证失败，需核对密码）。详见 `docs/updates/2026-06-24-admin-readonly-debug-login.md`。
- **发布环境真实写入**：`AdminWrite:EnableWrites=true` + `ConnectionStrings:SzaipaAdmin`（真正可写副本）。写入推迟到发布/部署环境，agent 永不写库。
- 若要点测 Tongou 写：再配 `TongouAdminWrite:EnableWrites=true` + `ConnectionStrings:TongouAdmin`（Tongou 可写副本，与 Szaipa 是两个库）。
- 可写 `LegacyAssets:ContentRoot`（上传图片落盘需要）。
- 跑 `docs/sql/2026-06-exhibition-template-columns.sql`（给 Publication 加 Type/Preface/Signature；重要型渲染需要）。
- 可选：未来若决定把 14 个只读兼容展览纳入 Staff 后台管理，再在隔离的本地可写副本上先跑 readiness、后跑事务化 slug migration；当前页面运行不依赖这一步。
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

约束：构建/测试用 ~/.dotnet/dotnet build|test Szaipa.Modernization.slnx（须 0/0 + 全绿，当前 252）；前端 cd src/Szaipa.Web && npm run build。DB 只读、绝不用生产凭据、不写 Windows 连的库；admin 写本地副本。Windows 服务器及旧发布版只供参考，不部署、不改 IIS、不移动/删除发布文件；确需服务器变更时先停下说明并取得明确确认。没有可写库时只做代码+单测+路由冒烟，端到端留给用户本地。边做边验证、遇 legacy bug 顺手修。

加 admin 模块照 News 范本：仓储(SzaipaAdminContext)+控制器(Areas/Staff)+_Form/Index/Create/Edit 视图+导航；艺术家子模块用泛型基类 ArtistScopedAdminRepository<T>；写操作走 IOperationRecorder。

先告诉我你打算先做哪一项、用什么模型（建议：slug 迁移/Phase 6 用 Sonnet 4.6 medium；仪表盘/作品目录/架构决策才用 Opus）。
```
