# 2026-07-09 展览 slug 页退役（迁成数据驱动 Publication 行）

## 背景

`PublicationController` 里 19 个手写的展览详情 action 中，除 3 个特大自定义页
（`chunyu3` 2883 行、`tonggou2` 1864 行、`tonggou2024` 1159 行，结构特殊/视觉复杂，
未抽象出模板）外，其余大多是同一套普通型模板（标题 + 中英文副标题 + 开幕/结束时间 +
swiper 双画廊）。这批已随「展览模块数据驱动」（`Publication` 实体 + `/Home/Publication/{id}`
+ `Views/Shared/_ExhibitionGallery.cshtml`）具备了迁移条件，本次把它们迁成
`Publication` 数据行，退役硬编码视图，旧 URL 用 301 永久重定向保留（SEO/收藏夹友好）。

## 范围调整：`zengfeng` 未迁移

> 2026-07-10 后续：用户确认该翻页书前端退役。现代与旧 MVC 路由现返回 410，约 140MB 专属资源已从当前分支移除并由本地 legacy 归档分支保存。

原计划的 15 个 slug 里，`zengfeng.cshtml` 读取后发现**不是画廊页**——`Layout = null`，
整页是一个独立的 HTML5 翻页书（flipbook）迷你站，嵌入
`/Content/publication/zengfeng/mobile/...` 下的一整套 CSS/JS/资源，没有
`_ExhibitionGallery` 依赖的标题/日期/图片列表结构。`Publication` 实体无法表达这种内容，
强行迁移只会丢失内容。**因此 `zengfeng` 的 controller action 和视图原样保留**，与
`chunyu3`/`tonggou2`/`tonggou2024` 一样不动。另外确认过 `NewIndex.cshtml`、
`NewArt.cshtml`、`Publication/index.cshtml` 里都没有指向 `/Publication/zengfeng` 的
链接（本来就是孤立页面），所以不影响任何前台入口。

**实际迁移 14 个页面**（预留 ID 区间 92001-92015，92004 留空对应 zengfeng，未插入）。

## 迁移表：slug → 固定 Id → FolderName

| Id | slug | 标题 | FolderName | MaxImg | 旧资源路径目录 |
|----|------|------|-----------|--------|-------------------------------|
| 92001 | tonggouEurope | 同构——欧洲行 当代艺术展 | tonggouEurope | 29 | ✅ 精确目录（100000 起） |
| 92002 | shuimai | 2023龙游水脉艺术节 | shuimai | 29 | ✅ 精确目录（保留缺号与 PNG） |
| 92003 | chunyu | 春语·当代艺术名家邀请展 | chunyu | 11 | ✅ 精确目录（01…012 混合扩展名） |
| 92004 | (zengfeng，未迁移) | — | — | — | — |
| 92005 | zhongyi | 中意艺术名画展 | zhongyi | 23 | ✅ 精确目录（10000 起） |
| 92006 | tonggou | 同构——当代艺术作品邀请展 | tonggou | 22 | ✅ 精确目录 |
| 92007 | chunyu2 | 春语第二季——国际视觉艺术邀请展 | chunyu2 | 16 | ✅ 精确目录（保留 100010 尾段） |
| 92008 | trio | 三人行——鸥洋/雷双/张岚芊艺术展 | trio | 17 | ✅ 精确目录（保留 100010 尾段） |
| 92009 | man | 漫MAN-艺术时尚先锋展 | man | 24 | ✅ 精确目录 |
| 92010 | yijia | 艺+科技新潮流展 | yijia | 69 | ✅ 精确目录 |
| 92011 | zhongri | 同构——中日艺术交流展 | zhongri | 56 | ✅ 精确目录 |
| 92012 | shuyuyi | "数"与"艺"——新文艺群体创作成果展 | shuyuyi | 130 | ✅ 精确目录（100000 起） |
| 92013 | chunyu4 | 春语第四季——当代艺术作品邀请展 | chunyu4 | 95 | ✅ 精确目录（100000 起） |
| 92014 | zhongfa | 深圳-法国国际当代艺术展2025 | zhongfa | 44 | ✅ 精确目录（含实际文件 1000016.jpg） |
| 92015 | tangqishan | 入骨相知——唐岐山当代艺术展 | tangqishan | 25 | ✅ 精确目录（100000 起） |

