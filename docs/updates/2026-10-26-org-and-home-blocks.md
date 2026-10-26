# 更新 — 2026-10-26：T3 组织架构与首页静态块数据化

## 1. 为什么

首页「核心成员」「顾问团队」「在售艺术品」和「关于我们」的「合作伙伴」都是手写在 Razor 模板里的：
会长换届、新增一幅在售作品、换一家合作伙伴，都要改模板、发版。现在改为后台录入、页面自动渲染。

## 2. 改了什么

### 数据（三张新表，均为"追加"）

| 实体 / 表 | 用途 | 关键字段 |
|---|---|---|
| `OrgMember` | 组织架构成员 | Name、Role（职务）、Title（头衔）、GroupName（核心成员 / 顾问团队）、ArtistId（可空，关联艺术家）、AvatarPath、ImageWidth/Height、Sort、IsVisible |
| `SaleWork` | 首页在售艺术品轮播 | ArtistId（可空）、ArtistName、Title、Size、Medium、ImagePath（原图）、ImageWidth/Height、Sort、IsVisible |
| `Partner` | 关于我们 · 合作伙伴 | Name（可换行）、LogoPath、ImageWidth/Height、Link（可选 http/https）、Description、Sort、IsVisible |

- 用户决策落实：组织架构是**独立名单，可选关联艺术家**。关联 → 首页卡片链接 `/Home/Art/{ArtistId}`；未关联 → 同样的卡片、没有链接；
  头像留空且已关联 → 自动借用该艺术家的头像。
- 职务/头衔是自由文本，`OrgRoles`（会长/荣誉会长/副会长/监事/秘书长/理事/会员/顾问/策展人；艺术家/教授/评论家/考古学家/艺术顾问）
  只作为表单 `datalist` 的建议值，可随意自填。
- 排序：每个分组（OrgMember）/整表（SaleWork、Partner）内按 `Sort, Id`；后台「上移/下移」把该范围整体重排成 1..n（顺带修复并列/空洞）；
  新增的排到所在分组末尾；改分组排到新分组末尾。
- 两个上下文 `OnModelCreating` 各只追加一行 `HomeBlocksModelConfiguration.Configure(modelBuilder)` + 3 个 DbSet；DI 追加在 `SzaipaDataServiceCollectionExtensions.cs`、`Program.cs` 各一处。

### 前台（输出结构/类名与改造前一致）

- `Views/Home/Index.cshtml`：**只改** `#section05`、`#section05guwen`、`#section5to6`。人物卡片抽成 `Views/Shared/_HomeMemberItems.cshtml` 两段复用。
  在售作品仍是 `src=q30 预览（ImagePreviewResolver）` + `data-magnify-src=原图` + `class="zoom"`，`magnify-loader.js` 规则不变。
- `Views/Home/About.cshtml`：合作伙伴循环渲染；保持原有的"前两家手机端也出现 / 其余只在桌面端第一行出现并在手机端第二行只显示 Logo+名称"。
  「合作案例」是隐藏的空占位（`xs/sm/md/lg-hide` 全隐藏、无内容），**没有数据化**，保持原样。
- 数据由 `IHomeBlocksProvider`（`Services/HomeBlocksProvider.cs`）提供：与 `HomeController` 同一套"读模型是否激活"规则；未激活时直接给内置默认、不解析仓储。
  这样没有改 `HomeController` / `HomePageSnapshotModel`（它们是别的任务的热点）。
- **缺表 / 空表回退**：`HomeBlocksDefaults` 就是改造前模板里的硬编码内容（也是 SQL 种子的来源）。表或列不存在、或表里一行都没有 → 渲染默认内容，**不 500**。
  判断缺表优先看 `SqlException.Number`（208/207），因为生产 SQL Server 的报错文字可能是中文（本次实测就是「对象名 'OrgMember' 无效」）。
  缺表状态由单例 `HomeBlocksSchemaState` 记 60 秒，避免执行 SQL 之前每次请求都查库、刷 EF 错误日志。
- 有行但全部"隐藏"→ 该块**整段不输出**（不是回退默认）；表里没有某个分组的人 → 该分组整段不输出。

### 后台（照 News 范本，3 个模块）

- 仓储：`Services/Admin/SortableAdminRepository<T>`（泛型基类）+ `OrgMemberAdminRepository` / `SaleWorkAdminRepository` / `PartnerAdminRepository`，写操作全走 `IOperationRecorder`（同事务写 Diary + Staff）。
- 控制器：`OrgMemberController` / `SaleWorkController` / `PartnerController`，共用 `SortableBlockControllerBase`（上移/下移、显示/隐藏、删除，POST + 防伪），`[Authorize(Policy = StaffPolicy)]`。
- 视图：`Areas/Staff/Views/{OrgMember,SaleWork,Partner}/{Index,_Form,Create,Edit}.cshtml` + 共用操作列 `Shared/_SortableRowActions.cshtml`；导航「内容管理」下拉追加三项。
- 成员表单：**关联艺术家下拉 +「一键带出头像」**；图片复用 `upload-field.js`（`data-store="url"`，存完整 `/Content/...` 路径），新增 `wwwroot/admin/home-block-form.js`：上传/带出头像后**自动回填图片宽高**、清除图片、选艺术家自动带出艺术家名。
- 校验：图片路径限定 `/Content/` 下（拒绝外链/`javascript:`/`..`）；合作伙伴链接限定 http(s)；关联的艺术家必须真实存在。
- `wwwroot/admin/admin.css` 重新构建了（新增页面用到了几个原来没有的 Tailwind 类）。**合并有冲突时直接重跑 `npm run build` 即可，不要手解这个压缩文件。**

