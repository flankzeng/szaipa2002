# 交接（HANDOFF）

> 新 session 接手时：先读 [PROJECT_MAP.md](PROJECT_MAP.md) 和记忆目录里的各条记忆，再开工。下面的 prompt 可直接粘进新 session。

## 现状（已完成的核心，均 build 0/0 + 50 测试绿）
- **Phase 0 基座**：可写 `SzaipaAdminContext`（门控本地副本）、cookie 认证替换 Session、`Areas/Admin` 外壳、Tailwind 主题（品牌红 #bf272d，贴近 NewIndex）。
- **Phase 1**：TipTap 富文本编辑器 + 上传服务（唯一 GUID 命名，规避旧版图片误删 bug）。
- **Phase 2**：News + ArtNews 全 CRUD。
- **Phase 3（部分）**：Fav / Auction / Exhibition（泛型基类 `ArtistScopedAdminRepository<T>`）。
- **展览模块**：普通型 + 重要型（数据驱动 `Publication` + 画廊上传器 + 两套皮肤）。
- **前台优化**：NewIndex / NewNews / NewNewsRead / NewArt 内联 CSS/JS 外提到缓存文件；成员头像区 table→flex 去重复。
- **旧后台 bug**：news-add 丢正文 / newsImg 误删 已在 `szaipa2022` 源码打补丁。

## 剩余工作（多为既有模式复制，适合便宜模型）
1. **Phase 3 收尾**：Artist（~25 字段 + 富文本 + 多图，最复杂）、Works（图 + 尺寸 + 标签 + 富文本）、ArtWorks。镜像现有 admin 模块。
2. **Phase 4**：Company、Tongou(Atrist/Works) CRUD —— 简单，照搬 Fav/Auction。
3. **Phase 5**：访问分析仪表盘（`AccessData` 省市聚合喂图表）+ Index 仪表盘 + OperationRecord / PasswordChange —— 中等，需选图表库。
4. **展览「参展作品目录」**：新增 `ExhibitionWork` 实体 + works 管理器（分类/标题/艺术家/尺寸/材质/图 + 排序）—— 中等偏新，重要型可选增强。
5. **slug 页退役**：把 ~16 个 `Views/Publication/*.cshtml` 迁成 `Publication` 行后删硬编码视图、首页改链 —— 机械。
6. **Phase 6 加固**：与 legacy 比对、anti-forgery/授权审、补单测。
7. **NewIndex 进一步优化**（用户说后面单独聊，用新 session）。

## 待用户本地操作
- 配 `appsettings.Local.json`：`AdminWrite:EnableWrites=true` + `ConnectionStrings:SzaipaAdmin`（本地可写副本）+ 可写 `LegacyAssets:ContentRoot`。
- 跑 `docs/sql/2026-06-exhibition-template-columns.sql`（给 Publication 加 Type/Preface/Signature；重要型渲染需要）。
- 旧 `szaipa2022` .NET Framework 改动需在能跑该栈的机器上重新生成确认编译。

## 模型建议
核心难点已做完，剩下多是「照模式复制」的 CRUD/功能活，**不必再用 Opus 4.8 高 effort**：
- **默认 Sonnet 4.6（medium）**：Phase 3 收尾、Phase 4、slug 迁移、Phase 6。模式清晰、性价比最高。
- **Haiku 4.5**：纯机械活（如「照 Fav 加 Company 模块」）。
- **升回 Opus 4.8（high）**：仅 Phase 5 仪表盘数据可视化设计、展览作品目录 UX、dev/prod 库架构决策、棘手调试。
- **每个 phase 开新 session**（上下文短=更省更快），靠记忆文件 + 本交接 + PROJECT_MAP 续接。

---

## 粘贴用 prompt（新 session）
```
你接手「szaipa2002」ASP.NET Core 迁移项目。先读 docs/PROJECT_MAP.md、docs/HANDOFF.md 和记忆目录里的各条记忆，用中文给我汇报，再开工。

约束：构建/测试用 ~/.dotnet/dotnet build|test Szaipa.Modernization.slnx（须 0/0 + 全绿，当前 50）；前端 cd src/Szaipa.Web && npm run build。DB 只读、绝不用生产凭据、不写 Windows 连的库；admin 写本地副本。没有可写库时只做 代码+单测+路由冒烟(302)，端到端留给用户本地。边做边验证、遇 legacy bug 顺手修。

加 admin 模块照 News 范本：仓储(SzaipaAdminContext)+控制器(Areas/Admin)+_Form/Index/Create/Edit 视图+导航；艺术家子模块用泛型基类 ArtistScopedAdminRepository<T>；写操作走 IOperationRecorder。

先告诉我你打算先做哪一项、用什么模型（建议：Phase 3 收尾/Phase 4/slug 迁移 用 Sonnet 4.6 medium；仪表盘/作品目录/架构决策才用 Opus）。
```