（`Type` 全部设为 0，即普通型模板；`Status` 全部设为 0/已结束——用当前会话日期
2026-07-09 核对，这 14 场展览的结束时间均已过去。）

## 2026-07-25：不再要求重编号旧图片

后续审计确认普通画廊的主图与缩略图原本分别从 `10001`、`10000` 起算，直接重编号仍无法
同时完整复现旧页面顺序。因此现代站新增 `LegacyPublicationGalleryCatalog`，为这 14 个固定
ID 提供旧页面实际使用的精确路径顺序；主图和缩略图共用同一列表。它保留缺号、混合扩展名和
历史命名，不复制、不重压、不重命名外置 Content。

本地 `web24.05/Content` 已逐项验证 604/604 路径存在。其中 zhongfa 的旧视图引用
`100016.jpg`，实际文件为 `1000016.jpg`；目录直接使用实际路径，顺手消除旧页面已有的这处
404。非 92001–92015 的普通/重要展览仍保持原来的数字约定，不受影响。

## 改了什么

1. **新增** `docs/sql/2026-07-09-publication-slug-migration.sql`——只写出来，**未在本机
   执行**，也没有连接任何数据库。执行前请先确认 92001-92015 这个 ID 区间在生产库里
   没有被占用（脚本开头附了确认用的 SELECT）。
2. **`src/Szaipa.Web/Controllers/PublicationController.cs`**：14 个 action 从
   `View()` 改成 `RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = ... })`
   （301 永久重定向到新的数据驱动详情页）。`index`、`zengfeng`、`chunyu3`、`tonggou2`、
   `tonggou2024` 五个 action 原样不动。
3. **删除** 14 个对应的 `Views/Publication/*.cshtml`（保留 `index.cshtml`、
   `zengfeng.cshtml`、`chunyu3.cshtml`、`tonggou2.cshtml`、`tonggou2024.cshtml`）。
4. **更新硬编码链接**：`grep -rn "/Publication/" src/Szaipa.Web/Views` 确认的三处—
   `Views/Home/NewIndex.cshtml`、`Views/Home/NewArt.cshtml`、
   `Views/Publication/index.cshtml`——把指向这 14 个已迁移 slug 的 `<a href>` 全部改成
   `/Home/Publication/{对应固定id}`；指向 `chunyu3`/`tonggou2`/`tonggou2024` 的链接
   保持不动。

## 验证

- `~/.dotnet/dotnet build Szaipa.Modernization.slnx` → 0 Warning / 0 Error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx` → 82/82 全绿（基线不变，本次改动
  是路由级别机械迁移，没有新业务逻辑，未新增测试）。
- 未新增 Web 路由冒烟测试：项目目前只有 `Szaipa.Data.Tests`（SQLite 数据层测试），没有
  现成的 Web 控制器测试项目/模式可抄；`RedirectToActionPermanent` 是 ASP.NET Core
  `Controller` 基类标准的三参跨 controller 重定向重载，build 通过已确认
  `nameof(HomeController.Publication)` 解析正确。

## 用户仍需做的事

无需数据库操作即可验证：应用会在数据库缺少固定 ID 时使用只读兼容元数据，数据库真实行
存在时则自动优先使用真实行。启动
`~/.dotnet/dotnet run --project src/Szaipa.Web/Szaipa.Web.csproj` 后，旧 slug（如
`/Publication/tonggou`）应 301 到 `/Home/Publication/{id}` 并正确渲染标题/日期；
14 个固定 ID 都应通过精确路径目录看到完整画廊。

未来若需要在 Staff 后台编辑这 14 场历史展览，可再把 SQL 作为可选的数据归一化步骤；
公开页面运行不依赖它。