## 3. 小幅统一（用户允许的"视觉小幅统一"，其余像素级保持）

在售作品说明文字，原手写里：
1. 其中 4 条是游离的 `</p>`（浏览器会生成一个空 `<p>`），另 2 条才是真正的 `<p>` 包裹——所以两种条目的说明文字上边距不一致；现统一为 `<p>` 包裹（即另 2 条的样子）。
2. 《作品名》与尺寸之间，3 条没有空格、3 条有一个空格；现统一为一个空格。

Index 另外两段、About 合作伙伴在回退路径下与改造前**除了合作伙伴简介里换行被编码为 `&#xA;` 之外完全一致**（`white-space: pre-line` 下换行是同一个字符，渲染相同）。

## 4. Vip 页评估：不改

`/Home/Vip`（会员风采）列的是 **Artist 表**（全体艺术家会员，按最新在前），不是组织架构；卡片字段（中英文名 + Artist.Title）也与职务无关。
按职务分组会改变现有排序/卡片并需要引入"未在组织架构里的人放哪"的新规则，用户并未提出，所以保持现状；组织架构层级已经在首页两段呈现。
如以后想在 Vip 卡片上带出职务，可用 `OrgMember.ArtistId` 反查，`HomeBlocksReadRepository` 已有按 ArtistId 取数的先例。

## 5. 验证结果

- `build` 0 error（无新增告警，仅既有 NU19xx）；`test` 全绿 **422**（Data 155 + Web 267；合并 T0 后基线 375，本任务新增 47）。
  - Data：`HomeBlocksAdminRepositoryTests`（CRUD/排序/并列重排/换组/显隐/审计）、`HomeBlocksReadRepositoryTests`（投影、缺表降级、重试窗口、借头像/借名、"把默认内容写进表再读回 == 默认内容"）、`HomeBlocksSeedSqlTests`（SQL 种子区块与 `HomeBlocksDefaults` 同源校验）。
  - Web：`HomeBlocksContractTests`（提供器降级不碰仓储、三个控制器 Authorize + 每个 POST 带防伪、图片路径/链接校验）。
- 前台回退路径（生产只读库没有这三张表，真实启动 5062）：首页 `/`、`/Home/About` 200；日志里 `OrgMember/SaleWork/Partner` 的 208 错误被吞掉并降级，第二次请求起不再查库（60 秒窗口生效）。
  对改造前保存的 HTML 逐段比对，差异仅限第 3 节所述。
- 前台数据路径（本机镜像库，T0）：`mirror-sync` 后三张表是空表；用种子 SQL 的 SQLite 等价语句灌入后，**首页三段与 About 合作伙伴的 HTML 与回退路径逐字节相同**。
- 后台端到端（镜像库 + 调试账号，`LegacyAssets:ContentRoot` 指向 worktree 内临时目录）：
  - 上移"沈敬东"、把"杨宽"职务改为副会长、隐藏"邓国源"、新建未关联成员"测试新人" → 首页 `#section05` 顺序/职务/隐藏/无链接卡片**立即**变化；
  - 新建关联艺术家 1000 且无头像的成员 → 卡片自动用艺术家头像并链接 `/Home/Art/1000`；
  - 在售作品移动、全部隐藏 → `#section5to6` 整段消失且无新增 JS 报错；
  - 新建带链接的合作伙伴（名称含换行）→ About 桌面第一行与手机第二行都出现，链接新窗口打开；非法链接 / 外链图片路径被表单拒绝；
  - 上传头像 → 返回 `/Content/ArtImg/Org/{guid}.png` 并自动回填 320×240；"一键带出头像"、"清除头像"按钮正常。
  - 测试数据位于本地镜像 SQLite，不进仓库。
- 390px / 1440px：首页与 About `scrollWidth == innerWidth`，无横向溢出；About 手机端可见 5 家（前 2 家 + 第二行 3 家），符合原逻辑。
  （首页顶部轮播在镜像库因 Publication 为空会报 `swiperSlideSize`，是镜像数据缺失造成的既有现象，与本任务无关；生产只读库有展览数据不会出现。）

## 6. 需要用户 / 集成者手动做的事

1. **在生产库（及后台写入的库）执行** `docs/sql/2026-10-26-org-and-home-blocks.sql`（幂等；种子仅在表为空时插入；执行后页面与现在一致）。
   - 建议**先执行 SQL 再部署新代码**；顺序反过来也不会出错（缺表自动回退），只是日志会有短暂的 EF 错误噪音（60 秒一次）。
2. 图片：成员头像沿用现有 `/Content/images/to black/*` 与 `/Content/ArtImg/Artist/*`，种子里写的就是这些现有路径，无需搬文件。
   后台新上传的图落在 `/Content/ArtImg/Org`、`/Content/ArtImg/selling`、`/Content/images/partners`（`LegacyAssets:ContentRoot` 下）。
3. 镜像库（T0）验证步骤见本任务回复；合并含新实体的分支后需重跑 `mirror-sync` 才有这三张表。
4. 「详情请咨询微信:…」那一行目前是常量 `HomeBlocksDefaults.SaleContactLine`（所有在售作品共用），要改微信号需改这一处代码；后续如需按作品配置再加字段。

## 7. 已知限制

- 首页只渲染"核心成员""顾问团队"两个分组；表单里的分组下拉也只提供这两个（Vip 不按职务分组，见第 4 节）。
- 头像裁切 / 黑白处理组件由 T4 做（`image-cropper.js`），集成时再接到成员头像的 `upload-field` 上；本任务只用现有上传。
- 在售作品图片宽高靠浏览器加载预览图时回填（隐藏字段），未做服务端读图；手工 POST 不带宽高也能正常渲染（只是缺少尺寸提示）。
